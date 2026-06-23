using System.Linq;
using AddressablesBuildInspector.Editor.Analysis;
using AddressablesBuildInspector.Editor.Models;
using NUnit.Framework;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class BuildAnalysisServiceTests
    {
        [Test]
        public void Overview_CalculatesCountsLargestItemsAndDuplicateWaste()
        {
            var report = CreateReport();
            var service = new BuildAnalysisService(report);

            BuildOverviewData overview = service.GetOverview();

            Assert.AreEqual(2, overview.BundleCount);
            Assert.AreEqual(3, overview.AssetCount);
            Assert.AreEqual(3072, overview.TotalBuildSizeBytes);
            Assert.AreEqual(1, overview.DuplicateAssetCount);
            Assert.AreEqual(512, overview.EstimatedDuplicateWasteBytes);
            Assert.AreEqual("environment.bundle", overview.LargestBundle.Name);
            Assert.AreEqual("Assets/Shared/Icon.png", overview.LargestAsset.Path);
        }

        [Test]
        public void GetDuplicateAssets_ReturnsAssetsSortedByWasteDescending()
        {
            var report = CreateReport();
            report.Assets.Add(new AssetData("LargeDuplicate", "Assets/Shared/Large.png", 2048)
            {
                BundleNames = { "characters.bundle", "environment.bundle" }
            });

            var service = new BuildAnalysisService(report);

            DuplicateAssetData first = service.GetDuplicateAssets().First();

            Assert.AreEqual("Assets/Shared/Large.png", first.Asset.Path);
            Assert.AreEqual(2048, first.EstimatedWasteBytes);
            Assert.AreEqual(2, first.BundleCount);
        }

        [Test]
        public void GetLargestBundles_ReturnsBundlesSortedBySizeDescending()
        {
            var service = new BuildAnalysisService(CreateReport());

            BundleData first = service.GetLargestBundles().First();

            Assert.AreEqual("environment.bundle", first.Name);
        }

        private static BuildReportData CreateReport()
        {
            var duplicate = new AssetData("Icon", "Assets/Shared/Icon.png", 512);
            duplicate.BundleNames.Add("characters.bundle");
            duplicate.BundleNames.Add("environment.bundle");

            var hero = new AssetData("Hero", "Assets/Characters/Hero.prefab", 1024);
            hero.BundleNames.Add("characters.bundle");

            var forest = new AssetData("Forest", "Assets/Scenes/Forest.unity", 1536);
            forest.BundleNames.Add("environment.bundle");

            var charactersBundle = new BundleData("characters.bundle", 1024);
            charactersBundle.Assets.Add(hero);
            charactersBundle.Assets.Add(duplicate);

            var environmentBundle = new BundleData("environment.bundle", 2048);
            environmentBundle.Assets.Add(forest);
            environmentBundle.Assets.Add(duplicate);

            return new BuildReportData
            {
                SourcePath = "SampleBuildLayout.txt",
                Bundles = { charactersBundle, environmentBundle },
                Assets = { hero, forest, duplicate }
            };
        }
    }
}
