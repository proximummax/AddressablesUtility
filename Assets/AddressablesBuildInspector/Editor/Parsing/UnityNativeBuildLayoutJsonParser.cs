using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Parsing
{
    /// <summary>
    /// Parses Unity Addressables native buildlayout.json reports.
    /// </summary>
    public sealed class UnityNativeBuildLayoutJsonParser : IBuildLayoutParser
    {
        private const string BundleTypeMarker = "BuildLayout/Bundle";
        private const string AssetTypeMarker = "BuildLayout/Asset";
        private const string AddressablesEditorAssemblyName = "Unity.Addressables.Editor";
        private const string BuildLayoutTypeName = "UnityEditor.AddressableAssets.Build.Layout.BuildLayout";

        private static readonly Regex JsonStringFieldRegex = new Regex(
            $"\"(?<name>{string.Join("|", "Name", "LoadPath", "AssetPath", "MainAssetPath", "AddressableName")})\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
            RegexOptions.Compiled);

        private static readonly Regex JsonLongFieldRegex = new Regex(
            "\"(?<name>FileSize|TotalSize|Size)\"\\s*:\\s*(?<value>-?\\d+)",
            RegexOptions.Compiled);

        /// <summary>
        /// Returns whether the file content matches Unity native build layout JSON.
        /// </summary>
        public static bool LooksLikeUnityNativeBuildLayout(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    char[] buffer = new char[8192];
                    int read = reader.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        return false;
                    }

                    string header = new string(buffer, 0, read);
                    return header.IndexOf(BundleTypeMarker, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           header.IndexOf("BuildLayout.Bundle", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

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

            if (!LooksLikeUnityNativeBuildLayout(filePath))
            {
                return BuildLayoutParseResult.Failed("The selected file is not a Unity native build layout report.");
            }

            BuildReportData report = TryParseViaReflection(filePath) ?? TryParseViaTextExtraction(filePath);
            if (report == null || (report.Bundles.Count == 0 && report.Assets.Count == 0))
            {
                return BuildLayoutParseResult.Failed("The Unity build layout report did not contain bundle or asset entries.");
            }

            return BuildLayoutParseResult.Succeeded(report);
        }

        private static BuildReportData TryParseViaReflection(string filePath)
        {
            Type buildLayoutType = Type.GetType($"{BuildLayoutTypeName}, {AddressablesEditorAssemblyName}");
            if (buildLayoutType == null)
            {
                return null;
            }

            MethodInfo openMethod = buildLayoutType.GetMethod(
                "Open",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(bool), typeof(bool) },
                null);
            if (openMethod == null)
            {
                return null;
            }

            object layout = openMethod.Invoke(null, new object[] { filePath, false, true });
            if (layout == null)
            {
                return null;
            }

            try
            {
                var report = new BuildReportData
                {
                    SourcePath = filePath,
                    LoadedAtUtc = DateTime.UtcNow
                };

                var bundlesByName = new Dictionary<string, BundleData>(StringComparer.OrdinalIgnoreCase);
                var assetsByPath = new Dictionary<string, AssetData>(StringComparer.OrdinalIgnoreCase);

                foreach (object bundle in ReadLayoutBundles(layout))
                {
                    AddBundleFromReflection(bundle, report, bundlesByName, assetsByPath);
                }

                return report;
            }
            catch (TargetInvocationException)
            {
                return null;
            }
            finally
            {
                buildLayoutType.GetMethod("Close", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            }
        }

        private static IEnumerable ReadLayoutBundles(object layout)
        {
            foreach (object bundle in ReflectionObjectReader.ReadEnumerable(
                         layout,
                         "BuiltInBundles",
                         "UnityBuiltInBundles",
                         "bundles"))
            {
                yield return bundle;
            }

            foreach (object group in ReflectionObjectReader.ReadEnumerable(layout, "Groups", "groups"))
            {
                foreach (object bundle in ReflectionObjectReader.ReadEnumerable(group, "Bundles", "bundles"))
                {
                    yield return bundle;
                }
            }
        }

        private static void AddBundleFromReflection(
            object bundleObject,
            BuildReportData report,
            IDictionary<string, BundleData> bundlesByName,
            IDictionary<string, AssetData> assetsByPath)
        {
            string bundleName = NormalizeBundleName(ReflectionObjectReader.ReadString(bundleObject, "Name", "InternalName"));
            if (bundleName.Length == 0)
            {
                return;
            }

            if (!bundlesByName.TryGetValue(bundleName, out BundleData bundle))
            {
                long bundleSizeBytes = ReflectionObjectReader.ReadLong(bundleObject, "FileSize", "Size");
                bundle = new BundleData(bundleName, bundleSizeBytes);
                bundle.Location = BundleLocationResolver.ResolveFromLoadPath(
                    ReflectionObjectReader.ReadString(bundleObject, "LoadPath"));
                bundlesByName.Add(bundleName, bundle);
                report.Bundles.Add(bundle);
            }

            foreach (object file in ReflectionObjectReader.ReadEnumerable(bundleObject, "Files", "files"))
            {
                foreach (object asset in ReflectionObjectReader.ReadEnumerable(file, "Assets", "assets"))
                {
                    AddAssetFromReflection(asset, bundle, report, assetsByPath);
                }

                foreach (object asset in ReflectionObjectReader.ReadEnumerable(file, "ImplicitAssets", "implicitAssets"))
                {
                    AddAssetFromReflection(asset, bundle, report, assetsByPath);
                }
            }
        }

        private static void AddAssetFromReflection(
            object assetObject,
            BundleData bundle,
            BuildReportData report,
            IDictionary<string, AssetData> assetsByPath)
        {
            string assetPath = NormalizeAssetPath(ReflectionObjectReader.ReadString(
                assetObject,
                "AssetPath",
                "MainAssetPath",
                "Filename",
                "AddressableName"));
            if (assetPath.Length == 0)
            {
                return;
            }

            long assetSizeBytes = ReflectionObjectReader.ReadLong(assetObject, "TotalSize", "FileSize", "Size");
            if (!assetsByPath.TryGetValue(assetPath, out AssetData asset))
            {
                asset = new AssetData(Path.GetFileName(assetPath), assetPath, assetSizeBytes);
                assetsByPath.Add(assetPath, asset);
                report.Assets.Add(asset);
            }
            else if (assetSizeBytes > asset.SizeBytes)
            {
                asset.SizeBytes = assetSizeBytes;
            }

            if (!ContainsBundleName(asset.BundleNames, bundle.Name))
            {
                asset.BundleNames.Add(bundle.Name);
            }

            if (!bundle.Assets.Contains(asset))
            {
                bundle.Assets.Add(asset);
            }
        }

        private static BuildReportData TryParseViaTextExtraction(string filePath)
        {
            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var report = new BuildReportData
            {
                SourcePath = filePath,
                LoadedAtUtc = DateTime.UtcNow
            };

            var bundlesByName = new Dictionary<string, BundleData>(StringComparer.OrdinalIgnoreCase);
            var assetsByPath = new Dictionary<string, AssetData>(StringComparer.OrdinalIgnoreCase);

            foreach (ExtractedObject extractedBundle in ExtractTypedObjects(json, BundleTypeMarker))
            {
                string bundleName = NormalizeBundleName(ReadJsonString(extractedBundle.DataJson, "Name"));
                if (bundleName.Length == 0)
                {
                    continue;
                }

                if (!bundlesByName.TryGetValue(bundleName, out BundleData bundle))
                {
                    long bundleSizeBytes = ReadJsonLong(extractedBundle.DataJson, "FileSize", "Size");
                    bundle = new BundleData(bundleName, bundleSizeBytes);
                    bundle.Location = BundleLocationResolver.ResolveFromLoadPath(
                        ReadJsonString(extractedBundle.DataJson, "LoadPath"));
                    bundlesByName.Add(bundleName, bundle);
                    report.Bundles.Add(bundle);
                }
            }

            foreach (ExtractedObject extractedAsset in ExtractTypedObjects(json, AssetTypeMarker))
            {
                string assetPath = NormalizeAssetPath(ReadJsonString(
                    extractedAsset.DataJson,
                    "AssetPath",
                    "MainAssetPath",
                    "AddressableName"));
                if (assetPath.Length == 0)
                {
                    continue;
                }

                long assetSizeBytes = ReadJsonLong(extractedAsset.DataJson, "TotalSize", "FileSize", "Size");
                if (!assetsByPath.TryGetValue(assetPath, out AssetData asset))
                {
                    asset = new AssetData(Path.GetFileName(assetPath), assetPath, assetSizeBytes);
                    assetsByPath.Add(assetPath, asset);
                    report.Assets.Add(asset);
                }
                else if (assetSizeBytes > asset.SizeBytes)
                {
                    asset.SizeBytes = assetSizeBytes;
                }
            }

            return report;
        }

        private static IEnumerable<ExtractedObject> ExtractTypedObjects(string json, string typeMarker)
        {
            int searchIndex = 0;
            while (searchIndex < json.Length)
            {
                int typeIndex = json.IndexOf(typeMarker, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (typeIndex < 0)
                {
                    yield break;
                }

                int dataIndex = json.IndexOf("\"data\":", typeIndex, StringComparison.OrdinalIgnoreCase);
                if (dataIndex < 0)
                {
                    searchIndex = typeIndex + typeMarker.Length;
                    continue;
                }

                int objectStart = json.IndexOf('{', dataIndex);
                if (objectStart < 0)
                {
                    searchIndex = typeIndex + typeMarker.Length;
                    continue;
                }

                if (!TryReadJsonObject(json, objectStart, out int objectEnd))
                {
                    searchIndex = typeIndex + typeMarker.Length;
                    continue;
                }

                yield return new ExtractedObject(json.Substring(objectStart, objectEnd - objectStart + 1));
                searchIndex = objectEnd + 1;
            }
        }

        private static bool TryReadJsonObject(string json, int startIndex, out int endIndex)
        {
            endIndex = -1;
            if (startIndex < 0 || startIndex >= json.Length || json[startIndex] != '{')
            {
                return false;
            }

            int depth = 0;
            bool inString = false;
            bool isEscaped = false;
            for (int index = startIndex; index < json.Length; index++)
            {
                char character = json[index];
                if (inString)
                {
                    if (isEscaped)
                    {
                        isEscaped = false;
                        continue;
                    }

                    if (character == '\\')
                    {
                        isEscaped = true;
                        continue;
                    }

                    if (character == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                    continue;
                }

                if (character == '{')
                {
                    depth++;
                    continue;
                }

                if (character != '}')
                {
                    continue;
                }

                depth--;
                if (depth == 0)
                {
                    endIndex = index;
                    return true;
                }
            }

            return false;
        }

        private static string ReadJsonString(string json, params string[] fieldNames)
        {
            foreach (Match match in JsonStringFieldRegex.Matches(json))
            {
                string fieldName = match.Groups["name"].Value;
                bool matches = false;
                foreach (string requestedName in fieldNames)
                {
                    if (string.Equals(fieldName, requestedName, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                        break;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                return UnescapeJsonString(match.Groups["value"].Value);
            }

            return string.Empty;
        }

        private static long ReadJsonLong(string json, params string[] fieldNames)
        {
            foreach (Match match in JsonLongFieldRegex.Matches(json))
            {
                string fieldName = match.Groups["name"].Value;
                bool matches = false;
                foreach (string requestedName in fieldNames)
                {
                    if (string.Equals(fieldName, requestedName, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                        break;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                if (long.TryParse(match.Groups["value"].Value, out long value))
                {
                    return Math.Max(0, value);
                }
            }

            return 0;
        }

        private static string UnescapeJsonString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal)
                .Replace("\\/", "/", StringComparison.Ordinal);
        }

        private static string NormalizeBundleName(string bundleName)
        {
            string normalized = (bundleName ?? string.Empty).Replace('\\', '/').Trim();
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            int slashIndex = normalized.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex < normalized.Length - 1
                ? normalized.Substring(slashIndex + 1)
                : normalized;
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty).Replace('\\', '/').Trim();
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

        private readonly struct ExtractedObject
        {
            public ExtractedObject(string dataJson)
            {
                DataJson = dataJson ?? string.Empty;
            }

            public string DataJson { get; }
        }
    }
}
