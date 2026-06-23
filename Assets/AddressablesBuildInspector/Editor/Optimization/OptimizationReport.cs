using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Complete duplicate optimization analysis report.
    /// </summary>
    public sealed class OptimizationReport
    {
        /// <summary>Optimization candidates.</summary>
        public List<OptimizationCandidate> Candidates { get; } = new List<OptimizationCandidate>();

        /// <summary>Shared group recommendations.</summary>
        public List<SharedGroupRecommendation> SharedGroupRecommendations { get; } = new List<SharedGroupRecommendation>();

        /// <summary>Smart insights.</summary>
        public List<OptimizationInsight> Insights { get; } = new List<OptimizationInsight>();

        /// <summary>Simulation using top candidates.</summary>
        public OptimizationSimulation Simulation { get; set; } = new OptimizationSimulation();

        /// <summary>Total duplicate waste in bytes.</summary>
        public long TotalDuplicateWasteBytes { get; set; }

        /// <summary>Total potential savings in bytes.</summary>
        public long PotentialSavingsBytes { get; set; }

        /// <summary>Number of critical candidates.</summary>
        public int CriticalIssueCount { get; set; }
    }
}
