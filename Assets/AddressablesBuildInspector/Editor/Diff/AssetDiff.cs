namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Size and status comparison for one asset.
    /// </summary>
    public sealed class AssetDiff
    {
        /// <summary>Asset display name.</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>Asset path.</summary>
        public string AssetPath { get; set; } = string.Empty;

        /// <summary>Old build size in bytes.</summary>
        public long OldSize { get; set; }

        /// <summary>New build size in bytes.</summary>
        public long NewSize { get; set; }

        /// <summary>New size minus old size.</summary>
        public long Delta { get; set; }

        /// <summary>Diff status.</summary>
        public DiffStatus Status { get; set; }
    }
}
