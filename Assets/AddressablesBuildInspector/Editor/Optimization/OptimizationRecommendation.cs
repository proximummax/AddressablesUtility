namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Recommended remediation for a duplicate optimization candidate.
    /// </summary>
    public enum OptimizationRecommendation
    {
        /// <summary>Move the asset to a shared group.</summary>
        MoveToSharedGroup,

        /// <summary>Create a common assets group.</summary>
        CreateCommonAssetsGroup,

        /// <summary>Split an overly broad group.</summary>
        SplitAssetGroup,

        /// <summary>Reassign bundle ownership.</summary>
        ReassignBundleOwnership,

        /// <summary>Convert the asset to a shared dependency.</summary>
        ConvertToSharedDependency,

        /// <summary>Review Addressables packing strategy.</summary>
        ReviewGroupPackingStrategy,

        /// <summary>Accept and mark as intentional duplicate.</summary>
        MarkAsIntentionalDuplicate
    }
}
