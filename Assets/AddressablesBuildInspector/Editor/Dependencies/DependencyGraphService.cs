using System;
using System.Collections.Generic;
using System.Linq;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Provides dependency graph queries for a parsed build report.
    /// </summary>
    public sealed class DependencyGraphService
    {
        private readonly BuildReportData _report;
        private readonly IDependencyProvider _primaryProvider;
        private readonly IDependencyProvider _fallbackProvider;
        private readonly DependencyGraphCache _cache;
        private readonly DependencyGraphSettings _settings;
        private readonly Dictionary<string, AssetData> _assetsByPath;
        private Dictionary<string, List<DependencyNode>> _referencedByIndex;

        /// <summary>
        /// Creates a dependency graph service.
        /// </summary>
        public DependencyGraphService(BuildReportData report, IDependencyProvider primaryProvider, IDependencyProvider fallbackProvider = null, DependencyGraphCache cache = null, DependencyGraphSettings settings = null)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _primaryProvider = primaryProvider ?? throw new ArgumentNullException(nameof(primaryProvider));
            _fallbackProvider = fallbackProvider;
            _cache = cache ?? new DependencyGraphCache();
            _settings = settings ?? new DependencyGraphSettings();
            _assetsByPath = report.Assets
                .GroupBy(asset => NormalizePath(asset.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets all report assets as dependency nodes.
        /// </summary>
        public IReadOnlyList<DependencyNode> GetAllAssets()
        {
            return _report.Assets.Select(CreateNode).ToList();
        }

        /// <summary>
        /// Gets direct dependencies for an asset.
        /// </summary>
        public IReadOnlyList<DependencyNode> GetDependencies(string assetPath)
        {
            string normalizedPath = NormalizePath(assetPath);
            return _cache.GetOrAddDependencies(normalizedPath, () =>
            {
                IReadOnlyList<DependencyNode> dependencies = _primaryProvider.GetDependencies(normalizedPath);
                if (dependencies.Count == 0 && !_primaryProvider.HasDependencyData && _fallbackProvider != null)
                {
                    dependencies = _fallbackProvider.GetDependencies(normalizedPath);
                }

                return dependencies;
            });
        }

        /// <summary>
        /// Gets assets that directly reference the supplied asset.
        /// </summary>
        public IReadOnlyList<DependencyNode> GetReferencedBy(string assetPath)
        {
            string normalizedPath = NormalizePath(assetPath);
            return _cache.GetOrAddReferencedBy(normalizedPath, () =>
            {
                EnsureReferencedByIndex();
                if (!_referencedByIndex.TryGetValue(normalizedPath, out List<DependencyNode> references))
                {
                    return Array.Empty<DependencyNode>();
                }

                return references
                    .OrderBy(node => node.AssetPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });
        }

        /// <summary>
        /// Gets assets used by more than one bundle.
        /// </summary>
        public IReadOnlyList<DependencyNode> GetSharedAssets()
        {
            return _report.Assets
                .Select(CreateNode)
                .Where(node => node.IsSharedBetweenBundles)
                .OrderByDescending(node => node.BundleCount)
                .ThenByDescending(node => node.SizeBytes)
                .ThenBy(node => node.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Gets assets with the highest reverse reference count.
        /// </summary>
        public IReadOnlyList<DependencyNode> GetMostReferencedAssets(int maxCount = 10)
        {
            return _report.Assets
                .Select(asset =>
                {
                    DependencyNode node = CreateNode(asset);
                    node.ReferencedByCount = GetReferencedBy(asset.Path).Count;
                    return node;
                })
                .Where(node => node.ReferencedByCount > 0)
                .OrderByDescending(node => node.ReferencedByCount)
                .ThenByDescending(node => node.SizeBytes)
                .ThenBy(node => node.AssetPath, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, maxCount))
                .ToList();
        }

        /// <summary>
        /// Gets assets with the deepest dependency chains.
        /// </summary>
        public IReadOnlyList<DependencyChainInfo> GetLargestDependencyChains(int maxCount = 10)
        {
            return _report.Assets
                .Select(asset => new DependencyChainInfo
                {
                    RootAssetName = asset.Name,
                    RootAssetPath = asset.Path,
                    Depth = CalculateDepth(asset.Path, new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                })
                .Where(chain => chain.Depth > 1)
                .OrderByDescending(chain => chain.Depth)
                .ThenBy(chain => chain.RootAssetPath, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, maxCount))
                .ToList();
        }

        /// <summary>
        /// Creates a node for an asset path.
        /// </summary>
        public DependencyNode CreateNode(string assetPath)
        {
            string normalizedPath = NormalizePath(assetPath);
            return _assetsByPath.TryGetValue(normalizedPath, out AssetData asset)
                ? CreateNode(asset)
                : new DependencyNode
                {
                    AssetName = GetFileName(normalizedPath),
                    AssetPath = normalizedPath,
                    IsMissing = true,
                    IsBrokenReference = true
                };
        }

        private DependencyNode CreateNode(AssetData asset)
        {
            int bundleCount = asset.BundleNames
                .Where(bundleName => !string.IsNullOrWhiteSpace(bundleName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            return new DependencyNode
            {
                AssetName = asset.Name,
                AssetPath = asset.Path,
                SizeBytes = asset.SizeBytes,
                BundleCount = bundleCount,
                IsDuplicate = bundleCount > 1,
                IsSharedBetweenBundles = bundleCount > 1,
                IsLargeAsset = asset.SizeBytes >= _settings.LargeAssetThresholdBytes,
                EstimatedDuplicateWasteBytes = Math.Max(0, bundleCount - 1) * Math.Max(0, asset.SizeBytes)
            };
        }

        private int CalculateDepth(string assetPath, HashSet<string> visited)
        {
            string normalizedPath = NormalizePath(assetPath);
            if (!visited.Add(normalizedPath))
            {
                return 1;
            }

            int maxChildDepth = 0;
            foreach (DependencyNode dependency in GetDependencies(normalizedPath))
            {
                maxChildDepth = Math.Max(maxChildDepth, CalculateDepth(dependency.AssetPath, new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase)));
            }

            return 1 + maxChildDepth;
        }

        private void EnsureReferencedByIndex()
        {
            if (_referencedByIndex != null)
            {
                return;
            }

            _referencedByIndex = new Dictionary<string, List<DependencyNode>>(StringComparer.OrdinalIgnoreCase);
            foreach (AssetData asset in _report.Assets)
            {
                DependencyNode sourceNode = CreateNode(asset);
                foreach (DependencyNode dependency in GetDependencies(asset.Path))
                {
                    string dependencyPath = NormalizePath(dependency.AssetPath);
                    if (!_referencedByIndex.TryGetValue(dependencyPath, out List<DependencyNode> references))
                    {
                        references = new List<DependencyNode>();
                        _referencedByIndex.Add(dependencyPath, references);
                    }

                    if (!references.Any(node => string.Equals(node.AssetPath, sourceNode.AssetPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        references.Add(sourceNode);
                    }
                }
            }
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
