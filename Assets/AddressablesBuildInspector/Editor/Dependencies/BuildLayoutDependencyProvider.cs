using System;
using System.Collections.Generic;
using System.Linq;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Reads dependency edges captured from a build layout report.
    /// </summary>
    public sealed class BuildLayoutDependencyProvider : IDependencyProvider
    {
        private readonly BuildReportData _report;
        private readonly Dictionary<string, AssetData> _assetsByPath;
        private readonly Dictionary<string, List<string>> _dependenciesByPath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a provider backed by parsed build layout data.
        /// </summary>
        /// <param name="report">Parsed report data.</param>
        public BuildLayoutDependencyProvider(BuildReportData report)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _assetsByPath = report.Assets
                .GroupBy(asset => NormalizePath(asset.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            BuildIndex(report.Dependencies);
        }

        /// <inheritdoc />
        public bool HasDependencyData => _dependenciesByPath.Count > 0;

        /// <inheritdoc />
        public IReadOnlyList<DependencyNode> GetDependencies(string assetPath)
        {
            string normalizedPath = NormalizePath(assetPath);
            if (!_dependenciesByPath.TryGetValue(normalizedPath, out List<string> dependencies))
            {
                return Array.Empty<DependencyNode>();
            }

            return dependencies
                .Select(CreateNode)
                .OrderBy(node => node.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void BuildIndex(IEnumerable<string> dependencyLines)
        {
            foreach (string line in dependencyLines)
            {
                if (!TryParseDependencyLine(line, out string sourcePath, out string dependencyPath))
                {
                    continue;
                }

                if (!_dependenciesByPath.TryGetValue(sourcePath, out List<string> dependencies))
                {
                    dependencies = new List<string>();
                    _dependenciesByPath.Add(sourcePath, dependencies);
                }

                if (!ContainsDependencyPath(dependencies, dependencyPath))
                {
                    dependencies.Add(dependencyPath);
                }
            }
        }

        private DependencyNode CreateNode(string assetPath)
        {
            string normalizedPath = NormalizePath(assetPath);
            _assetsByPath.TryGetValue(normalizedPath, out AssetData asset);

            int bundleCount = asset?.BundleNames
                .Where(bundleName => !string.IsNullOrWhiteSpace(bundleName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() ?? 0;

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

        private static bool TryParseDependencyLine(string line, out string sourcePath, out string dependencyPath)
        {
            sourcePath = string.Empty;
            dependencyPath = string.Empty;

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            string cleaned = line.Trim();
            if (cleaned.StartsWith("Dependency:", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(cleaned.IndexOf(':') + 1).Trim();
            }

            string separator = cleaned.IndexOf("->", StringComparison.OrdinalIgnoreCase) >= 0
                ? "->"
                : cleaned.IndexOf("depends on", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "depends on"
                    : string.Empty;

            if (separator.Length == 0)
            {
                return false;
            }

            string[] parts = cleaned.Split(new[] { separator }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            sourcePath = NormalizePath(ExtractAssetPath(parts[0]));
            dependencyPath = NormalizePath(ExtractAssetPath(parts[1]));
            return sourcePath.Length > 0 && dependencyPath.Length > 0;
        }

        private static string ExtractAssetPath(string value)
        {
            int assetsIndex = value.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            int packagesIndex = value.IndexOf("Packages/", StringComparison.OrdinalIgnoreCase);
            int startIndex = assetsIndex < 0
                ? packagesIndex
                : packagesIndex < 0
                    ? assetsIndex
                    : Math.Min(assetsIndex, packagesIndex);

            if (startIndex < 0)
            {
                return value.Trim();
            }

            string path = value.Substring(startIndex).Trim();
            int delimiterIndex = path.IndexOfAny(new[] { '|', '(', '[', '\t' });
            return delimiterIndex >= 0 ? path.Substring(0, delimiterIndex).Trim() : path;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim(' ', '\t', '-', '*', ':', '|', '(', ')', '[', ']', '"', '\'');
        }

        private static string GetFileName(string path)
        {
            int slashIndex = path.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex < path.Length - 1 ? path.Substring(slashIndex + 1) : path;
        }

        private static bool ContainsDependencyPath(IReadOnlyList<string> dependencyPaths, string dependencyPath)
        {
            foreach (string existingDependencyPath in dependencyPaths)
            {
                if (string.Equals(existingDependencyPath, dependencyPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
