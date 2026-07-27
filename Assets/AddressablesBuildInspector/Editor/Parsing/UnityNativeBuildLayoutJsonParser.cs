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
        private const string FileTypeMarker = "BuildLayout/File";
        private const string AssetTypeMarker = "BuildLayout/Asset";
        private const string ExplicitAssetTypeMarker = "BuildLayout/ExplicitAsset";
        private const string DataFromOtherAssetTypeMarker = "BuildLayout/DataFromOtherAsset";
        private const string AddressablesEditorAssemblyName = "Unity.Addressables.Editor";
        private const string BuildLayoutTypeName = "UnityEditor.AddressableAssets.Build.Layout.BuildLayout";
        private const int DetectionChunkSize = 65536;
        private const int MaxDetectionBytes = 262144;

        private static readonly string[] AssetTypeMarkers =
        {
            AssetTypeMarker,
            ExplicitAssetTypeMarker,
            DataFromOtherAssetTypeMarker
        };

        private static readonly Regex JsonStringFieldRegex = new Regex(
            $"\"(?<name>{string.Join("|", "Name", "InternalName", "LoadPath", "Guid", "AssetGuid", "AssetPath", "MainAssetPath", "AddressableName", "Filename", "Path")})\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex JsonLongFieldRegex = new Regex(
            "\"(?<name>rid|FileSize|TotalSize|Size|BundleSize|SerializedSize|StreamedSize)\"\\s*:\\s*(?<value>-?\\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly bool _includeDependencies;

        /// <summary>
        /// Creates a parser that extracts the full Unity native build layout report.
        /// </summary>
        public UnityNativeBuildLayoutJsonParser()
            : this(true)
        {
        }

        /// <summary>
        /// Creates a parser with optional dependency extraction.
        /// </summary>
        /// <param name="includeDependencies">Whether to extract dependency graph edges during parsing.</param>
        public UnityNativeBuildLayoutJsonParser(bool includeDependencies)
        {
            _includeDependencies = includeDependencies;
        }

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
                    char[] buffer = new char[DetectionChunkSize];
                    int bytesScanned = 0;
                    while (!reader.EndOfStream && bytesScanned < MaxDetectionBytes)
                    {
                        int read = reader.Read(buffer, 0, buffer.Length);
                        if (read <= 0)
                        {
                            break;
                        }

                        string chunk = new string(buffer, 0, read);
                        if (ContainsUnityNativeTypeMarker(chunk))
                        {
                            return true;
                        }

                        if (bytesScanned == 0 && LooksLikeUnityNativeRootObject(chunk))
                        {
                            return true;
                        }

                        bytesScanned += read;
                    }
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

            return false;
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

            BuildReportData reflectionReport = TryParseViaReflection(filePath, _includeDependencies);
            BuildReportData textReport = TryParseViaTextExtraction(filePath, _includeDependencies);
            BuildReportData report = SelectMoreCompleteReport(reflectionReport, textReport, _includeDependencies);
            if (report == null || (report.Bundles.Count == 0 && report.Assets.Count == 0))
            {
                return BuildLayoutParseResult.Failed("The Unity build layout report did not contain bundle or asset entries.");
            }

            return BuildLayoutParseResult.Succeeded(report);
        }

        /// <summary>
        /// Parses Unity native build layout JSON without invoking Unity Addressables editor APIs.
        /// This path is safe for background parsing because it only reads the JSON file.
        /// </summary>
        /// <param name="filePath">Build layout JSON path.</param>
        /// <param name="includeDependencies">Whether to extract dependency graph edges.</param>
        /// <returns>Parse result from text extraction.</returns>
        public static BuildLayoutParseResult ParseViaTextExtractionOnly(string filePath, bool includeDependencies)
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

            BuildReportData report = TryParseViaTextExtraction(filePath, includeDependencies);
            if (report == null || (report.Bundles.Count == 0 && report.Assets.Count == 0))
            {
                return BuildLayoutParseResult.Failed("The Unity build layout report did not contain bundle or asset entries.");
            }

            return BuildLayoutParseResult.Succeeded(report);
        }

        private static BuildReportData SelectMoreCompleteReport(BuildReportData reflectionReport, BuildReportData textReport, bool scoreDependencies)
        {
            if (reflectionReport == null)
            {
                return textReport;
            }

            if (textReport == null)
            {
                return reflectionReport;
            }

            return CalculateCompletenessScore(textReport, scoreDependencies) > CalculateCompletenessScore(reflectionReport, scoreDependencies)
                ? textReport
                : reflectionReport;
        }

        private static int CalculateCompletenessScore(BuildReportData report, bool scoreDependencies)
        {
            if (report == null)
            {
                return 0;
            }

            int bundleAssetCount = CountBundleAssets(report);
            int membershipCount = CountBundleMemberships(report);
            long totalAssetSizeBytes = SumAssetSizeBytes(report);

            return (report.Bundles.Count * 2) +
                   (report.Assets.Count * 4) +
                   (bundleAssetCount * 8) +
                   (membershipCount * 8) +
                   (scoreDependencies ? report.Dependencies.Count * 16 : 0) +
                   (int)Math.Min(int.MaxValue / 4, totalAssetSizeBytes / 1024);
        }

        private static int CountBundleAssets(BuildReportData report)
        {
            int count = 0;
            foreach (BundleData bundle in report.Bundles)
            {
                count += bundle.Assets.Count;
            }

            return count;
        }

        private static int CountBundleMemberships(BuildReportData report)
        {
            int count = 0;
            foreach (AssetData asset in report.Assets)
            {
                count += asset.BundleNames.Count;
            }

            return count;
        }

        private static long SumAssetSizeBytes(BuildReportData report)
        {
            long total = 0;
            foreach (AssetData asset in report.Assets)
            {
                total += Math.Max(0, asset.SizeBytes);
            }

            return total;
        }

        private static BuildReportData TryParseViaReflection(string filePath, bool includeDependencies)
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
                    LoadedAtUtc = DateTime.UtcNow,
                    DependenciesLoaded = includeDependencies
                };

                var bundlesByName = new Dictionary<string, BundleData>(StringComparer.OrdinalIgnoreCase);
                var assetsByPath = new Dictionary<string, AssetData>(StringComparer.OrdinalIgnoreCase);

                foreach (object bundle in ReadLayoutBundles(layout))
                {
                    AddBundleFromReflection(bundle, report, bundlesByName, assetsByPath, includeDependencies);
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
            IDictionary<string, AssetData> assetsByPath,
            bool includeDependencies)
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
                bundlesByName.Add(bundleName, bundle);
                report.Bundles.Add(bundle);
            }

            foreach (object file in ReflectionObjectReader.ReadEnumerable(bundleObject, "Files", "files"))
            {
                foreach (object asset in ReflectionObjectReader.ReadEnumerable(file, "Assets", "assets"))
                {
                    AddAssetFromReflection(asset, bundle, report, assetsByPath, includeDependencies);
                }

                foreach (object asset in ReflectionObjectReader.ReadEnumerable(
                             file,
                             "OtherAssets",
                             "otherAssets",
                             "ImplicitAssets",
                             "implicitAssets"))
                {
                    AddAssetFromReflection(asset, bundle, report, assetsByPath, includeDependencies);
                }
            }

            foreach (object asset in ReflectionObjectReader.ReadEnumerable(bundleObject, "Assets", "assets", "ExplicitAssets", "explicitAssets"))
            {
                AddAssetFromReflection(asset, bundle, report, assetsByPath, includeDependencies);
            }

            foreach (object asset in ReflectionObjectReader.ReadEnumerable(
                         bundleObject,
                         "OtherAssets",
                         "otherAssets",
                         "ImplicitAssets",
                         "implicitAssets",
                         "DataFromOtherAssets",
                         "dataFromOtherAssets"))
            {
                AddAssetFromReflection(asset, bundle, report, assetsByPath, includeDependencies);
            }
        }

        private static void AddAssetFromReflection(
            object assetObject,
            BundleData bundle,
            BuildReportData report,
            IDictionary<string, AssetData> assetsByPath,
            bool includeDependencies)
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

            long assetSizeBytes = ReadReflectionAssetSizeBytes(assetObject);
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

            AddAssetToBundle(asset, bundle);
            if (includeDependencies)
            {
                AddDependenciesFromReflection(assetObject, asset.Path, report);
            }
        }

        private static long ReadReflectionAssetSizeBytes(object assetObject)
        {
            long directSize = ReflectionObjectReader.ReadLong(assetObject, "TotalSize", "FileSize", "Size");
            if (directSize > 0)
            {
                return directSize;
            }

            return Math.Max(0, ReflectionObjectReader.ReadLong(assetObject, "SerializedSize")) +
                   Math.Max(0, ReflectionObjectReader.ReadLong(assetObject, "StreamedSize"));
        }

        private static void AddDependenciesFromReflection(object assetObject, string sourcePath, BuildReportData report)
        {
            foreach (object dependency in ReflectionObjectReader.ReadEnumerable(
                         assetObject,
                         "Dependencies",
                         "dependencies",
                         "DirectDependencies",
                         "directDependencies",
                         "ReferencedAssets",
                         "referencedAssets"))
            {
                string dependencyPath = NormalizeAssetPath(ReflectionObjectReader.ReadString(
                    dependency,
                    "AssetPath",
                    "MainAssetPath",
                    "Filename",
                    "AddressableName",
                    "Path"));
                AddDependencyLine(report, sourcePath, dependencyPath);
            }
        }

        private static BuildReportData TryParseViaTextExtraction(string filePath, bool includeDependencies)
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
                LoadedAtUtc = DateTime.UtcNow,
                DependenciesLoaded = includeDependencies
            };

            var bundlesByName = new Dictionary<string, BundleData>(StringComparer.OrdinalIgnoreCase);
            var assetsByPath = new Dictionary<string, AssetData>(StringComparer.OrdinalIgnoreCase);
            var assetRecords = new List<ExtractedObject>();
            var assetRecordsByRid = new Dictionary<int, ExtractedObject>();
            var bundleRecords = new List<ExtractedObject>();
            var bundlesByRid = new Dictionary<int, BundleData>();
            var fileRecords = new List<ExtractedObject>();
            var fileBundleRidByRid = new Dictionary<int, int>();

            foreach (string assetTypeMarker in AssetTypeMarkers)
            {
                foreach (ExtractedObject extractedAsset in ExtractTypedObjects(json, assetTypeMarker))
                {
                    assetRecords.Add(extractedAsset);
                    if (extractedAsset.Rid > 0 && !assetRecordsByRid.ContainsKey(extractedAsset.Rid))
                    {
                        assetRecordsByRid.Add(extractedAsset.Rid, extractedAsset);
                    }
                }
            }

            foreach (ExtractedObject extractedFile in ExtractTypedObjects(json, FileTypeMarker))
            {
                fileRecords.Add(extractedFile);
                int bundleRid = ReadReferencedRid(extractedFile.DataJson, "Bundle");
                if (extractedFile.Rid > 0 && bundleRid > 0 && !fileBundleRidByRid.ContainsKey(extractedFile.Rid))
                {
                    fileBundleRidByRid.Add(extractedFile.Rid, bundleRid);
                }
            }

            foreach (ExtractedObject extractedBundle in ExtractTypedObjects(json, BundleTypeMarker))
            {
                bundleRecords.Add(extractedBundle);
                string bundleName = NormalizeBundleName(ReadJsonString(extractedBundle.DataJson, "Name", "InternalName"));
                if (bundleName.Length == 0)
                {
                    continue;
                }

                long bundleSizeBytes = ReadJsonLong(extractedBundle.DataJson, "FileSize", "Size", "BundleSize", "TotalSize");
                BundleData bundle = GetOrCreateBundle(bundleName, bundleSizeBytes, report, bundlesByName);
                if (extractedBundle.Rid > 0 && !bundlesByRid.ContainsKey(extractedBundle.Rid))
                {
                    bundlesByRid.Add(extractedBundle.Rid, bundle);
                }

                foreach (string fileJson in ExtractNamedArrayObjects(extractedBundle.DataJson, "Files"))
                {
                    int fileRid = (int)ReadJsonLong(fileJson, "rid");
                    if (fileRid > 0 && extractedBundle.Rid > 0 && !fileBundleRidByRid.ContainsKey(fileRid))
                    {
                        fileBundleRidByRid.Add(fileRid, extractedBundle.Rid);
                    }
                }
            }

            foreach (ExtractedObject extractedBundle in bundleRecords)
            {
                if (extractedBundle.Rid <= 0 || !bundlesByRid.TryGetValue(extractedBundle.Rid, out BundleData bundle))
                {
                    continue;
                }

                AddBundleAssetsFromJson(extractedBundle.DataJson, bundle, report, assetsByPath, assetRecordsByRid, includeDependencies);
                if (includeDependencies)
                {
                    AddBundleDependenciesFromJson(extractedBundle.DataJson, report, assetRecordsByRid);
                }
            }

            foreach (ExtractedObject extractedFile in fileRecords)
            {
                if (!TryGetBundleForFile(extractedFile.Rid, fileBundleRidByRid, bundlesByRid, out BundleData bundle))
                {
                    continue;
                }

                AddFileAssetsFromJson(extractedFile.DataJson, bundle, report, assetsByPath, assetRecordsByRid, includeDependencies);
            }

            foreach (ExtractedObject extractedAsset in assetRecords)
            {
                AssetData asset = AddExtractedAsset(extractedAsset, report, assetsByPath);
                if (asset != null)
                {
                    AddAssetToReferencedBundle(extractedAsset.DataJson, asset, fileBundleRidByRid, bundlesByRid);
                    if (includeDependencies)
                    {
                        AddDependenciesFromJsonAsset(extractedAsset.DataJson, asset.Path, report, assetRecordsByRid);
                    }
                }
            }

            AddDuplicatedAssetMemberships(
                json,
                BuildAssetsByGuid(assetRecords, assetsByPath),
                fileBundleRidByRid,
                bundlesByRid);

            return report;
        }

        private static BundleData GetOrCreateBundle(
            string bundleName,
            long sizeBytes,
            BuildReportData report,
            IDictionary<string, BundleData> bundlesByName)
        {
            if (!bundlesByName.TryGetValue(bundleName, out BundleData bundle))
            {
                bundle = new BundleData(bundleName, sizeBytes);
                bundlesByName.Add(bundleName, bundle);
                report.Bundles.Add(bundle);
            }
            else if (sizeBytes > bundle.SizeBytes)
            {
                bundle.SizeBytes = sizeBytes;
            }

            return bundle;
        }

        private static AssetData AddExtractedAsset(
            ExtractedObject extractedAsset,
            BuildReportData report,
            IDictionary<string, AssetData> assetsByPath)
        {
            return AddExtractedAssetData(extractedAsset.DataJson, report, assetsByPath);
        }

        private static AssetData AddExtractedAssetData(
            string dataJson,
            BuildReportData report,
            IDictionary<string, AssetData> assetsByPath)
        {
            string assetPath = NormalizeAssetPath(ReadJsonString(
                dataJson,
                "AssetPath",
                "MainAssetPath",
                "AddressableName",
                "Filename",
                "Path"));
            if (assetPath.Length == 0)
            {
                return null;
            }

            long assetSizeBytes = ReadAssetSizeBytes(dataJson);
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

            return asset;
        }

        private static long ReadAssetSizeBytes(string dataJson)
        {
            bool hasSerialized = TryReadTopLevelLong(dataJson, new[] { "SerializedSize" }, out long serializedSize);
            bool hasStreamed = TryReadTopLevelLong(dataJson, new[] { "StreamedSize" }, out long streamedSize);
            if (hasSerialized || hasStreamed)
            {
                return Math.Max(0, serializedSize) + Math.Max(0, streamedSize);
            }

            return ReadJsonLong(dataJson, "TotalSize", "FileSize", "Size");
        }

        private static void AddBundleAssetsFromJson(
            string bundleDataJson,
            BundleData bundle,
            BuildReportData report,
            IDictionary<string, AssetData> assetsByPath,
            IReadOnlyDictionary<int, ExtractedObject> assetRecordsByRid,
            bool includeDependencies)
        {
            foreach (string assetJson in ExtractNamedArrayObjects(
                         bundleDataJson,
                         "Assets",
                         "ExplicitAssets",
                         "ImplicitAssets",
                         "DataFromOtherAssets"))
            {
                string assetDataJson = ResolveReferencedDataJson(assetJson, assetRecordsByRid);
                AssetData asset = AddExtractedAssetData(assetDataJson, report, assetsByPath);
                if (asset == null)
                {
                    continue;
                }

                AddAssetToBundle(asset, bundle);
                if (includeDependencies)
                {
                    AddDependenciesFromJsonAsset(assetDataJson, asset.Path, report, assetRecordsByRid);
                }
            }
        }

        private static void AddFileAssetsFromJson(
            string fileDataJson,
            BundleData bundle,
            BuildReportData report,
            IDictionary<string, AssetData> assetsByPath,
            IReadOnlyDictionary<int, ExtractedObject> assetRecordsByRid,
            bool includeDependencies)
        {
            foreach (string assetJson in ExtractNamedArrayObjects(fileDataJson, "Assets", "OtherAssets", "ImplicitAssets"))
            {
                string assetDataJson = ResolveReferencedDataJson(assetJson, assetRecordsByRid);
                AssetData asset = AddExtractedAssetData(assetDataJson, report, assetsByPath);
                if (asset == null)
                {
                    continue;
                }

                AddAssetToBundle(asset, bundle);
                if (includeDependencies)
                {
                    AddDependenciesFromJsonAsset(assetDataJson, asset.Path, report, assetRecordsByRid);
                }
            }
        }

        private static void AddAssetToReferencedBundle(
            string assetDataJson,
            AssetData asset,
            IReadOnlyDictionary<int, int> fileBundleRidByRid,
            IReadOnlyDictionary<int, BundleData> bundlesByRid)
        {
            int bundleRid = ReadReferencedRid(assetDataJson, "Bundle");
            if (bundleRid > 0 && bundlesByRid.TryGetValue(bundleRid, out BundleData directBundle))
            {
                AddAssetToBundle(asset, directBundle);
                return;
            }

            int fileRid = ReadReferencedRid(assetDataJson, "File");
            if (TryGetBundleForFile(fileRid, fileBundleRidByRid, bundlesByRid, out BundleData fileBundle))
            {
                AddAssetToBundle(asset, fileBundle);
            }
        }

        private static Dictionary<string, AssetData> BuildAssetsByGuid(
            IEnumerable<ExtractedObject> assetRecords,
            IReadOnlyDictionary<string, AssetData> assetsByPath)
        {
            var assetsByGuid = new Dictionary<string, AssetData>(StringComparer.OrdinalIgnoreCase);
            foreach (ExtractedObject assetRecord in assetRecords)
            {
                string guid = ReadJsonString(assetRecord.DataJson, "Guid", "AssetGuid");
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                string path = NormalizeAssetPath(ReadJsonString(
                    assetRecord.DataJson,
                    "AssetPath",
                    "MainAssetPath",
                    "AddressableName",
                    "Filename",
                    "Path"));
                if (path.Length == 0 ||
                    !assetsByPath.TryGetValue(path, out AssetData asset) ||
                    assetsByGuid.ContainsKey(guid))
                {
                    continue;
                }

                assetsByGuid.Add(guid, asset);
            }

            return assetsByGuid;
        }

        private static void AddDuplicatedAssetMemberships(
            string json,
            IReadOnlyDictionary<string, AssetData> assetsByGuid,
            IReadOnlyDictionary<int, int> fileBundleRidByRid,
            IReadOnlyDictionary<int, BundleData> bundlesByRid)
        {
            foreach (string duplicatedAssetJson in ExtractNamedArrayObjects(json, "DuplicatedAssets"))
            {
                string guid = ReadJsonString(duplicatedAssetJson, "AssetGuid", "Guid");
                if (string.IsNullOrWhiteSpace(guid) ||
                    !assetsByGuid.TryGetValue(guid, out AssetData asset))
                {
                    continue;
                }

                foreach (string fileJson in EnumerateDuplicatedAssetFileRefs(duplicatedAssetJson))
                {
                    int fileRid = (int)ReadJsonLong(fileJson, "rid");
                    if (TryGetBundleForFile(fileRid, fileBundleRidByRid, bundlesByRid, out BundleData bundle))
                    {
                        AddAssetToBundle(asset, bundle);
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateDuplicatedAssetFileRefs(string duplicatedAssetJson)
        {
            foreach (string fileJson in ExtractNamedArrayObjects(duplicatedAssetJson, "IncludedInBundleFiles"))
            {
                yield return fileJson;
            }

            foreach (string duplicatedObjectJson in ExtractNamedArrayObjects(duplicatedAssetJson, "DuplicatedObjects"))
            {
                foreach (string fileJson in ExtractNamedArrayObjects(duplicatedObjectJson, "IncludedInBundleFiles"))
                {
                    yield return fileJson;
                }
            }
        }

        private static bool TryGetBundleForFile(
            int fileRid,
            IReadOnlyDictionary<int, int> fileBundleRidByRid,
            IReadOnlyDictionary<int, BundleData> bundlesByRid,
            out BundleData bundle)
        {
            bundle = null;
            if (fileRid <= 0 ||
                !fileBundleRidByRid.TryGetValue(fileRid, out int bundleRid) ||
                !bundlesByRid.TryGetValue(bundleRid, out BundleData resolvedBundle))
            {
                return false;
            }

            bundle = resolvedBundle;
            return true;
        }

        private static void AddDependenciesFromJsonAsset(
            string assetDataJson,
            string sourcePath,
            BuildReportData report,
            IReadOnlyDictionary<int, ExtractedObject> assetRecordsByRid)
        {
            foreach (string dependencyJson in ExtractNamedArrayObjects(
                         assetDataJson,
                         "Dependencies",
                         "DirectDependencies",
                         "ReferencedAssets",
                         "InternalReferencedOtherAssets",
                         "InternalReferencedExplicitAssets",
                         "ExternallyReferencedAssets"))
            {
                string dependencyDataJson = ResolveReferencedDataJson(dependencyJson, assetRecordsByRid);
                string dependencyPath = NormalizeAssetPath(ReadJsonString(
                    dependencyDataJson,
                    "AssetPath",
                    "MainAssetPath",
                    "AddressableName",
                    "Filename",
                    "Path"));
                AddDependencyLine(report, sourcePath, dependencyPath);
            }
        }

        private static void AddBundleDependenciesFromJson(
            string bundleDataJson,
            BuildReportData report,
            IReadOnlyDictionary<int, ExtractedObject> assetRecordsByRid)
        {
            foreach (string assetDependencyJson in ExtractNamedArrayObjects(bundleDataJson, "AssetDependencies"))
            {
                int sourceRid = ReadReferencedRid(assetDependencyJson, "rootAsset");
                int dependencyRid = ReadReferencedRid(assetDependencyJson, "dependencyAsset");
                string sourcePath = ResolveAssetPath(sourceRid, assetRecordsByRid);
                string dependencyPath = ResolveAssetPath(dependencyRid, assetRecordsByRid);
                AddDependencyLine(report, sourcePath, dependencyPath);
            }
        }

        private static string ResolveAssetPath(
            int assetRid,
            IReadOnlyDictionary<int, ExtractedObject> assetRecordsByRid)
        {
            return assetRid > 0 && assetRecordsByRid.TryGetValue(assetRid, out ExtractedObject asset)
                ? NormalizeAssetPath(ReadJsonString(asset.DataJson, "AssetPath", "MainAssetPath", "AddressableName", "Filename", "Path"))
                : string.Empty;
        }

        private static string ResolveReferencedDataJson(
            string objectJson,
            IReadOnlyDictionary<int, ExtractedObject> recordsByRid)
        {
            int rid = (int)ReadJsonLong(objectJson, "rid");
            return rid > 0 && recordsByRid.TryGetValue(rid, out ExtractedObject referenced)
                ? referenced.DataJson
                : objectJson;
        }

        private static int ReadReferencedRid(string json, string fieldName)
        {
            if (!TryFindJsonObjectField(json, 0, fieldName, out _, out int objectStart) ||
                !TryReadJsonObject(json, objectStart, out int objectEnd))
            {
                return 0;
            }

            return (int)ReadJsonLong(json.Substring(objectStart, objectEnd - objectStart + 1), "rid");
        }

        private static bool ContainsUnityNativeTypeMarker(string chunk)
        {
            if (chunk.IndexOf(BundleTypeMarker, StringComparison.OrdinalIgnoreCase) >= 0 ||
                chunk.IndexOf("BuildLayout.Bundle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            foreach (string assetTypeMarker in AssetTypeMarkers)
            {
                if (chunk.IndexOf(assetTypeMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeUnityNativeRootObject(string header)
        {
            if (header.IndexOf("\"BuildTarget\"", StringComparison.OrdinalIgnoreCase) < 0 ||
                header.IndexOf("com.unity.addressables", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return header.IndexOf("\"BuiltInBundles\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   header.IndexOf("\"Groups\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   header.IndexOf("\"references\"", StringComparison.OrdinalIgnoreCase) >= 0;
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

                if (!TryFindJsonObjectField(json, typeIndex, "data", out int dataIndex, out int objectStart))
                {
                    searchIndex = typeIndex + typeMarker.Length;
                    continue;
                }

                if (!TryReadJsonObject(json, objectStart, out int objectEnd))
                {
                    searchIndex = typeIndex + typeMarker.Length;
                    continue;
                }

                int prefixStart = Math.Max(0, typeIndex - 512);
                string prefix = json.Substring(prefixStart, dataIndex - prefixStart);
                int rid = ReadLastJsonInt(prefix, "rid");
                yield return new ExtractedObject(json.Substring(objectStart, objectEnd - objectStart + 1), rid);
                searchIndex = objectEnd + 1;
            }
        }

        private static bool TryFindJsonObjectField(
            string json,
            int startIndex,
            string fieldName,
            out int fieldIndex,
            out int objectStart)
        {
            fieldIndex = -1;
            objectStart = -1;
            string fieldToken = "\"" + fieldName + "\"";
            int searchIndex = Math.Max(0, startIndex);
            while (searchIndex < json.Length)
            {
                int candidateIndex = json.IndexOf(fieldToken, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (candidateIndex < 0)
                {
                    return false;
                }

                int colonIndex = FindNextNonWhitespace(json, candidateIndex + fieldToken.Length);
                if (colonIndex < 0 || json[colonIndex] != ':')
                {
                    searchIndex = candidateIndex + fieldToken.Length;
                    continue;
                }

                int valueIndex = FindNextNonWhitespace(json, colonIndex + 1);
                if (valueIndex >= 0 && json[valueIndex] == '{')
                {
                    fieldIndex = candidateIndex;
                    objectStart = valueIndex;
                    return true;
                }

                searchIndex = colonIndex + 1;
            }

            return false;
        }

        private static IEnumerable<string> ExtractNamedArrayObjects(string json, params string[] arrayNames)
        {
            if (string.IsNullOrEmpty(json))
            {
                yield break;
            }

            foreach (string arrayName in arrayNames)
            {
                string fieldToken = "\"" + arrayName + "\"";
                int searchIndex = 0;
                while (searchIndex < json.Length)
                {
                    int fieldIndex = json.IndexOf(fieldToken, searchIndex, StringComparison.OrdinalIgnoreCase);
                    if (fieldIndex < 0)
                    {
                        break;
                    }

                    int colonIndex = FindNextNonWhitespace(json, fieldIndex + fieldToken.Length);
                    if (colonIndex < 0 || json[colonIndex] != ':')
                    {
                        searchIndex = fieldIndex + fieldToken.Length;
                        continue;
                    }

                    int arrayStart = FindNextNonWhitespace(json, colonIndex + 1);
                    if (arrayStart < 0 || json[arrayStart] != '[')
                    {
                        searchIndex = colonIndex + 1;
                        continue;
                    }

                    if (!TryReadJsonArray(json, arrayStart, out int arrayEnd))
                    {
                        searchIndex = arrayStart + 1;
                        continue;
                    }

                    foreach (string objectJson in ExtractObjectsFromArray(json, arrayStart + 1, arrayEnd))
                    {
                        yield return objectJson;
                    }

                    searchIndex = arrayEnd + 1;
                }
            }
        }

        private static int FindNextNonWhitespace(string json, int startIndex)
        {
            for (int index = Math.Max(0, startIndex); index < json.Length; index++)
            {
                if (!char.IsWhiteSpace(json[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        private static IEnumerable<string> ExtractObjectsFromArray(string json, int startIndex, int endIndex)
        {
            int index = startIndex;
            while (index < endIndex)
            {
                if (json[index] != '{')
                {
                    index++;
                    continue;
                }

                if (!TryReadJsonObject(json, index, out int objectEnd) || objectEnd > endIndex)
                {
                    index++;
                    continue;
                }

                yield return json.Substring(index, objectEnd - index + 1);
                index = objectEnd + 1;
            }
        }

        private static bool TryReadJsonArray(string json, int startIndex, out int endIndex)
        {
            endIndex = -1;
            if (startIndex < 0 || startIndex >= json.Length || json[startIndex] != '[')
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

                if (character == '[')
                {
                    depth++;
                    continue;
                }

                if (character != ']')
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
            if (TryReadTopLevelString(json, fieldNames, out string value))
            {
                return value;
            }

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
            if (TryReadTopLevelLong(json, fieldNames, out long value))
            {
                return value;
            }

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

                if (long.TryParse(match.Groups["value"].Value, out long parsedValue))
                {
                    return Math.Max(0, parsedValue);
                }
            }

            return 0;
        }

        private static bool TryReadTopLevelString(string json, string[] fieldNames, out string value)
        {
            value = string.Empty;
            if (!TryFindTopLevelField(json, fieldNames, out int valueStart, out int valueEnd) ||
                valueStart < 0 ||
                valueStart >= json.Length ||
                json[valueStart] != '"' ||
                valueEnd <= valueStart)
            {
                return false;
            }

            value = UnescapeJsonString(json.Substring(valueStart + 1, valueEnd - valueStart - 1));
            return true;
        }

        private static bool TryReadTopLevelLong(string json, string[] fieldNames, out long value)
        {
            value = 0;
            if (!TryFindTopLevelField(json, fieldNames, out int valueStart, out int valueEnd) ||
                valueStart < 0 ||
                valueEnd <= valueStart)
            {
                return false;
            }

            string text = json.Substring(valueStart, valueEnd - valueStart + 1);
            if (!long.TryParse(text, out long parsed))
            {
                return false;
            }

            value = Math.Max(0, parsed);
            return true;
        }

        private static bool TryFindTopLevelField(
            string json,
            string[] fieldNames,
            out int valueStart,
            out int valueEnd)
        {
            valueStart = -1;
            valueEnd = -1;
            if (string.IsNullOrEmpty(json) || fieldNames == null || fieldNames.Length == 0)
            {
                return false;
            }

            int objectStart = json.IndexOf('{');
            if (objectStart < 0)
            {
                return false;
            }

            int index = objectStart + 1;
            while (index < json.Length)
            {
                index = FindNextNonWhitespace(json, index);
                if (index < 0 || index >= json.Length || json[index] == '}')
                {
                    return false;
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] != '"')
                {
                    index++;
                    continue;
                }

                int nameStart = index + 1;
                if (!TryFindJsonStringEnd(json, index, out int nameEnd))
                {
                    return false;
                }

                int colonIndex = FindNextNonWhitespace(json, nameEnd + 1);
                if (colonIndex < 0 || colonIndex >= json.Length || json[colonIndex] != ':')
                {
                    index = nameEnd + 1;
                    continue;
                }

                int currentValueStart = FindNextNonWhitespace(json, colonIndex + 1);
                if (currentValueStart < 0 ||
                    !TryFindJsonValueEnd(json, currentValueStart, out int currentValueEnd))
                {
                    return false;
                }

                if (MatchesAnyFieldName(json, nameStart, nameEnd - nameStart, fieldNames))
                {
                    valueStart = currentValueStart;
                    valueEnd = currentValueEnd;
                    return true;
                }

                index = currentValueEnd + 1;
            }

            return false;
        }

        private static bool MatchesAnyFieldName(string json, int startIndex, int length, IEnumerable<string> fieldNames)
        {
            foreach (string fieldName in fieldNames)
            {
                if (string.IsNullOrEmpty(fieldName) || fieldName.Length != length)
                {
                    continue;
                }

                if (string.Compare(json, startIndex, fieldName, 0, length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindJsonValueEnd(string json, int valueStart, out int valueEnd)
        {
            valueEnd = -1;
            if (valueStart < 0 || valueStart >= json.Length)
            {
                return false;
            }

            char first = json[valueStart];
            if (first == '"')
            {
                return TryFindJsonStringEnd(json, valueStart, out valueEnd);
            }

            if (first == '{')
            {
                return TryReadJsonObject(json, valueStart, out valueEnd);
            }

            if (first == '[')
            {
                return TryReadJsonArray(json, valueStart, out valueEnd);
            }

            int index = valueStart;
            while (index < json.Length && json[index] != ',' && json[index] != '}')
            {
                index++;
            }

            valueEnd = index - 1;
            while (valueEnd >= valueStart && char.IsWhiteSpace(json[valueEnd]))
            {
                valueEnd--;
            }

            return valueEnd >= valueStart;
        }

        private static bool TryFindJsonStringEnd(string json, int quoteStart, out int quoteEnd)
        {
            quoteEnd = -1;
            if (quoteStart < 0 || quoteStart >= json.Length || json[quoteStart] != '"')
            {
                return false;
            }

            bool isEscaped = false;
            for (int index = quoteStart + 1; index < json.Length; index++)
            {
                char character = json[index];
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
                    quoteEnd = index;
                    return true;
                }
            }

            return false;
        }

        private static int ReadLastJsonInt(string json, string fieldName)
        {
            int value = 0;
            foreach (Match match in JsonLongFieldRegex.Matches(json))
            {
                if (!string.Equals(match.Groups["name"].Value, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(match.Groups["value"].Value, out int parsed))
                {
                    value = Math.Max(0, parsed);
                }
            }

            return value;
        }

        private static string UnescapeJsonString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\/", "/");
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

        private static void AddAssetToBundle(AssetData asset, BundleData bundle)
        {
            if (asset == null || bundle == null || string.IsNullOrWhiteSpace(bundle.Name))
            {
                return;
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

        private static void AddDependencyLine(BuildReportData report, string sourcePath, string dependencyPath)
        {
            string source = NormalizeAssetPath(sourcePath);
            string dependency = NormalizeAssetPath(dependencyPath);
            if (source.Length == 0 ||
                dependency.Length == 0 ||
                string.Equals(source, dependency, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string line = source + " -> " + dependency;
            foreach (string existingLine in report.Dependencies)
            {
                if (string.Equals(existingLine, line, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            report.Dependencies.Add(line);
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
            public ExtractedObject(string dataJson, int rid)
            {
                DataJson = dataJson ?? string.Empty;
                Rid = Math.Max(0, rid);
            }

            public string DataJson { get; }

            public int Rid { get; }
        }
    }
}
