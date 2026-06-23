using System;
using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Represents an asset node inside a dependency graph.
    /// </summary>
    [Serializable]
    public sealed class DependencyNode
    {
        /// <summary>
        /// Display asset name.
        /// </summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>
        /// Project-relative asset path.
        /// </summary>
        public string AssetPath { get; set; } = string.Empty;

        /// <summary>
        /// Asset size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Direct dependency nodes.
        /// </summary>
        public List<DependencyNode> Dependencies { get; } = new List<DependencyNode>();

        /// <summary>
        /// True when the asset appears in more than one bundle.
        /// </summary>
        public bool IsDuplicate { get; set; }

        /// <summary>
        /// True when the asset is shared between bundles.
        /// </summary>
        public bool IsSharedBetweenBundles { get; set; }

        /// <summary>
        /// True when this node closes a circular dependency path.
        /// </summary>
        public bool IsCircularReference { get; set; }

        /// <summary>
        /// True when the asset was referenced but not present in the parsed report.
        /// </summary>
        public bool IsMissing { get; set; }

        /// <summary>
        /// True when the reference should be treated as broken.
        /// </summary>
        public bool IsBrokenReference { get; set; }

        /// <summary>
        /// True when the asset exceeds the configured large asset threshold.
        /// </summary>
        public bool IsLargeAsset { get; set; }

        /// <summary>
        /// Number of bundles that include this asset.
        /// </summary>
        public int BundleCount { get; set; }

        /// <summary>
        /// Number of direct references to this asset from other assets.
        /// </summary>
        public int ReferencedByCount { get; set; }

        /// <summary>
        /// Estimated duplicate waste in bytes.
        /// </summary>
        public long EstimatedDuplicateWasteBytes { get; set; }

        /// <summary>
        /// Number of direct dependencies.
        /// </summary>
        public int DependencyCount => Dependencies.Count;
    }
}
