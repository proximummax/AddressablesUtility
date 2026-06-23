namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Status of an item when comparing two builds.
    /// </summary>
    public enum DiffStatus
    {
        /// <summary>The item exists only in the new build.</summary>
        Added,

        /// <summary>The item exists only in the old build.</summary>
        Removed,

        /// <summary>The item exists in both builds and changed size.</summary>
        Modified,

        /// <summary>The item exists in both builds with unchanged size.</summary>
        Unchanged
    }
}
