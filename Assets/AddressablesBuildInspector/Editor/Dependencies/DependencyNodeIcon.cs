namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Semantic node indicators used by the dependency explorer UI.
    /// </summary>
    public enum DependencyNodeIcon
    {
        /// <summary>No special indicator.</summary>
        None,

        /// <summary>The asset is duplicated across bundles.</summary>
        Duplicate,

        /// <summary>The asset is shared between bundles.</summary>
        Shared,

        /// <summary>The asset exceeds the configured size threshold.</summary>
        Large,

        /// <summary>The asset is referenced but missing from the report.</summary>
        Missing,

        /// <summary>The asset reference is broken.</summary>
        Broken
    }
}
