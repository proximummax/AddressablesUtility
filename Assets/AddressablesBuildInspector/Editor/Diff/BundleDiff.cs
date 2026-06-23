namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Size and status comparison for one bundle.
    /// </summary>
    public sealed class BundleDiff
    {
        /// <summary>Bundle name.</summary>
        public string BundleName { get; set; } = string.Empty;

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
