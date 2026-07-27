using AddressablesBuildInspector.Editor.AI;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Optimization;
using NUnit.Framework;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class AiPromptGeneratorTests
    {
        [Test]
        public void GenerateReport_IncludesLlmReadyOptimizationSections()
        {
            BuildReportData report = CreateReport();
            OptimizationReport optimization = CreateOptimizationReport(report.Assets[0]);
            var generator = new AiPromptGenerator();

            string prompt = generator.GenerateReport(new AiPromptContext
            {
                Report = report,
                OptimizationReport = optimization,
                Mode = AiReportMode.OptimizationConsultant
            });

            StringAssert.Contains("ROLE", prompt);
            StringAssert.Contains("PROJECT SUMMARY", prompt);
            StringAssert.Contains("TOP DUPLICATE ASSETS", prompt);
            StringAssert.Contains("OPTIMIZATION CANDIDATES", prompt);
            StringAssert.Contains("DEPENDENCY ANALYSIS", prompt);
            StringAssert.Contains("QUESTIONS FOR THE AI", prompt);
            StringAssert.Contains("Rank all recommendations by ROI", prompt);
        }

        [Test]
        public void GenerateFixPlanPrompt_IncludesImplementationPlanRequirements()
        {
            BuildReportData report = CreateReport();
            OptimizationReport optimization = CreateOptimizationReport(report.Assets[0]);
            var generator = new AiPromptGenerator();

            string prompt = generator.GenerateFixPlanPrompt(report, optimization);

            StringAssert.Contains("Analyze the following Addressables optimization report.", prompt);
            StringAssert.Contains("Immediate fixes", prompt);
            StringAssert.Contains("Medium priority fixes", prompt);
            StringAssert.Contains("Long-term improvements", prompt);
            StringAssert.Contains("Validation checklist after each change", prompt);
            StringAssert.Contains("Rollback strategy", prompt);
            StringAssert.Contains("Assets/Shared/Hero.png", prompt);
        }

        [Test]
        public void ReportModes_HaveUserFacingDescriptions()
        {
            foreach (AiReportMode mode in System.Enum.GetValues(typeof(AiReportMode)))
            {
                AiPromptTemplate template = AiPromptTemplate.ForMode(mode);

                Assert.IsFalse(string.IsNullOrWhiteSpace(template.Title));
                Assert.IsFalse(string.IsNullOrWhiteSpace(template.Description));
            }
        }

        private static BuildReportData CreateReport()
        {
            var asset = new AssetData("Hero.png", "Assets/Shared/Hero.png", 1024);
            asset.BundleNames.Add("characters.bundle");
            asset.BundleNames.Add("ui.bundle");

            return new BuildReportData
            {
                Bundles =
                {
                    new BundleData("characters.bundle", 4096),
                    new BundleData("ui.bundle", 2048)
                },
                Assets = { asset }
            };
        }

        private static OptimizationReport CreateOptimizationReport(AssetData asset)
        {
            var candidate = new OptimizationCandidate
            {
                Asset = asset,
                BundleCount = 2,
                DuplicateWasteBytes = 1024,
                Severity = OptimizationSeverity.Medium,
                Cause = DuplicateCause.SharedTextureAcrossGroups,
                Recommendation = OptimizationRecommendation.MoveToSharedGroup,
                Impact = new OptimizationImpact
                {
                    PotentialSavingsBytes = 1024,
                    EstimatedDownloadReductionBytes = 1024,
                    EstimatedPatchReductionBytes = 1024,
                    Confidence = 0.85f
                }
            };

            var report = new OptimizationReport
            {
                TotalDuplicateWasteBytes = 1024,
                PotentialSavingsBytes = 1024,
                Simulation = new OptimizationSimulation
                {
                    CurrentBundleCount = 2,
                    PredictedBundleCount = 2,
                    CurrentDuplicateWasteBytes = 1024,
                    PredictedDuplicateWasteBytes = 0,
                    SavingsBytes = 1024,
                    EstimatedPatchReductionBytes = 1024
                }
            };
            report.Candidates.Add(candidate);
            return report;
        }
    }
}
