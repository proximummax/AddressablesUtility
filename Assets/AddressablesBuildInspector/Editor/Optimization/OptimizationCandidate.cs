using System.Collections.Generic;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Actionable duplicate optimization candidate.
    /// </summary>
    public sealed class OptimizationCandidate
    {
        /// <summary>Duplicated asset.</summary>
        public AssetData Asset { get; set; }

        /// <summary>Number of bundles containing this asset.</summary>
        public int BundleCount { get; set; }

        /// <summary>Estimated duplicate waste.</summary>
        public long DuplicateWasteBytes { get; set; }

        /// <summary>Issue severity.</summary>
        public OptimizationSeverity Severity { get; set; }

        /// <summary>Likely duplicate cause.</summary>
        public DuplicateCause Cause { get; set; }

        /// <summary>Recommended fix.</summary>
        public OptimizationRecommendation Recommendation { get; set; }

        /// <summary>Estimated impact.</summary>
        public OptimizationImpact Impact { get; set; } = new OptimizationImpact();

        /// <summary>Dependency paths leading to this asset.</summary>
        public List<DependencyPath> DependencyPaths { get; } = new List<DependencyPath>();
    }
}
