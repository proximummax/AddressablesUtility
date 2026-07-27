using System;
using System.Linq;
using AddressablesBuildInspector.Editor.Analysis;
using AddressablesBuildInspector.Editor.Dependencies;
using AddressablesBuildInspector.Editor.Diff;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Optimization;
using AddressablesBuildInspector.Editor.Utilities;

namespace AddressablesBuildInspector.Editor.AI
{
    /// <summary>
    /// Generates offline LLM-ready prompts from Addressables Build Inspector data.
    /// </summary>
    public sealed class AiPromptGenerator
    {
        private readonly AiReportFormatter _formatter = new AiReportFormatter();

        public string GenerateReport(AiPromptContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            AiPromptTemplate template = AiPromptTemplate.ForMode(context.Mode);
            var builder = new AiPromptBuilder();
            AppendRole(builder, template);
            AppendProjectSummary(builder, context);
            AppendTopIssues(builder, context, template);

            if (template.IncludeDuplicates)
            {
                AppendTopDuplicateAssets(builder, context, template);
            }

            if (template.IncludeBuildGrowth)
            {
                AppendBuildGrowth(builder, context, template);
            }

            AppendOptimizationCandidates(builder, context, template);

            if (template.IncludeDependencies)
            {
                AppendDependencyAnalysis(builder, context, template);
            }

            AppendQuestions(builder, template);
            builder.Heading("END OF REPORT");
            return builder.ToString();
        }

        public string GenerateFixPlanPrompt(BuildReportData report, OptimizationReport optimizationReport)
        {
            var builder = new AiPromptBuilder();
            builder.Line("Analyze the following Addressables optimization report.");
            builder.Line();
            builder.Line("Create a step-by-step implementation plan.");
            builder.Line();
            builder.Line("Requirements:");
            builder.Line();
            builder.Line("- Minimize project risk.");
            builder.Line("- Preserve current loading behavior.");
            builder.Line("- Avoid unnecessary shared bundles.");
            builder.Line("- Prioritize highest ROI changes.");
            builder.Line();
            builder.Line("Output:");
            builder.Line();
            builder.Line("1. Immediate fixes.");
            builder.Line("2. Medium priority fixes.");
            builder.Line("3. Long-term improvements.");
            builder.Line("4. Validation checklist after each change.");
            builder.Line("5. Rollback strategy.");
            builder.Line();
            builder.Heading("ADDRESSABLES OPTIMIZATION REPORT");
            AppendProjectSummary(builder, new AiPromptContext { Report = report, OptimizationReport = optimizationReport });
            AppendOptimizationCandidates(builder, new AiPromptContext { Report = report, OptimizationReport = optimizationReport }, AiPromptTemplate.ForMode(AiReportMode.OptimizationConsultant));
            return builder.ToString();
        }

        private static void AppendRole(AiPromptBuilder builder, AiPromptTemplate template)
        {
            builder.Heading("ROLE");
            builder.Line("You are a senior Unity engineer specializing in Addressables optimization, asset delivery, patch size reduction and live service game deployment.");
            builder.Line("Report Type: " + template.Title);
        }

        private static void AppendProjectSummary(AiPromptBuilder builder, AiPromptContext context)
        {
            BuildReportData report = context.Report;
            BuildOverviewData overview = report != null ? new BuildAnalysisService(report).GetOverview() : null;
            OptimizationReport optimization = context.OptimizationReport;

            builder.Heading("PROJECT SUMMARY");
            builder.Field("Build Size", overview != null ? ByteFormatter.FormatBytes(overview.TotalBuildSizeBytes) : "Unknown");
            builder.Field("Bundle Count", overview != null ? overview.BundleCount.ToString() : "Unknown");
            builder.Field("Asset Count", overview != null ? overview.AssetCount.ToString() : "Unknown");
            builder.Field("Duplicate Waste", optimization != null ? ByteFormatter.FormatBytes(optimization.TotalDuplicateWasteBytes) : overview != null ? ByteFormatter.FormatBytes(overview.EstimatedDuplicateWasteBytes) : "Unknown");
            builder.Field("Potential Savings", optimization != null ? ByteFormatter.FormatBytes(optimization.PotentialSavingsBytes) : "Unknown");
        }

        private void AppendTopIssues(AiPromptBuilder builder, AiPromptContext context, AiPromptTemplate template)
        {
            builder.Heading("TOP ISSUES");
            OptimizationCandidate[] candidates = GetTopCandidates(context, template).ToArray();
            if (candidates.Length == 0)
            {
                builder.Line("No high-impact optimization issues were available.");
                return;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                OptimizationCandidate candidate = candidates[i];
                builder.Line("Issue #" + (i + 1));
                builder.Field("Description", candidate.Asset?.Path ?? "Unknown asset");
                builder.Field("Impact", ByteFormatter.FormatBytes(candidate.DuplicateWasteBytes) + " duplicate waste");
                builder.Field("Estimated Cost", ResolveEstimatedCost(candidate));
                builder.Field("Recommendation", candidate.Recommendation.ToString());
            }
        }

        private void AppendTopDuplicateAssets(AiPromptBuilder builder, AiPromptContext context, AiPromptTemplate template)
        {
            builder.Heading("TOP DUPLICATE ASSETS");
            OptimizationCandidate[] candidates = GetTopCandidates(context, template).ToArray();
            if (candidates.Length == 0)
            {
                builder.Line("No duplicate asset candidates were available.");
                return;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                builder.Line(_formatter.FormatDuplicateAsset(candidates[i], i + 1));
                builder.Line();
            }
        }

