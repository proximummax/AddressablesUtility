using System;
using System.Linq;
using AddressablesBuildInspector.Editor.Dependencies;
using AddressablesBuildInspector.Editor.Diff;
using AddressablesBuildInspector.Editor.Optimization;
using AddressablesBuildInspector.Editor.Utilities;

namespace AddressablesBuildInspector.Editor.AI
{
    /// <summary>
    /// Converts analysis models into compact human-readable prompt lines.
    /// </summary>
    public sealed class AiReportFormatter
    {
        public string FormatCandidate(OptimizationCandidate candidate, int rank)
        {
            if (candidate?.Asset == null)
            {
                return $"Issue #{rank}: Unknown candidate";
            }

            return $"Issue #{rank}: {candidate.Asset.Path} | Waste {ByteFormatter.FormatBytes(candidate.DuplicateWasteBytes)} | " +
                   $"Bundles {candidate.BundleCount} | Severity {candidate.Severity} | Cause {candidate.Cause} | " +
                   $"Recommendation {candidate.Recommendation} | Potential savings {ByteFormatter.FormatBytes(candidate.Impact.PotentialSavingsBytes)}";
        }

        public string FormatDuplicateAsset(OptimizationCandidate candidate, int rank)
        {
            if (candidate?.Asset == null)
            {
                return $"Asset #{rank}: Unknown";
            }

            string paths = candidate.DependencyPaths.Count == 0
                ? "Reference paths unavailable"
                : string.Join(" ; ", candidate.DependencyPaths.Take(3).Select(path => string.Join(" -> ", path.AssetPaths)));
            return $"Asset #{rank}: {candidate.Asset.Name}\n" +
                   $"Path: {candidate.Asset.Path}\n" +
                   $"Size: {ByteFormatter.FormatBytes(candidate.Asset.SizeBytes)}\n" +
                   $"Bundle Count: {candidate.BundleCount}\n" +
                   $"Waste: {ByteFormatter.FormatBytes(candidate.DuplicateWasteBytes)}\n" +
                   $"Cause: {candidate.Cause}\n" +
                   $"Reference Paths: {paths}\n" +
                   $"Recommendation: {candidate.Recommendation}";
        }

        public string FormatSimulation(OptimizationReport report)
        {
            if (report?.Simulation == null)
            {
                return "Simulation data unavailable.";
            }

            OptimizationSimulation simulation = report.Simulation;
            return $"Current duplicate waste {ByteFormatter.FormatBytes(simulation.CurrentDuplicateWasteBytes)}, " +
                   $"predicted duplicate waste {ByteFormatter.FormatBytes(simulation.PredictedDuplicateWasteBytes)}, " +
                   $"estimated savings {ByteFormatter.FormatBytes(simulation.SavingsBytes)}, " +
                   $"estimated patch reduction {ByteFormatter.FormatBytes(simulation.EstimatedPatchReductionBytes)}.";
        }

        public string FormatBundleDiff(BundleDiff diff, int rank)
        {
            return diff == null
                ? $"Bundle #{rank}: None"
                : $"Bundle #{rank}: {diff.BundleName} | Old {ByteFormatter.FormatBytes(diff.OldSize)} | New {ByteFormatter.FormatBytes(diff.NewSize)} | Delta {FormatSignedBytes(diff.Delta)} | {diff.Status}";
        }

        public string FormatAssetDiff(AssetDiff diff, int rank)
        {
            return diff == null
                ? $"Asset #{rank}: None"
                : $"Asset #{rank}: {diff.AssetPath} | Old {ByteFormatter.FormatBytes(diff.OldSize)} | New {ByteFormatter.FormatBytes(diff.NewSize)} | Delta {FormatSignedBytes(diff.Delta)} | {diff.Status}";
        }

        public string FormatGrowthReason(GrowthReason reason, int rank)
        {
            return reason == null
                ? $"Contributor #{rank}: None"
                : $"Contributor #{rank}: {reason.AssetPath} in {reason.BundleName} | +{ByteFormatter.FormatBytes(reason.SizeDeltaBytes)} | {reason.Description}";
        }

        public string FormatDependencyNode(DependencyNode node, int rank)
        {
            return node == null
                ? $"Asset #{rank}: None"
                : $"Asset #{rank}: {node.AssetPath} | Size {ByteFormatter.FormatBytes(node.SizeBytes)} | Bundles {node.BundleCount} | References {node.ReferencedByCount} | Duplicate Waste {ByteFormatter.FormatBytes(node.EstimatedDuplicateWasteBytes)}";
        }

        public string FormatChain(DependencyChainInfo chain, int rank)
        {
            return chain == null
                ? $"Hotspot #{rank}: None"
                : $"Hotspot #{rank}: {chain.RootAssetPath} | Depth {chain.Depth}";
        }

        public static string FormatSignedBytes(long value)
        {
            return value > 0
                ? "+" + ByteFormatter.FormatBytes(value)
                : value < 0
                    ? "-" + ByteFormatter.FormatBytes(Math.Abs(value))
                    : ByteFormatter.FormatBytes(0);
        }
    }
}
