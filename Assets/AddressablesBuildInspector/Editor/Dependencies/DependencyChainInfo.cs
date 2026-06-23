namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Summary of a dependency chain rooted at one asset.
    /// </summary>
    public sealed class DependencyChainInfo
    {
        /// <summary>
        /// Root asset path.
        /// </summary>
        public string RootAssetPath { get; set; } = string.Empty;

        /// <summary>
        /// Root asset display name.
        /// </summary>
        public string RootAssetName { get; set; } = string.Empty;

        /// <summary>
        /// Longest dependency depth below the root.
        /// </summary>
        public int Depth { get; set; }
    }
}
