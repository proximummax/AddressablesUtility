using System.Linq;
using AddressablesBuildInspector.Editor.Diff;
using AddressablesBuildInspector.Editor.Models;
using NUnit.Framework;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class BuildDiffAnalyzerTests
    {
        [Test]
        public void Analyze_ComparesBundleAndAssetStatuses()
        {
            BuildDiffReport report = new BuildDiffAnalyzer().Analyze(CreateOldBuild(), CreateNewBuild());

            Assert.AreEqual(30, report.TotalSizeDelta);
            Assert.AreEqual(DiffStatus.Modified, report.BundleDiffs.Single(diff => diff.BundleName == "characters.bundle").Status);
            Assert.AreEqual(DiffStatus.Removed, report.BundleDiffs.Single(diff => diff.BundleName == "environment.bundle").Status);
            Assert.AreEqual(DiffStatus.Added, report.BundleDiffs.Single(diff => diff.BundleName == "ui.bundle").Status);
            Assert.AreEqual(DiffStatus.Modified, report.AssetDiffs.Single(diff => diff.AssetPath == "Assets/Textures/Hero.png").Status);
            Assert.AreEqual(DiffStatus.Added, report.AssetDiffs.Single(diff => diff.AssetPath == "Assets/Textures/Boss.png").Status);
            Assert.AreEqual(DiffStatus.Removed, report.AssetDiffs.Single(diff => diff.AssetPath == "Assets/Scenes/Forest.unity").Status);
        }

        [Test]
        public void AnalyzeGrowthReasons_RanksLargestPositiveContributors()
        {
            BuildDiffReport report = new BuildDiffAnalyzer().Analyze(CreateOldBuild(), CreateNewBuild());

            GrowthReason first = report.GrowthReasons.First();

            Assert.AreEqual("characters.bundle", first.BundleName);
            Assert.AreEqual("Assets/Textures/Hero.png", first.AssetPath);
            Assert.AreEqual(60, first.SizeDeltaBytes);
        }

        [Test]
        public void AnalyzeDuplicateRegression_DetectsNewDuplicateWaste()
        {
            BuildDiffReport report = new BuildDiffAnalyzer().Analyze(CreateOldBuild(), CreateNewBuild());

            DuplicateRegression regression = report.DuplicateRegressions.Single();

            Assert.AreEqual("Assets/Textures/Explosion.png", regression.AssetPath);
            Assert.AreEqual(1, regression.OldBundleCount);
            Assert.AreEqual(3, regression.NewBundleCount);
            Assert.AreEqual(100, regression.WasteIntroducedBytes);
        }

        [Test]
        public void GenerateInsights_ExplainsBuildGrowthAndDuplicateRegression()
        {
            BuildDiffReport report = new BuildDiffAnalyzer().Analyze(CreateOldBuild(), CreateNewBuild());

            Assert.IsTrue(report.Insights.Any(insight => insight.Message.Contains("Build size increased")));
            Assert.IsTrue(report.Insights.Any(insight => insight.Message.Contains("Duplicate waste increased")));
            Assert.IsTrue(report.Insights.Any(insight => insight.Message.Contains("growth comes from")));
        }

        private static BuildReportData CreateOldBuild()
        {
            var heroTexture = CreateAsset("Hero.png", "Assets/Textures/Hero.png", 40, "characters.bundle");
            var explosion = CreateAsset("Explosion.png", "Assets/Textures/Explosion.png", 50, "characters.bundle");
            var forest = CreateAsset("Forest.unity", "Assets/Scenes/Forest.unity", 50, "environment.bundle");

            var characters = new BundleData("characters.bundle", 100);
            characters.Assets.Add(heroTexture);
            characters.Assets.Add(explosion);

            var environment = new BundleData("environment.bundle", 50);
            environment.Assets.Add(forest);

            return new BuildReportData
            {
                Bundles = { characters, environment },
                Assets = { heroTexture, explosion, forest }
            };
        }

        private static BuildReportData CreateNewBuild()
        {
            var heroTexture = CreateAsset("Hero.png", "Assets/Textures/Hero.png", 100, "characters.bundle");
            var bossTexture = CreateAsset("Boss.png", "Assets/Textures/Boss.png", 20, "characters.bundle");
            var explosion = CreateAsset("Explosion.png", "Assets/Textures/Explosion.png", 50, "characters.bundle", "ui.bundle", "audio.bundle");
            var menu = CreateAsset("Menu.uxml", "Assets/UI/Menu.uxml", 20, "ui.bundle");

            var characters = new BundleData("characters.bundle", 160);
            characters.Assets.Add(heroTexture);
            characters.Assets.Add(bossTexture);
            characters.Assets.Add(explosion);

            var ui = new BundleData("ui.bundle", 20);
            ui.Assets.Add(menu);
            ui.Assets.Add(explosion);

            return new BuildReportData
            {
                Bundles = { characters, ui },
                Assets = { heroTexture, bossTexture, explosion, menu }
            };
        }

        private static AssetData CreateAsset(string name, string path, long sizeBytes, params string[] bundleNames)
        {
            var asset = new AssetData(name, path, sizeBytes);
            foreach (string bundleName in bundleNames)
            {
                asset.BundleNames.Add(bundleName);
            }

            return asset;
        }
    }
}
