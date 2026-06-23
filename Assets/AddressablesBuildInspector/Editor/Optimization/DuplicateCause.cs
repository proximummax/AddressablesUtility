namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Likely cause of duplicated asset inclusion.
    /// </summary>
    public enum DuplicateCause
    {
        /// <summary>Cause could not be classified.</summary>
        Unknown,

        /// <summary>Texture referenced by assets from multiple groups.</summary>
        SharedTextureAcrossGroups,

        /// <summary>Material referenced by multiple groups.</summary>
        SharedMaterialAcrossGroups,

        /// <summary>Prefab referenced by multiple groups.</summary>
        SharedPrefabReferences,

        /// <summary>Audio asset duplicated across groups.</summary>
        SharedAudioAssets,

        /// <summary>ScriptableObject duplicated across groups.</summary>
        SharedScriptableObjects,

        /// <summary>Nested prefab dependency caused duplication.</summary>
        NestedPrefabDependencies,

        /// <summary>Cross-group reference caused duplication.</summary>
        CrossGroupReferences,

        /// <summary>Likely Addressables group packing issue.</summary>
        AddressablesGroupMisconfiguration
    }
}
