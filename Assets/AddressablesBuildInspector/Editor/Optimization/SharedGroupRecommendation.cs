namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Asset recommended for a shared Addressables group.
    /// </summary>
    public sealed class SharedGroupRecommendation
    {
        /// <summary>Asset path.</summary>
        public string AssetPath { get; set; } = string.Empty;

        /// <summary>Asset display name.</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>Estimated savings.</summary>
        public long PotentialSavingsBytes { get; set; }

        /// <summary>Number of affected bundles.</summary>
        public int BundleCount { get; set; }
    }
}
