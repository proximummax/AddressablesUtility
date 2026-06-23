namespace AddressablesBuildInspector.Editor.Models
{
    /// <summary>
    /// Addressables bundle delivery location.
    /// </summary>
    public enum BundleLocation
    {
        /// <summary>Location is not known from the report.</summary>
        Unknown,

        /// <summary>Bundle is local to the build.</summary>
        Local,

        /// <summary>Bundle is loaded remotely.</summary>
        Remote
    }
}
