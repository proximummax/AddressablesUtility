namespace AddressablesBuildInspector.Editor.Models
{
    /// <summary>
    /// Analysis result for an asset duplicated across multiple bundles.
    /// </summary>
    public sealed class DuplicateAssetData
    {
        /// <summary>
        /// Creates an empty duplicate entry.
        /// </summary>
        public DuplicateAssetData()
        {
        }

        /// <summary>
        /// Creates a duplicate entry.
        /// </summary>
        /// <param name="asset">Duplicated asset.</param>
        /// <param name="bundleCount">Number of bundles containing the asset.</param>
        /// <param name="estimatedWasteBytes">Estimated duplicate waste in bytes.</param>
        public DuplicateAssetData(AssetData asset, int bundleCount, long estimatedWasteBytes)
        {
            Asset = asset;
            BundleCount = bundleCount;
            EstimatedWasteBytes = estimatedWasteBytes;
        }

        /// <summary>
        /// Duplicated asset.
        /// </summary>
        public AssetData Asset { get; set; }

        /// <summary>
        /// Number of bundles containing the asset.
        /// </summary>
        public int BundleCount { get; set; }

        /// <summary>
        /// Estimated waste using <c>(BundleCount - 1) * AssetSize</c>.
        /// </summary>
        public long EstimatedWasteBytes { get; set; }
    }
}
