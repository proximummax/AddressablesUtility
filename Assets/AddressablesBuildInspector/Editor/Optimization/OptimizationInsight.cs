namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Ranked optimization insight.
    /// </summary>
    public sealed class OptimizationInsight
    {
        /// <summary>Insight rank.</summary>
        public int Rank { get; set; }

        /// <summary>Insight message.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
