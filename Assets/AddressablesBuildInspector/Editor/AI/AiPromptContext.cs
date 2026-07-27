using System.Collections.Generic;
using AddressablesBuildInspector.Editor.Dependencies;
using AddressablesBuildInspector.Editor.Diff;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Optimization;

namespace AddressablesBuildInspector.Editor.AI
{
    /// <summary>
    /// Input data used for offline AI prompt generation.
    /// </summary>
    public sealed class AiPromptContext
    {
        public BuildReportData Report { get; set; }

        public OptimizationReport OptimizationReport { get; set; }

        public BuildDiffReport BuildDiffReport { get; set; }

        public AiReportMode Mode { get; set; } = AiReportMode.OptimizationConsultant;

        public IReadOnlyList<DependencyNode> MostReferencedAssets { get; set; }

        public IReadOnlyList<DependencyNode> SharedAssets { get; set; }

        public IReadOnlyList<DependencyChainInfo> DependencyHotspots { get; set; }
    }
}
