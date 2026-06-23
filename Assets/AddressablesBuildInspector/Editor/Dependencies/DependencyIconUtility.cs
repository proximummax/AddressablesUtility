namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Maps dependency node state to theme-aware UI indicator classes.
    /// </summary>
    public static class DependencyIconUtility
    {
        /// <summary>
        /// Gets the highest-priority icon for a node.
        /// </summary>
        public static DependencyNodeIcon GetIcon(DependencyNode node)
        {
            if (node == null)
            {
                return DependencyNodeIcon.None;
            }

            if (node.IsBrokenReference)
            {
                return DependencyNodeIcon.Broken;
            }

            if (node.IsMissing)
            {
                return DependencyNodeIcon.Missing;
            }

            if (node.IsLargeAsset)
            {
                return DependencyNodeIcon.Large;
            }

            if (node.IsDuplicate)
            {
                return DependencyNodeIcon.Duplicate;
            }

            if (node.IsSharedBetweenBundles)
            {
                return DependencyNodeIcon.Shared;
            }

            return DependencyNodeIcon.None;
        }

        /// <summary>
        /// Gets a compact text marker for the icon.
        /// </summary>
        public static string GetMarker(DependencyNodeIcon icon)
        {
            switch (icon)
            {
                case DependencyNodeIcon.Duplicate:
                    return "D";
                case DependencyNodeIcon.Shared:
                    return "S";
                case DependencyNodeIcon.Large:
                    return "L";
                case DependencyNodeIcon.Missing:
                    return "M";
                case DependencyNodeIcon.Broken:
                    return "B";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Gets the USS class for the icon.
        /// </summary>
        public static string GetClass(DependencyNodeIcon icon)
        {
            switch (icon)
            {
                case DependencyNodeIcon.Duplicate:
                    return "abi-node-icon-duplicate";
                case DependencyNodeIcon.Shared:
                    return "abi-node-icon-shared";
                case DependencyNodeIcon.Large:
                    return "abi-node-icon-large";
                case DependencyNodeIcon.Missing:
                    return "abi-node-icon-missing";
                case DependencyNodeIcon.Broken:
                    return "abi-node-icon-broken";
                default:
                    return string.Empty;
            }
        }
    }
}
