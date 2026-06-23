using System.Collections.Generic;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Complete comparison result for two build layout reports.
    /// </summary>
    public sealed class BuildDiffReport
    {
        /// <summary>Old build report.</summary>
        public BuildReportData OldBuild { get; set; }

        /// <summary>New build report.</summary>
        public BuildReportData NewBuild { get; set; }

        /// <summary>Total new build size minus old build size.</summary>
        public long TotalSizeDelta { get; set; }

        /// <summary>Bundle-level diffs.</summary>
        public List<BundleDiff> BundleDiffs { get; } = new List<BundleDiff>();

        /// <summary>Asset-level diffs.</summary>
        public List<AssetDiff> AssetDiffs { get; } = new List<AssetDiff>();

        /// <summary>Ranked growth contributors.</summary>
        public List<GrowthReason> GrowthReasons { get; } = new List<GrowthReason>();

        /// <summary>Assets that became duplicated in the new build.</summary>
        public List<DuplicateRegression> DuplicateRegressions { get; } = new List<DuplicateRegression>();

        /// <summary>Ranked human-readable insights.</summary>
        public List<DiffInsight> Insights { get; } = new List<DiffInsight>();
    }
}
