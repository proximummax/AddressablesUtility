namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Severity assigned to a duplicate optimization candidate.
    /// </summary>
    public enum OptimizationSeverity
    {
        /// <summary>Low impact issue.</summary>
        Low,

        /// <summary>Moderate impact issue.</summary>
        Medium,

        /// <summary>High impact issue.</summary>
        High,

        /// <summary>Critical duplicate waste or patch impact.</summary>
        Critical
    }
}
