using System.Linq;
using AddressablesBuildInspector.Editor.Dependencies;
using AddressablesBuildInspector.Editor.Models;
using NUnit.Framework;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class DependencyExplorerTests
    {
        [Test]
        public void BuildLayoutDependencyProvider_ReturnsDirectDependenciesFromReportLines()
        {
            BuildReportData report = CreateDependencyReport();
            var provider = new BuildLayoutDependencyProvider(report);

            var dependencies = provider.GetDependencies("Assets/Characters/Hero.prefab");

            Assert.AreEqual(2, dependencies.Count);
            Assert.IsTrue(dependencies.Any(node => node.AssetPath == "Assets/Materials/Hero.mat"));
            Assert.IsTrue(dependencies.Any(node => node.AssetPath == "Assets/Weapons/Sword.prefab"));
        }

        [Test]
        public void DependencyGraphService_ReturnsReferencedByAndSharedAssets()
        {
            BuildReportData report = CreateDependencyReport();
            var service = new DependencyGraphService(report, new BuildLayoutDependencyProvider(report));

            var referencedBy = service.GetReferencedBy("Assets/Textures/Hero.png");
            var sharedAssets = service.GetSharedAssets();

            Assert.AreEqual(2, referencedBy.Count);
            Assert.IsTrue(referencedBy.Any(node => node.AssetPath == "Assets/Materials/Hero.mat"));
            Assert.IsTrue(referencedBy.Any(node => node.AssetPath == "Assets/Materials/Enemy.mat"));
            Assert.IsTrue(sharedAssets.Any(node => node.AssetPath == "Assets/Textures/Hero.png"));
        }

        [Test]
        public void DependencyTreeBuilder_MarksCircularReferenceAndStopsTraversal()
        {
            var report = new BuildReportData();
            report.Assets.Add(new AssetData("A.prefab", "Assets/A.prefab", 10));
            report.Assets.Add(new AssetData("B.prefab", "Assets/B.prefab", 20));
            report.Dependencies.Add("Assets/A.prefab -> Assets/B.prefab");
            report.Dependencies.Add("Assets/B.prefab -> Assets/A.prefab");

            var service = new DependencyGraphService(report, new BuildLayoutDependencyProvider(report));
            var builder = new DependencyTreeBuilder(service);

            DependencyNode tree = builder.BuildTree("Assets/A.prefab");

            DependencyNode circularNode = tree.Dependencies[0].Dependencies[0];
            Assert.AreEqual("Assets/A.prefab", circularNode.AssetPath);
            Assert.IsTrue(circularNode.IsCircularReference);
            Assert.AreEqual(0, circularNode.Dependencies.Count);
        }

        [Test]
        public void DependencySearchService_SearchesByNameAndPath()
        {
            BuildReportData report = CreateDependencyReport();
            var service = new DependencyGraphService(report, new BuildLayoutDependencyProvider(report));
            var search = new DependencySearchService(service);

            var results = search.Search("hero");

            Assert.IsTrue(results.Any(node => node.AssetName == "Hero.prefab"));
            Assert.IsTrue(results.Any(node => node.AssetPath == "Assets/Textures/Hero.png"));
        }

        [Test]
        public void DependencyGraphService_ReturnsMostReferencedAssetsAndLargestChains()
        {
            BuildReportData report = CreateDependencyReport();
            var service = new DependencyGraphService(report, new BuildLayoutDependencyProvider(report));

            DependencyNode mostReferenced = service.GetMostReferencedAssets(1).First();
            DependencyChainInfo largestChain = service.GetLargestDependencyChains(1).First();

            Assert.AreEqual("Assets/Textures/Hero.png", mostReferenced.AssetPath);
            Assert.AreEqual(2, mostReferenced.ReferencedByCount);
            Assert.AreEqual("Assets/Characters/Hero.prefab", largestChain.RootAssetPath);
            Assert.GreaterOrEqual(largestChain.Depth, 3);
        }

        private static BuildReportData CreateDependencyReport()
        {
            var hero = new AssetData("Hero.prefab", "Assets/Characters/Hero.prefab", 1000);
            hero.BundleNames.Add("characters.bundle");

            var enemy = new AssetData("Enemy.prefab", "Assets/Characters/Enemy.prefab", 800);
            enemy.BundleNames.Add("characters.bundle");

            var heroMaterial = new AssetData("Hero.mat", "Assets/Materials/Hero.mat", 100);
            heroMaterial.BundleNames.Add("characters.bundle");

            var enemyMaterial = new AssetData("Enemy.mat", "Assets/Materials/Enemy.mat", 100);
            enemyMaterial.BundleNames.Add("enemies.bundle");

            var heroTexture = new AssetData("Hero.png", "Assets/Textures/Hero.png", 4096);
            heroTexture.BundleNames.Add("characters.bundle");
            heroTexture.BundleNames.Add("enemies.bundle");

            var sword = new AssetData("Sword.prefab", "Assets/Weapons/Sword.prefab", 512);
            sword.BundleNames.Add("characters.bundle");

            return new BuildReportData
            {
                Assets =
                {
                    hero,
                    enemy,
                    heroMaterial,
                    enemyMaterial,
                    heroTexture,
                    sword
                },
                Dependencies =
                {
                    "Assets/Characters/Hero.prefab -> Assets/Materials/Hero.mat",
                    "Assets/Characters/Hero.prefab -> Assets/Weapons/Sword.prefab",
                    "Assets/Characters/Enemy.prefab -> Assets/Materials/Enemy.mat",
                    "Assets/Materials/Hero.mat -> Assets/Textures/Hero.png",
                    "Assets/Materials/Enemy.mat -> Assets/Textures/Hero.png"
                }
            };
        }
    }
}
