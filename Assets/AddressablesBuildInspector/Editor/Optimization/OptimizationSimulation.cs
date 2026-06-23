namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Simulated result of applying optimization candidates.
    /// </summary>
    public sealed class OptimizationSimulation
    {
        /// <summary>Current bundle count.</summary>
        public int CurrentBundleCount { get; set; }

        /// <summary>Predicted bundle count.</summary>
        public int PredictedBundleCount { get; set; }

        /// <summary>Current duplicate waste.</summary>
        public long CurrentDuplicateWasteBytes { get; set; }

        /// <summary>Predicted duplicate waste.</summary>
        public long PredictedDuplicateWasteBytes { get; set; }

        /// <summary>Estimated savings.</summary>
        public long SavingsBytes { get; set; }

        /// <summary>Estimated patch size reduction.</summary>
        public long EstimatedPatchReductionBytes { get; set; }
    }
}
