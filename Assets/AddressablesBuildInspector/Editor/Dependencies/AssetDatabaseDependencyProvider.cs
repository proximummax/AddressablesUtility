using System;
using System.Collections.Generic;
using System.Linq;
using AddressablesBuildInspector.Editor.Models;
using UnityEditor;

namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Fallback dependency provider using Unity's AssetDatabase.
    /// </summary>
    public sealed class AssetDatabaseDependencyProvider : IDependencyProvider
    {
        private readonly Dictionary<string, AssetData> _assetsByPath;

        /// <summary>
        /// Creates an AssetDatabase-backed provider.
        /// </summary>
        /// <param name="report">Report used for asset metadata.</param>
        public AssetDatabaseDependencyProvider(BuildReportData report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            _assetsByPath = report.Assets
                .GroupBy(asset => NormalizePath(asset.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public bool HasDependencyData => true;

        /// <inheritdoc />
        public IReadOnlyList<DependencyNode> GetDependencies(string assetPath)
        {
            string normalizedPath = NormalizePath(assetPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return Array.Empty<DependencyNode>();
            }

            string[] dependencyPaths = AssetDatabase.GetDependencies(normalizedPath, false);
            return dependencyPaths
                .Where(path => !string.Equals(NormalizePath(path), normalizedPath, StringComparison.OrdinalIgnoreCase))
                .Select(CreateNode)
                .OrderBy(node => node.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private DependencyNode CreateNode(string path)
        {
            string normalizedPath = NormalizePath(path);
            _assetsByPath.TryGetValue(normalizedPath, out AssetData asset);
            int bundleCount = asset?.BundleNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() ?? 0;
            long sizeBytes = asset?.SizeBytes ?? 0;

            return new DependencyNode
            {
                AssetName = asset?.Name ?? GetFileName(normalizedPath),
                AssetPath = normalizedPath,
                SizeBytes = sizeBytes,
                BundleCount = bundleCount,
                IsDuplicate = bundleCount > 1,
                IsSharedBetweenBundles = bundleCount > 1,
                IsMissing = asset == null,
                IsBrokenReference = asset == null,
                EstimatedDuplicateWasteBytes = Math.Max(0, bundleCount - 1) * Math.Max(0, sizeBytes)
            };
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string GetFileName(string path)
        {
            int slashIndex = path.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex < path.Length - 1 ? path.Substring(slashIndex + 1) : path;
        }
    }
}
