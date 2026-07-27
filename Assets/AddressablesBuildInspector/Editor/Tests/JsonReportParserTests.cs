using System.IO;
using System.Linq;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Parsing;
using NUnit.Framework;
using UnityEngine;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class JsonReportParserTests
    {
        [Test]
        public void BuildReportJsonParser_ParsesBundlesAssetsDependencies()
        {
            string path = WriteTempJson(
                "{",
                "  \"bundles\": [",
                "    { \"name\": \"remote.bundle\", \"sizeBytes\": 2048, \"assets\": [",
                "      { \"name\": \"Hero.png\", \"path\": \"Assets/Textures/Hero.png\", \"sizeBytes\": 1024 },",
                "      { \"name\": \"Shared.mat\", \"path\": \"Assets/Materials/Shared.mat\", \"sizeBytes\": 512 }",
                "    ] },",
                "    { \"name\": \"local.bundle\", \"sizeBytes\": 1024, \"assets\": [",
                "      { \"name\": \"Menu.uxml\", \"path\": \"Assets/UI/Menu.uxml\", \"sizeBytes\": 256 },",
                "      { \"name\": \"Shared.mat\", \"path\": \"Assets/Materials/Shared.mat\", \"sizeBytes\": 512 }",
                "    ] }",
                "  ],",
                "  \"dependencies\": [",
                "    { \"source\": \"Assets/UI/Menu.uxml\", \"target\": \"Assets/Materials/Shared.mat\" }",
                "  ]",
                "}");

            var parser = new BuildReportJsonParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(2, result.Report.Bundles.Count);
            Assert.AreEqual(2048, result.Report.Bundles.Single(bundle => bundle.Name == "remote.bundle").SizeBytes);
            Assert.AreEqual(1024, result.Report.Bundles.Single(bundle => bundle.Name == "local.bundle").SizeBytes);
            Assert.AreEqual(3, result.Report.Assets.Count);
            Assert.AreEqual(2, result.Report.Assets.Single(asset => asset.Path == "Assets/Materials/Shared.mat").BundleCount);
            Assert.AreEqual("Assets/UI/Menu.uxml -> Assets/Materials/Shared.mat", result.Report.Dependencies.Single());
        }

        [Test]
        public void BuildReportParser_SelectsJsonParserForJsonFiles()
        {
            string path = WriteTempJson("{ \"bundles\": [ { \"name\": \"content.bundle\", \"sizeBytes\": 10 } ] }");
            var parser = new BuildReportParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(10, result.Report.Bundles[0].SizeBytes);
        }

        [Test]
        public void BuildReportJsonParser_ReadsSizeAliasesUsedByJsonReports()
        {
            string path = WriteTempJson(
                "{",
                "  \"bundles\": [",
                "    { \"name\": \"characters.bundle\", \"FileSize\": 2048, \"assets\": [",
                "      { \"name\": \"Hero.png\", \"path\": \"Assets/Textures/Hero.png\", \"Size\": 1024 }",
                "    ] },",
                "    { \"name\": \"ui.bundle\", \"Size\": 4096, \"assets\": [",
                "      { \"name\": \"Hero.png\", \"path\": \"Assets/Textures/Hero.png\", \"TotalSize\": 1024 }",
                "    ] }",
                "  ]",
                "}");
            var parser = new BuildReportJsonParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(2048, result.Report.Bundles.Single(bundle => bundle.Name == "characters.bundle").SizeBytes);
            Assert.AreEqual(4096, result.Report.Bundles.Single(bundle => bundle.Name == "ui.bundle").SizeBytes);
            AssetData duplicatedAsset = result.Report.Assets.Single(asset => asset.Path == "Assets/Textures/Hero.png");
            Assert.AreEqual(1024, duplicatedAsset.SizeBytes);
            Assert.AreEqual(2, duplicatedAsset.BundleCount);
        }

        [Test]
        public void BuildReportJsonParser_UsesTopLevelAssetBundleNames()
        {
            string path = WriteTempJson(
                "{",
                "  \"bundles\": [",
                "    { \"name\": \"characters.bundle\", \"FileSize\": 2048 },",
                "    { \"name\": \"ui.bundle\", \"FileSize\": 1024 }",
                "  ],",
                "  \"assets\": [",
                "    { \"name\": \"Hero.png\", \"path\": \"Assets/Textures/Hero.png\", \"Size\": 128, \"bundleNames\": [ \"characters.bundle\", \"ui.bundle\" ] }",
                "  ],",
                "  \"dependencies\": [",
                "    { \"from\": \"Assets/Characters/Hero.prefab\", \"to\": \"Assets/Textures/Hero.png\" }",
                "  ]",
                "}");
            var parser = new BuildReportJsonParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            AssetData asset = result.Report.Assets.Single(item => item.Path == "Assets/Textures/Hero.png");
            Assert.AreEqual(2, asset.BundleCount);
            Assert.AreEqual(1, result.Report.Bundles.Single(bundle => bundle.Name == "characters.bundle").AssetCount);
            Assert.AreEqual("Assets/Characters/Hero.prefab -> Assets/Textures/Hero.png", result.Report.Dependencies.Single());
        }

        private static string WriteTempJson(params string[] lines)
        {
            string path = Path.Combine(Application.temporaryCachePath, Path.GetRandomFileName() + ".json");
            File.WriteAllText(path, string.Join("\n", lines));
            return path;
        }
    }
}
