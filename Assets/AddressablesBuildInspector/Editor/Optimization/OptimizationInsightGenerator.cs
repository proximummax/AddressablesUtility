using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AddressablesBuildInspector.Editor.Utilities;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Generates concise ranked findings for an optimization report.
    /// </summary>
    public sealed class OptimizationInsightGenerator
    {
        /// <summary>
        /// Creates insights from candidate and simulation data.
        /// </summary>
        public IReadOnlyList<OptimizationInsight> Generate(OptimizationReport report)
        {
            var insights = new List<OptimizationInsight>();
            if (report == null)
            {
                return insights;
            }

            if (report.TotalDuplicateWasteBytes > 0)
            {
                insights.Add(CreateInsight(
                    insights.Count + 1,
                    $"Estimated duplicate waste is {ByteFormatter.FormatBytes(report.TotalDuplicateWasteBytes)} across {report.Candidates.Count.ToString(CultureInfo.InvariantCulture)} candidate assets."));
            }

            if (report.SharedGroupRecommendations.Count > 0)
            {
                SharedGroupRecommendation best = report.SharedGroupRecommendations[0];
                insights.Add(CreateInsight(
                    insights.Count + 1,
                    $"Move top duplicated assets into a shared group; {best.AssetName} alone can save about {ByteFormatter.FormatBytes(best.PotentialSavingsBytes)}."));
            }

            OptimizationCandidate largest = report.Candidates
                .OrderByDescending(candidate => candidate.DuplicateWasteBytes)
                .FirstOrDefault();
            if (largest != null)
            {
                insights.Add(CreateInsight(
                    insights.Count + 1,
                    $"{largest.Asset.Name} is duplicated in {largest.BundleCount.ToString(CultureInfo.InvariantCulture)} bundles and is the highest priority fix."));
            }

            return insights;
        }

        private static OptimizationInsight CreateInsight(int rank, string message)
        {
            return new OptimizationInsight
            {
                Rank = rank,
                Message = message
            };
        }
    }
}
