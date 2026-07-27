using System;
using System.Collections.Generic;
using System.IO;
using AddressablesBuildInspector.Editor.Models;
using UnityEngine;

namespace AddressablesBuildInspector.Editor.Parsing
{
    /// <summary>
    /// Parses Addressables Build Inspector JSON reports.
    /// </summary>
    public sealed class BuildReportJsonParser : IBuildLayoutParser
    {
        /// <inheritdoc />
        public BuildLayoutParseResult Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BuildLayoutParseResult.Failed("Select a JSON Build Layout report file.");
            }

            if (!File.Exists(filePath))
            {
                return BuildLayoutParseResult.Failed("The selected JSON Build Layout report file does not exist.");
            }

            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return BuildLayoutParseResult.Failed("The selected JSON Build Layout report could not be read.");
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return BuildLayoutParseResult.Failed("The selected JSON Build Layout report is empty.");
            }

            try
            {
                JsonBuildReport dto = JsonUtility.FromJson<JsonBuildReport>(json);
                if (dto == null)
                {
                    return BuildLayoutParseResult.Failed("The JSON Build Layout report could not be parsed.");
                }

                BuildReportData report = ConvertReport(filePath, dto);
                if (report.Bundles.Count == 0 && report.Assets.Count == 0)
                {
                    return BuildLayoutParseResult.Failed("The JSON Build Layout report did not contain bundle or asset entries.");
                }

                return BuildLayoutParseResult.Succeeded(report);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                return BuildLayoutParseResult.Failed("The JSON Build Layout report format is not supported.");
            }
        }

        private static BuildReportData ConvertReport(string filePath, JsonBuildReport dto)
        {
            var report = new BuildReportData
            {
                SourcePath = filePath,
                LoadedAtUtc = DateTime.UtcNow
            };

            var assetsByPath = new Dictionary<string, AssetData>(StringComparer.OrdinalIgnoreCase);
            var bundlesByName = new Dictionary<string, BundleData>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonBundle jsonBundle in dto.bundles ?? Array.Empty<JsonBundle>())
            {
                string bundleName = ResolveBundleName(jsonBundle);
                if (string.IsNullOrWhiteSpace(bundleName))
                {
                    continue;
                }

                BundleData bundle = GetOrCreateBundle(bundleName, ResolveSizeBytes(jsonBundle), report, bundlesByName);
                foreach (JsonAsset jsonAsset in jsonBundle.assets ?? Array.Empty<JsonAsset>())
                {
                    string path = ResolveAssetPath(jsonAsset);
                    if (path.Length == 0)
                    {
                        continue;
                    }

                    AssetData asset = GetOrCreateAsset(jsonAsset, path, report, assetsByPath);
                    AddAssetToBundle(asset, bundle);
                }
            }

            foreach (JsonAsset jsonAsset in dto.assets ?? Array.Empty<JsonAsset>())
            {
                string path = ResolveAssetPath(jsonAsset);
                if (path.Length == 0)
                {
                    continue;
                }

                AssetData asset = GetOrCreateAsset(jsonAsset, path, report, assetsByPath);
                AddDeclaredBundleMemberships(jsonAsset, asset, report, bundlesByName);
            }

            foreach (JsonDependency dependency in dto.dependencies ?? Array.Empty<JsonDependency>())
            {
                string source = ResolveDependencySource(dependency);
                string target = ResolveDependencyTarget(dependency);
                if (source.Length > 0 && target.Length > 0)
                {
                    report.Dependencies.Add(source + " -> " + target);
                }
            }

            return report;
        }

        private static BundleData GetOrCreateBundle(
            string bundleName,
            long sizeBytes,
            BuildReportData report,
            IDictionary<string, BundleData> bundlesByName)
        {
            string normalizedName = NormalizePath(bundleName);
            if (!bundlesByName.TryGetValue(normalizedName, out BundleData bundle))
            {
                bundle = new BundleData(normalizedName, sizeBytes);
                bundlesByName.Add(normalizedName, bundle);
                report.Bundles.Add(bundle);
            }
            else if (sizeBytes > bundle.SizeBytes)
            {
                bundle.SizeBytes = sizeBytes;
            }

            return bundle;
        }

        private static AssetData GetOrCreateAsset(
            JsonAsset jsonAsset,
            string path,
            BuildReportData report,
            IDictionary<string, AssetData> assetsByPath)
        {
            if (!assetsByPath.TryGetValue(path, out AssetData asset))
            {
                asset = new AssetData(ResolveAssetName(jsonAsset, path), path, ResolveSizeBytes(jsonAsset));
                assetsByPath.Add(path, asset);
                report.Assets.Add(asset);
            }

            long assetSizeBytes = ResolveSizeBytes(jsonAsset);
            if (assetSizeBytes > asset.SizeBytes)
            {
                asset.SizeBytes = assetSizeBytes;
            }

            return asset;
        }

        private static void AddDeclaredBundleMemberships(
            JsonAsset jsonAsset,
            AssetData asset,
            BuildReportData report,
            IDictionary<string, BundleData> bundlesByName)
        {
            foreach (string bundleName in EnumerateBundleNames(jsonAsset))
            {
                if (string.IsNullOrWhiteSpace(bundleName))
                {
                    continue;
                }

                BundleData bundle = GetOrCreateBundle(bundleName, 0, report, bundlesByName);
                AddAssetToBundle(asset, bundle);
            }
        }

        private static IEnumerable<string> EnumerateBundleNames(JsonAsset asset)
        {
            yield return asset.bundleName;
            yield return asset.BundleName;
            yield return asset.bundle;
            yield return asset.Bundle;

            foreach (string bundleName in asset.bundleNames ?? Array.Empty<string>())
            {
                yield return bundleName;
            }

            foreach (string bundleName in asset.BundleNames ?? Array.Empty<string>())
            {
                yield return bundleName;
            }

            foreach (string bundleName in asset.bundles ?? Array.Empty<string>())
            {
                yield return bundleName;
            }

            foreach (string bundleName in asset.Bundles ?? Array.Empty<string>())
            {
                yield return bundleName;
            }
        }

        private static void AddAssetToBundle(AssetData asset, BundleData bundle)
        {
            if (!ContainsBundleName(asset.BundleNames, bundle.Name))
            {
                asset.BundleNames.Add(bundle.Name);
            }

            if (!bundle.Assets.Contains(asset))
            {
                bundle.Assets.Add(asset);
            }
        }

        private static long ResolveSizeBytes(JsonBundle bundle)
        {
            return Math.Max(0, FirstPositive(
                bundle.sizeBytes,
                bundle.SizeBytes,
                bundle.FileSize,
                bundle.fileSize,
                bundle.Size,
                bundle.size,
                bundle.TotalSize,
                bundle.totalSize,
                bundle.BundleSize,
                bundle.bundleSize));
        }

        private static long ResolveSizeBytes(JsonAsset asset)
        {
            return Math.Max(0, FirstPositive(
                asset.sizeBytes,
                asset.SizeBytes,
                asset.FileSize,
                asset.fileSize,
                asset.Size,
                asset.size,
                asset.TotalSize,
                asset.totalSize));
        }

        private static long FirstPositive(params long[] values)
        {
            foreach (long value in values)
            {
                if (value > 0)
                {
                    return value;
                }
            }

            return 0;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string ResolveBundleName(JsonBundle bundle)
        {
            return FirstNonEmpty(
                bundle.name,
                bundle.Name,
                bundle.internalName,
                bundle.InternalName);
        }

        private static string ResolveAssetPath(JsonAsset asset)
        {
            return NormalizePath(FirstNonEmpty(
                asset.path,
                asset.Path,
                asset.assetPath,
                asset.AssetPath,
                asset.mainAssetPath,
                asset.MainAssetPath,
                asset.addressableName,
                asset.AddressableName));
        }

        private static string ResolveAssetName(JsonAsset asset, string path)
        {
            string name = FirstNonEmpty(asset.name, asset.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            int slashIndex = path.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex < path.Length - 1 ? path.Substring(slashIndex + 1) : path;
        }

        private static string ResolveDependencySource(JsonDependency dependency)
        {
            return NormalizePath(FirstNonEmpty(
                dependency.source,
                dependency.Source,
                dependency.from,
                dependency.From,
                dependency.sourcePath,
                dependency.SourcePath,
                dependency.asset,
                dependency.Asset));
        }

        private static string ResolveDependencyTarget(JsonDependency dependency)
        {
            return NormalizePath(FirstNonEmpty(
                dependency.target,
                dependency.Target,
                dependency.to,
                dependency.To,
                dependency.targetPath,
                dependency.TargetPath,
                dependency.dependency,
                dependency.Dependency));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool ContainsBundleName(IReadOnlyList<string> bundleNames, string bundleName)
        {
            foreach (string existingBundleName in bundleNames)
            {
                if (string.Equals(existingBundleName, bundleName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class JsonBuildReport
        {
            public JsonBundle[] bundles;
            public JsonAsset[] assets;
            public JsonDependency[] dependencies;
        }

        [Serializable]
        private sealed class JsonBundle
        {
            public string name;
            public string Name;
            public string internalName;
            public string InternalName;
            public long sizeBytes;
            public long SizeBytes;
            public long FileSize;
            public long fileSize;
            public long Size;
            public long size;
            public long TotalSize;
            public long totalSize;
            public long BundleSize;
            public long bundleSize;
            public JsonAsset[] assets;
        }

        [Serializable]
        private sealed class JsonAsset
        {
            public string name;
            public string Name;
            public string path;
            public string Path;
            public string assetPath;
            public string AssetPath;
            public string mainAssetPath;
            public string MainAssetPath;
            public string addressableName;
            public string AddressableName;
            public string bundleName;
            public string BundleName;
            public string bundle;
            public string Bundle;
            public string[] bundleNames;
            public string[] BundleNames;
            public string[] bundles;
            public string[] Bundles;
            public long sizeBytes;
            public long SizeBytes;
            public long FileSize;
            public long fileSize;
            public long Size;
            public long size;
            public long TotalSize;
            public long totalSize;
        }

        [Serializable]
        private sealed class JsonDependency
        {
            public string source;
            public string Source;
            public string from;
            public string From;
            public string sourcePath;
            public string SourcePath;
            public string asset;
            public string Asset;
            public string target;
            public string Target;
            public string to;
            public string To;
            public string targetPath;
            public string TargetPath;
            public string dependency;
            public string Dependency;
        }
#pragma warning restore 0649
    }
}
