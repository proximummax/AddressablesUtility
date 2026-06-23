namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Explains one asset-level contributor to bundle growth.
    /// </summary>
    public sealed class GrowthReason
    {
        /// <summary>Bundle where growth occurred.</summary>
        public string BundleName { get; set; } = string.Empty;

        /// <summary>Asset display name.</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>Asset path.</summary>
        public string AssetPath { get; set; } = string.Empty;

        /// <summary>Positive size delta in bytes.</summary>
        public long SizeDeltaBytes { get; set; }

        /// <summary>Human-readable explanation.</summary>
        public string Description { get; set; } = string.Empty;
    }
}
