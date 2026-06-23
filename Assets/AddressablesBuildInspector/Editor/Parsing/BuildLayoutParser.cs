using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Parsing
{
    /// <summary>
    /// Tolerant parser for Addressables build layout text reports.
    /// </summary>
    public sealed class BuildLayoutParser : IBuildLayoutParser
    {
        private static readonly Regex SizeRegex = new Regex(
            @"(?:(?:Size|Total Size|File Size)\s*[:=]\s*)?(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>bytes?|B|KB|KiB|MB|MiB|GB|GiB)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly char[] TrimChars = { ' ', '\t', '-', '*', ':', '|', '(', ')', '[', ']', '"', '\'' };

        /// <inheritdoc />
        public BuildLayoutParseResult Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BuildLayoutParseResult.Failed("Select a Build Layout report file.");
            }

            if (!File.Exists(filePath))
            {
                return BuildLayoutParseResult.Failed("The selected Build Layout report file does not exist.");
            }

            string text;
            try
            {
                text = File.ReadAllText(filePath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return BuildLayoutParseResult.Failed("The selected Build Layout report could not be read.");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return BuildLayoutParseResult.Failed("The selected Build Layout report is empty.");
            }

            if (!LooksLikeBuildLayoutReport(text))
            {
                return BuildLayoutParseResult.Failed("The selected file was not recognized as an Addressables Build Layout report.");
            }

            BuildReportData report = ParseText(filePath, text);
            if (report.Bundles.Count == 0 && report.Assets.Count == 0)
            {
                return BuildLayoutParseResult.Failed("The Build Layout report did not contain bundle or asset entries.");
            }

            return BuildLayoutParseResult.Succeeded(report);
        }

        private static BuildReportData ParseText(string filePath, string text)
        {
            var report = new BuildReportData
            {
                SourcePath = filePath,
                LoadedAtUtc = DateTime.UtcNow
            };

            var assetsByPath = new Dictionary<string, AssetData>(StringComparer.OrdinalIgnoreCase);
            var bundlesByName = new Dictionary<string, BundleData>(StringComparer.OrdinalIgnoreCase);
            BundleData currentBundle = null;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (TryParseDependencyLine(line, out string dependency))
                {
                    report.Dependencies.Add(dependency);
                    continue;
                }

                if (TryParseBundleLine(line, out string bundleName, out long bundleSizeBytes))
                {
                    currentBundle = GetOrCreateBundle(report, bundlesByName, bundleName);
                    if (bundleSizeBytes > 0)
                    {
                        currentBundle.SizeBytes = bundleSizeBytes;
                    }

                    continue;
                }

                if (currentBundle != null &&
                    currentBundle.SizeBytes <= 0 &&
                    TryParseStandaloneSizeLine(line, out long standaloneBundleSizeBytes))
                {
                    currentBundle.SizeBytes = standaloneBundleSizeBytes;
                    continue;
                }

                if (currentBundle != null &&
                    TryParseAssetLine(line, out string assetPath, out string assetName, out long assetSizeBytes))
                {
                    AssetData asset = GetOrCreateAsset(report, assetsByPath, assetPath, assetName);
                    if (assetSizeBytes > asset.SizeBytes)
                    {
                        asset.SizeBytes = assetSizeBytes;
                    }

                    AddBundleMembership(asset, currentBundle.Name);
                    AddAssetToBundle(currentBundle, asset);
                }
            }

            return report;
        }

        private static bool LooksLikeBuildLayoutReport(string text)
        {
            return text.IndexOf("Build Layout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Addressables", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Bundle:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf(".bundle", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static BundleData GetOrCreateBundle(
            BuildReportData report,
            IDictionary<string, BundleData> bundlesByName,
            string bundleName)
        {
            if (bundlesByName.TryGetValue(bundleName, out BundleData existing))
            {
                return existing;
            }

            var bundle = new BundleData(bundleName, 0);
            bundlesByName.Add(bundleName, bundle);
            report.Bundles.Add(bundle);
            return bundle;
        }

        private static AssetData GetOrCreateAsset(
            BuildReportData report,
            IDictionary<string, AssetData> assetsByPath,
            string assetPath,
            string assetName)
        {
            string normalizedPath = NormalizeAssetPath(assetPath);
            if (assetsByPath.TryGetValue(normalizedPath, out AssetData existing))
            {
                return existing;
            }

            var asset = new AssetData(assetName, normalizedPath, 0);
            assetsByPath.Add(normalizedPath, asset);
            report.Assets.Add(asset);
            return asset;
        }

        private static void AddBundleMembership(AssetData asset, string bundleName)
        {
            foreach (string existingBundleName in asset.BundleNames)
            {
                if (string.Equals(existingBundleName, bundleName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            asset.BundleNames.Add(bundleName);
        }

        private static void AddAssetToBundle(BundleData bundle, AssetData asset)
        {
            foreach (AssetData existingAsset in bundle.Assets)
            {
                if (string.Equals(existingAsset.Path, asset.Path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            bundle.Assets.Add(asset);
        }

        private static bool TryParseDependencyLine(string line, out string dependency)
        {
            dependency = string.Empty;
            if (line.StartsWith("Dependency:", StringComparison.OrdinalIgnoreCase))
            {
                dependency = line.Substring(line.IndexOf(':') + 1).Trim();
                return dependency.Length > 0;
            }

            return false;
        }

        private static bool TryParseBundleLine(string line, out string bundleName, out long sizeBytes)
        {
            bundleName = string.Empty;
            sizeBytes = 0;

            if (ContainsAssetPath(line))
            {
                return false;
            }

            string candidate = line;
            if (candidate.StartsWith("Bundle Name", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("Bundle", StringComparison.OrdinalIgnoreCase))
            {
                int separatorIndex = candidate.IndexOfAny(new[] { ':', '=' });
                if (separatorIndex >= 0)
                {
                    candidate = candidate.Substring(separatorIndex + 1);
                }
                else
                {
                    candidate = candidate.Substring("Bundle".Length);
                }
            }
            else if (candidate.IndexOf(".bundle", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            TryParseSize(candidate, out sizeBytes);
            candidate = RemoveSizeText(candidate);
            candidate = TrimAtDelimiter(candidate, '|');
            candidate = TrimAtDelimiter(candidate, '(');
            candidate = TrimAtDelimiter(candidate, '[');
            candidate = candidate.Trim(TrimChars);

            if (candidate.Length == 0)
            {
                return false;
            }

            bundleName = candidate;
            return true;
        }

        private static bool TryParseStandaloneSizeLine(string line, out long sizeBytes)
        {
            sizeBytes = 0;
            if (!line.StartsWith("Size", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Total Size", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("File Size", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryParseSize(line, out sizeBytes);
        }

        private static bool TryParseAssetLine(string line, out string assetPath, out string assetName, out long sizeBytes)
        {
            assetPath = string.Empty;
            assetName = string.Empty;
            sizeBytes = 0;

            if (!ContainsAssetPath(line))
            {
                return false;
            }

            int pathStart = FindAssetPathStart(line);
            string pathCandidate = line.Substring(pathStart);
            TryParseSize(pathCandidate, out sizeBytes);

            pathCandidate = RemoveSizeText(pathCandidate);
            pathCandidate = TrimAtDelimiter(pathCandidate, '|');
            pathCandidate = TrimAtDelimiter(pathCandidate, '(');
            pathCandidate = TrimAtDelimiter(pathCandidate, '[');
            pathCandidate = pathCandidate.Trim(TrimChars);

            if (pathCandidate.Length == 0)
            {
                return false;
            }

            assetPath = NormalizeAssetPath(pathCandidate);
            assetName = GetFileName(assetPath);
            return true;
        }

        private static bool TryParseSize(string text, out long sizeBytes)
        {
            sizeBytes = 0;
            Match match = SizeRegex.Match(text);
            if (!match.Success)
            {
                return false;
            }

            string valueText = match.Groups["value"].Value.Replace(',', '.');
            if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return false;
            }

            string unit = match.Groups["unit"].Value.ToLowerInvariant();
            double multiplier = 1d;
            if (unit == "kb" || unit == "kib")
            {
                multiplier = 1024d;
            }
            else if (unit == "mb" || unit == "mib")
            {
                multiplier = 1024d * 1024d;
            }
            else if (unit == "gb" || unit == "gib")
            {
                multiplier = 1024d * 1024d * 1024d;
            }

            sizeBytes = Math.Max(0, Convert.ToInt64(Math.Round(value * multiplier)));
            return true;
        }

        private static string RemoveSizeText(string text)
        {
            Match match = SizeRegex.Match(text);
            return match.Success ? text.Remove(match.Index).Trim() : text;
        }

        private static string TrimAtDelimiter(string value, char delimiter)
        {
            int index = value.IndexOf(delimiter);
            return index >= 0 ? value.Substring(0, index).Trim() : value;
        }

        private static bool ContainsAssetPath(string line)
        {
            return FindAssetPathStart(line) >= 0;
        }

        private static int FindAssetPathStart(string line)
        {
            int assetsIndex = line.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            int packagesIndex = line.IndexOf("Packages/", StringComparison.OrdinalIgnoreCase);

            if (assetsIndex < 0)
            {
                return packagesIndex;
            }

            if (packagesIndex < 0)
            {
                return assetsIndex;
            }

            return Math.Min(assetsIndex, packagesIndex);
        }

        private static string NormalizeAssetPath(string path)
        {
            return path.Replace('\\', '/').Trim(TrimChars);
        }

        private static string GetFileName(string assetPath)
        {
            int slashIndex = assetPath.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex < assetPath.Length - 1
                ? assetPath.Substring(slashIndex + 1)
                : assetPath;
        }
    }
}
