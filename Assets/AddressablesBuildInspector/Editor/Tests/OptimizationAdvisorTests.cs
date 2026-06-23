using System.Linq;
using AddressablesBuildInspector.Editor.Dependencies;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Optimization;
using NUnit.Framework;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class OptimizationAdvisorTests
    {
        [Test]
        public void OptimizationAnalyzer_CreatesCandidatesWithCauseRecommendationAndSeverity()
        {
            BuildReportData report = CreateReport();
            var graph = new DependencyGraphService(report, new BuildLayoutDependencyProvider(report));
            var analyzer = new OptimizationAnalyzer(graph);

            OptimizationReport optimization = analyzer.Analyze(report);
            OptimizationCandidate candidate = optimization.Candidates.First();

            Assert.AreEqual("Assets/Textures/HeroTexture.png", candidate.Asset.Path);
            Assert.AreEqual(3, candidate.BundleCount);
            Assert.AreEqual(200, candidate.DuplicateWasteBytes);
            Assert.AreEqual(DuplicateCause.SharedTextureAcrossGroups, candidate.Cause);
            Assert.AreEqual(OptimizationRecommendation.MoveToSharedGroup, candidate.Recommendation);
            Assert.AreEqual(200, candidate.Impact.PotentialSavingsBytes);
            Assert.IsNotEmpty(candidate.DependencyPaths);
        }

        [Test]
        public void OptimizationSimulation_PredictsSavingsWithoutModifyingReport()
        {
            BuildReportData report = CreateReport();
            var graph = new DependencyGraphService(report, new BuildLayoutDependencyProvider(report));
            OptimizationReport optimization = new OptimizationAnalyzer(graph).Analyze(report);

            OptimizationSimulation simulation = new DuplicateOptimizationService(graph).Simulate(report, optimization.Candidates.Take(1));

            Assert.AreEqual(200, simulation.CurrentDuplicateWasteBytes);
            Assert.AreEqual(0, simulation.PredictedDuplicateWasteBytes);
            Assert.AreEqual(200, simulation.SavingsBytes);
            Assert.AreEqual(2, report.Assets.Single(asset => asset.Path == "Assets/Textures/HeroTexture.png").BundleNames.Count - 1);
        }

        [Test]
        public void OptimizationInsightGenerator_GeneratesRankedFindings()
        {
            BuildReportData report = CreateReport();
            var graph = new DependencyGraphService(report, new BuildLayoutDependencyProvider(report));
            OptimizationReport optimization = new OptimizationAnalyzer(graph).Analyze(report);

            Assert.IsTrue(optimization.Insights.Any(insight => insight.Message.Contains("duplicate waste")));
            Assert.IsTrue(optimization.Insights.Any(insight => insight.Message.Contains("shared group")));
        }

        private static BuildReportData CreateReport()
        {
            var character = new AssetData("Character.prefab", "Assets/Characters/Character.prefab", 50);
            character.BundleNames.Add("characters.bundle");
            var weapon = new AssetData("Weapon.prefab", "Assets/Weapons/Weapon.prefab", 50);
            weapon.BundleNames.Add("weapons.bundle");
            var ui = new AssetData("Menu.uxml", "Assets/UI/Menu.uxml", 20);
            ui.BundleNames.Add("ui.bundle");
            var texture = new AssetData("HeroTexture.png", "Assets/Textures/HeroTexture.png", 100);
            texture.BundleNames.Add("characters.bundle");
            texture.BundleNames.Add("weapons.bundle");
            texture.BundleNames.Add("ui.bundle");

            return new BuildReportData
            {
                Assets = { character, weapon, ui, texture },
                Dependencies =
                {
                    "Assets/Characters/Character.prefab -> Assets/Textures/HeroTexture.png",
                    "Assets/Weapons/Weapon.prefab -> Assets/Textures/HeroTexture.png",
                    "Assets/UI/Menu.uxml -> Assets/Textures/HeroTexture.png"
                }
            };
        }
    }
}
