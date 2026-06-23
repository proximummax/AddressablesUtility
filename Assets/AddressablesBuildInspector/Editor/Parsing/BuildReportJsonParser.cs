using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            foreach (JsonBundle jsonBundle in dto.bundles ?? Array.Empty<JsonBundle>())
            {
                var bundle = new BundleData(jsonBundle.name, Math.Max(0, jsonBundle.sizeBytes));
                bundle.Location = BundleLocationResolver.Resolve(jsonBundle.location, jsonBundle.loadPath, jsonBundle.isRemote);
                report.Bundles.Add(bundle);

                foreach (JsonAsset jsonAsset in jsonBundle.assets ?? Array.Empty<JsonAsset>())
                {
                    string path = NormalizePath(jsonAsset.path);
                    if (path.Length == 0)
                    {
                        continue;
                    }

                    if (!assetsByPath.TryGetValue(path, out AssetData asset))
                    {
                        asset = new AssetData(ResolveName(jsonAsset.name, path), path, Math.Max(0, jsonAsset.sizeBytes));
                        assetsByPath.Add(path, asset);
                        report.Assets.Add(asset);
                    }

                    if (jsonAsset.sizeBytes > asset.SizeBytes)
                    {
                        asset.SizeBytes = jsonAsset.sizeBytes;
                    }

                    if (!ContainsBundleName(asset.BundleNames, bundle.Name))
                    {
                        asset.BundleNames.Add(bundle.Name);
                    }

                    bundle.Assets.Add(asset);
                }
            }

            foreach (JsonAsset jsonAsset in dto.assets ?? Array.Empty<JsonAsset>())
            {
                string path = NormalizePath(jsonAsset.path);
                if (path.Length == 0 || assetsByPath.ContainsKey(path))
                {
                    continue;
                }

                var asset = new AssetData(ResolveName(jsonAsset.name, path), path, Math.Max(0, jsonAsset.sizeBytes));
                assetsByPath.Add(path, asset);
                report.Assets.Add(asset);
            }

            foreach (JsonDependency dependency in dto.dependencies ?? Array.Empty<JsonDependency>())
            {
                string source = NormalizePath(dependency.source);
                string target = NormalizePath(dependency.target);
                if (source.Length > 0 && target.Length > 0)
                {
                    report.Dependencies.Add(source + " -> " + target);
                }
            }

            return report;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string ResolveName(string name, string path)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            int slashIndex = path.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex < path.Length - 1 ? path.Substring(slashIndex + 1) : path;
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
            public long sizeBytes;
            public string location;
            public string loadPath;
            public bool isRemote;
            public JsonAsset[] assets;
        }

        [Serializable]
        private sealed class JsonAsset
        {
            public string name;
            public string path;
            public long sizeBytes;
        }

        [Serializable]
        private sealed class JsonDependency
        {
            public string source;
            public string target;
        }
#pragma warning restore 0649
    }
}
