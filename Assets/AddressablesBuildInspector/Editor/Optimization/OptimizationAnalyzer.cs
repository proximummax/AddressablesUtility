using System;
using System.Collections.Generic;
using System.Linq;
using AddressablesBuildInspector.Editor.Dependencies;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Builds a duplicate optimization report from parsed Addressables build data.
    /// </summary>
    public sealed class OptimizationAnalyzer
    {
        private const long FiveMegabytes = 5L * 1024L * 1024L;
        private const long TwentyFiveMegabytes = 25L * 1024L * 1024L;
        private const long HundredMegabytes = 100L * 1024L * 1024L;

        private readonly DependencyGraphService _graphService;
        private readonly DuplicateCauseAnalyzer _causeAnalyzer;
        private readonly DependencyPathTracer _pathTracer;
        private readonly SharedGroupAdvisor _sharedGroupAdvisor;
        private readonly OptimizationInsightGenerator _insightGenerator;

        /// <summary>
        /// Creates an optimization analyzer.
        /// </summary>
        public OptimizationAnalyzer(
            DependencyGraphService graphService,
            DuplicateCauseAnalyzer causeAnalyzer = null,
            DependencyPathTracer pathTracer = null,
            SharedGroupAdvisor sharedGroupAdvisor = null,
            OptimizationInsightGenerator insightGenerator = null)
        {
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
            _causeAnalyzer = causeAnalyzer ?? new DuplicateCauseAnalyzer();
            _pathTracer = pathTracer ?? new DependencyPathTracer(_graphService);
            _sharedGroupAdvisor = sharedGroupAdvisor ?? new SharedGroupAdvisor();
            _insightGenerator = insightGenerator ?? new OptimizationInsightGenerator();
        }

        /// <summary>
        /// Analyzes duplicated assets and creates optimization recommendations.
        /// </summary>
        public OptimizationReport Analyze(BuildReportData report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var optimization = new OptimizationReport();
            foreach (AssetData asset in report.Assets)
            {
                int bundleCount = CountBundles(asset);
                if (bundleCount <= 1)
                {
                    continue;
                }

                long wasteBytes = Math.Max(0, bundleCount - 1) * Math.Max(0, asset.SizeBytes);
                DuplicateCause cause = _causeAnalyzer.Analyze(asset);
                var candidate = new OptimizationCandidate
                {
                    Asset = asset,
                    BundleCount = bundleCount,
                    DuplicateWasteBytes = wasteBytes,
                    Severity = ResolveSeverity(wasteBytes, bundleCount),
                    Cause = cause,
                    Recommendation = ResolveRecommendation(cause, wasteBytes, bundleCount),
                    Impact = CreateImpact(wasteBytes, bundleCount)
                };

                candidate.DependencyPaths.AddRange(_pathTracer.TracePaths(asset));
                optimization.Candidates.Add(candidate);
            }

            optimization.Candidates.Sort(CompareCandidates);
            optimization.TotalDuplicateWasteBytes = optimization.Candidates.Sum(candidate => candidate.DuplicateWasteBytes);
            optimization.PotentialSavingsBytes = optimization.Candidates.Sum(candidate => candidate.Impact.PotentialSavingsBytes);
            optimization.CriticalIssueCount = optimization.Candidates.Count(candidate => candidate.Severity == OptimizationSeverity.Critical);
            optimization.SharedGroupRecommendations.AddRange(_sharedGroupAdvisor.Recommend(optimization.Candidates));
            optimization.Simulation = DuplicateOptimizationService.CreateSimulation(report, optimization.Candidates);
            optimization.Insights.AddRange(_insightGenerator.Generate(optimization));
            return optimization;
        }

        private static int CompareCandidates(OptimizationCandidate left, OptimizationCandidate right)
        {
            int waste = right.DuplicateWasteBytes.CompareTo(left.DuplicateWasteBytes);
            return waste != 0
                ? waste
                : string.Compare(left.Asset?.Path, right.Asset?.Path, StringComparison.OrdinalIgnoreCase);
        }

        private static int CountBundles(AssetData asset)
        {
            return asset.BundleNames
                .Where(bundleName => !string.IsNullOrWhiteSpace(bundleName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private static OptimizationSeverity ResolveSeverity(long wasteBytes, int bundleCount)
        {
            if (wasteBytes >= HundredMegabytes || bundleCount >= 5)
            {
                return OptimizationSeverity.Critical;
            }

            if (wasteBytes >= TwentyFiveMegabytes || bundleCount >= 4)
            {
                return OptimizationSeverity.High;
            }

            return wasteBytes >= FiveMegabytes || bundleCount >= 3
                ? OptimizationSeverity.Medium
                : OptimizationSeverity.Low;
        }

        private static OptimizationRecommendation ResolveRecommendation(DuplicateCause cause, long wasteBytes, int bundleCount)
        {
            switch (cause)
            {
                case DuplicateCause.SharedTextureAcrossGroups:
                case DuplicateCause.SharedMaterialAcrossGroups:
                case DuplicateCause.SharedAudioAssets:
                case DuplicateCause.SharedScriptableObjects:
                    return OptimizationRecommendation.MoveToSharedGroup;
                case DuplicateCause.SharedPrefabReferences:
                case DuplicateCause.NestedPrefabDependencies:
                    return bundleCount >= 3
                        ? OptimizationRecommendation.CreateCommonAssetsGroup
                        : OptimizationRecommendation.ConvertToSharedDependency;
                case DuplicateCause.CrossGroupReferences:
                    return wasteBytes > 0
                        ? OptimizationRecommendation.MoveToSharedGroup
                        : OptimizationRecommendation.ReviewGroupPackingStrategy;
                default:
                    return OptimizationRecommendation.ReviewGroupPackingStrategy;
            }
        }

        private static OptimizationImpact CreateImpact(long wasteBytes, int bundleCount)
        {
            float confidence = bundleCount >= 3 ? 0.85f : 0.7f;
            return new OptimizationImpact
            {
                PotentialSavingsBytes = wasteBytes,
                EstimatedDownloadReductionBytes = wasteBytes,
                EstimatedPatchReductionBytes = wasteBytes,
                Confidence = confidence
            };
        }
    }
}
