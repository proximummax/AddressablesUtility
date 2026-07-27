using System;
using System.Collections.Generic;
using System.Linq;
using AddressablesBuildInspector.Editor.Dependencies;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Facade for duplicate optimization analysis and non-destructive simulations.
    /// </summary>
    public sealed class DuplicateOptimizationService
    {
        private readonly OptimizationAnalyzer _analyzer;
        private readonly DependencyPathTracer _pathTracer;

        /// <summary>
        /// Creates an optimization service from a dependency graph.
        /// </summary>
        public DuplicateOptimizationService(DependencyGraphService graphService)
        {
            _pathTracer = new DependencyPathTracer(graphService);
            _analyzer = new OptimizationAnalyzer(graphService, pathTracer: _pathTracer, includeDependencyPaths: false);
        }

        /// <summary>
        /// Creates an optimization report for a build report.
        /// </summary>
        public OptimizationReport Analyze(BuildReportData report)
        {
            return _analyzer.Analyze(report);
        }

        /// <summary>
        /// Populates dependency paths for a single optimization candidate on demand.
        /// </summary>
        public void PopulateDependencyPaths(OptimizationCandidate candidate)
        {
            if (candidate == null || candidate.DependencyPaths.Count > 0)
            {
                return;
            }

            candidate.DependencyPaths.AddRange(_pathTracer.TracePaths(candidate.Asset));
        }

        /// <summary>
        /// Simulates applying the selected candidates without mutating the report.
        /// </summary>
        public OptimizationSimulation Simulate(BuildReportData report, IEnumerable<OptimizationCandidate> candidates)
        {
            return CreateSimulation(report, candidates);
        }

        internal static OptimizationSimulation CreateSimulation(BuildReportData report, IEnumerable<OptimizationCandidate> candidates)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            long currentWaste = report.Assets.Sum(CalculateAssetWaste);
            long savings = candidates?
                .Where(candidate => candidate?.Asset != null)
                .GroupBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First().Impact?.PotentialSavingsBytes ?? group.First().DuplicateWasteBytes)
                .Sum() ?? 0;
            savings = Math.Min(currentWaste, Math.Max(0, savings));

            return new OptimizationSimulation
            {
                CurrentBundleCount = report.Bundles.Count,
                PredictedBundleCount = report.Bundles.Count,
                CurrentDuplicateWasteBytes = currentWaste,
                PredictedDuplicateWasteBytes = Math.Max(0, currentWaste - savings),
                SavingsBytes = savings,
                EstimatedPatchReductionBytes = savings
            };
        }

        private static long CalculateAssetWaste(AssetData asset)
        {
            int bundleCount = asset.BundleNames
                .Where(bundleName => !string.IsNullOrWhiteSpace(bundleName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            return Math.Max(0, bundleCount - 1) * Math.Max(0, asset.SizeBytes);
        }
    }
}