        private void AppendBuildGrowth(AiPromptBuilder builder, AiPromptContext context, AiPromptTemplate template)
        {
            builder.Heading("BUILD GROWTH ANALYSIS");
            BuildDiffReport diff = context.BuildDiffReport;
            if (diff == null)
            {
                builder.Line("No build diff data is currently available.");
                return;
            }

            builder.Field("Total Growth", AiReportFormatter.FormatSignedBytes(diff.TotalSizeDelta));
            builder.Line("Largest Growth Bundles");
            foreach (BundleDiff item in diff.BundleDiffs.Where(diffItem => diffItem.Delta > 0).OrderByDescending(diffItem => diffItem.Delta).Take(template.MaxItems))
            {
                builder.Line("- " + _formatter.FormatBundleDiff(item, 0));
            }

            builder.Line();
            builder.Line("Largest Added Assets");
            foreach (AssetDiff item in diff.AssetDiffs.Where(diffItem => diffItem.Status == DiffStatus.Added).OrderByDescending(diffItem => diffItem.NewSize).Take(template.MaxItems))
            {
                builder.Line("- " + _formatter.FormatAssetDiff(item, 0));
            }

            builder.Line();
            builder.Line("Largest Contributors");
            foreach (GrowthReason reason in diff.GrowthReasons.OrderByDescending(reason => reason.SizeDeltaBytes).Take(template.MaxItems))
            {
                builder.Line("- " + _formatter.FormatGrowthReason(reason, 0));
            }

            builder.Line();
            builder.Line("Smart Insights");
            foreach (DiffInsight insight in diff.Insights.OrderBy(insight => insight.Rank).Take(template.MaxItems))
            {
                builder.Line("- " + insight.Message);
            }
        }

        private void AppendOptimizationCandidates(AiPromptBuilder builder, AiPromptContext context, AiPromptTemplate template)
        {
            builder.Heading("OPTIMIZATION CANDIDATES");
            OptimizationReport optimization = context.OptimizationReport;
            OptimizationCandidate[] candidates = GetTopCandidates(context, template).ToArray();
            if (candidates.Length == 0)
            {
                builder.Line("No optimization candidates were available.");
                return;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                builder.Line(_formatter.FormatCandidate(candidates[i], i + 1));
            }

            builder.Line();
            builder.Field("Simulation Result", _formatter.FormatSimulation(optimization));
        }

        private void AppendDependencyAnalysis(AiPromptBuilder builder, AiPromptContext context, AiPromptTemplate template)
        {
            builder.Heading("DEPENDENCY ANALYSIS");
            builder.Line("Most Referenced Assets");
            AppendDependencyNodes(builder, context.MostReferencedAssets, template.MaxItems);
            builder.Line();
            builder.Line("Shared Assets");
            AppendDependencyNodes(builder, context.SharedAssets, template.MaxItems);
            builder.Line();
            builder.Line("Dependency Hotspots");
            if (context.DependencyHotspots == null || context.DependencyHotspots.Count == 0)
            {
                builder.Line("- No dependency hotspot data available.");
            }
            else
            {
                for (int i = 0; i < Math.Min(template.MaxItems, context.DependencyHotspots.Count); i++)
                {
                    builder.Line("- " + _formatter.FormatChain(context.DependencyHotspots[i], i + 1));
                }
            }
        }

        private static void AppendQuestions(AiPromptBuilder builder, AiPromptTemplate template)
        {
            builder.Heading("QUESTIONS FOR THE AI");
            builder.Line("1. Rank all recommendations by ROI.");
            builder.Line("2. Identify risky recommendations.");
            builder.Line("3. Suggest safer alternatives.");
            builder.Line("4. Suggest an optimization roadmap.");
            builder.Line("5. Estimate likely build size reduction.");
            builder.Line("6. Estimate likely patch size reduction.");
            builder.Line("7. Identify any optimization opportunities that may have been missed.");
            builder.Line("8. Explain tradeoffs between optimization and maintainability.");
            if (template.IncludeTechnicalAuditQuestions)
            {
                builder.Line("9. Identify Asset Store tool data gaps that would improve this audit.");
                builder.Line("10. Highlight validation steps that should be automated in CI.");
            }
        }

        private static void AppendDependencyNodes(AiPromptBuilder builder, System.Collections.Generic.IReadOnlyList<DependencyNode> nodes, int maxItems)
        {
            if (nodes == null || nodes.Count == 0)
            {
                builder.Line("- No dependency data available.");
                return;
            }

            var formatter = new AiReportFormatter();
            for (int i = 0; i < Math.Min(maxItems, nodes.Count); i++)
            {
                builder.Line("- " + formatter.FormatDependencyNode(nodes[i], i + 1));
            }
        }

        private static System.Collections.Generic.IEnumerable<OptimizationCandidate> GetTopCandidates(AiPromptContext context, AiPromptTemplate template)
        {
            return context.OptimizationReport?.Candidates
                .Where(candidate => candidate?.Asset != null)
                .OrderByDescending(candidate => candidate.Impact?.PotentialSavingsBytes ?? candidate.DuplicateWasteBytes)
                .ThenByDescending(candidate => candidate.DuplicateWasteBytes)
                .Take(template.MaxItems) ?? Enumerable.Empty<OptimizationCandidate>();
        }

        private static string ResolveEstimatedCost(OptimizationCandidate candidate)
        {
            switch (candidate.Recommendation)
            {
                case OptimizationRecommendation.MoveToSharedGroup:
                case OptimizationRecommendation.ConvertToSharedDependency:
                    return "Medium: requires group layout review and loading behavior validation.";
                case OptimizationRecommendation.CreateCommonAssetsGroup:
                    return "Medium-High: may add bundle topology and dependency changes.";
                default:
                    return "Low: review configuration before changing bundle layout.";
            }
        }
    }
}
