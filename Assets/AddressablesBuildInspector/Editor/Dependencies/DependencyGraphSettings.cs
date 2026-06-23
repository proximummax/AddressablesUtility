namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Settings used when building dependency graph metadata.
    /// </summary>
    public sealed class DependencyGraphSettings
    {
        /// <summary>
        /// Default threshold for large dependency assets.
        /// </summary>
        public const long DefaultLargeAssetThresholdBytes = 10L * 1024L * 1024L;

        /// <summary>
        /// Assets at or above this size are marked as large.
        /// </summary>
        public long LargeAssetThresholdBytes { get; set; } = DefaultLargeAssetThresholdBytes;
    }
}
