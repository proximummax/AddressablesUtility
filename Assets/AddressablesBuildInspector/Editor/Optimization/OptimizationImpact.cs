namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Estimated impact of applying an optimization recommendation.
    /// </summary>
    public sealed class OptimizationImpact
    {
        /// <summary>Estimated bytes saved from duplicate waste.</summary>
        public long PotentialSavingsBytes { get; set; }

        /// <summary>Estimated patch size reduction.</summary>
        public long EstimatedPatchReductionBytes { get; set; }

        /// <summary>Estimated download size reduction.</summary>
        public long EstimatedDownloadReductionBytes { get; set; }

        /// <summary>Confidence value from 0 to 1.</summary>
        public float Confidence { get; set; }
    }
}
