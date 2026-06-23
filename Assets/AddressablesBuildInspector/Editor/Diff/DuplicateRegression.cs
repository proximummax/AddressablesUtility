namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Describes an asset that became duplicated in the new build.
    /// </summary>
    public sealed class DuplicateRegression
    {
        /// <summary>Asset display name.</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>Asset path.</summary>
        public string AssetPath { get; set; } = string.Empty;

        /// <summary>Old bundle count.</summary>
        public int OldBundleCount { get; set; }

        /// <summary>New bundle count.</summary>
        public int NewBundleCount { get; set; }

        /// <summary>Estimated waste introduced in bytes.</summary>
        public long WasteIntroducedBytes { get; set; }
    }
}
