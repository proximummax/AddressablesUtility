namespace AddressablesBuildInspector.Editor.Models
{
    /// <summary>
    /// Summary metrics shown on the Overview tab.
    /// </summary>
    public sealed class BuildOverviewData
    {
        /// <summary>
        /// Sum of bundle sizes in bytes.
        /// </summary>
        public long TotalBuildSizeBytes { get; set; }

        /// <summary>
        /// Number of bundles in the report.
        /// </summary>
        public int BundleCount { get; set; }

        /// <summary>
        /// Number of unique assets in the report.
        /// </summary>
        public int AssetCount { get; set; }

        /// <summary>
        /// Number of assets included by more than one bundle.
        /// </summary>
        public int DuplicateAssetCount { get; set; }

        /// <summary>
        /// Estimated duplicate waste in bytes.
        /// </summary>
        public long EstimatedDuplicateWasteBytes { get; set; }

        /// <summary>
        /// Largest bundle by size.
        /// </summary>
        public BundleData LargestBundle { get; set; }

        /// <summary>
        /// Largest asset by size.
        /// </summary>
        public AssetData LargestAsset { get; set; }
    }
}
