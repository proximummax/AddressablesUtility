using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// UI-friendly projections for a build diff report.
    /// </summary>
    public sealed class DiffViewModel
    {
        /// <summary>Source diff report.</summary>
        public BuildDiffReport Report { get; set; }

        /// <summary>Bundle diffs sorted for display.</summary>
        public IReadOnlyList<BundleDiff> BundleDiffs { get; set; }

        /// <summary>Asset diffs sorted for display.</summary>
        public IReadOnlyList<AssetDiff> AssetDiffs { get; set; }

        /// <summary>Growth reasons sorted for display.</summary>
        public IReadOnlyList<GrowthReason> GrowthReasons { get; set; }
    }
}
