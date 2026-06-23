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
        public void BuildReportJsonParser_ParsesBundlesAssetsDependenciesAndLocations()
        {
            string path = WriteTempJson(
                "{",
                "  \"bundles\": [",
                "    { \"name\": \"remote.bundle\", \"sizeBytes\": 2048, \"location\": \"Remote\", \"assets\": [",
                "      { \"name\": \"Hero.png\", \"path\": \"Assets/Textures/Hero.png\", \"sizeBytes\": 1024 },",
                "      { \"name\": \"Shared.mat\", \"path\": \"Assets/Materials/Shared.mat\", \"sizeBytes\": 512 }",
                "    ] },",
                "    { \"name\": \"local.bundle\", \"sizeBytes\": 1024, \"location\": \"Local\", \"assets\": [",
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
            Assert.AreEqual(BundleLocation.Remote, result.Report.Bundles.Single(bundle => bundle.Name == "remote.bundle").Location);
            Assert.AreEqual(BundleLocation.Local, result.Report.Bundles.Single(bundle => bundle.Name == "local.bundle").Location);
            Assert.AreEqual(3, result.Report.Assets.Count);
            Assert.AreEqual(2, result.Report.Assets.Single(asset => asset.Path == "Assets/Materials/Shared.mat").BundleCount);
            Assert.AreEqual("Assets/UI/Menu.uxml -> Assets/Materials/Shared.mat", result.Report.Dependencies.Single());
        }

        [Test]
        public void BuildReportParser_SelectsJsonParserForJsonFiles()
        {
            string path = WriteTempJson("{ \"bundles\": [ { \"name\": \"remote.bundle\", \"sizeBytes\": 10, \"location\": \"Remote\" } ] }");
            var parser = new BuildReportParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(BundleLocation.Remote, result.Report.Bundles[0].Location);
        }

        private static string WriteTempJson(params string[] lines)
        {
            string path = Path.Combine(Application.temporaryCachePath, Path.GetRandomFileName() + ".json");
            File.WriteAllText(path, string.Join("\n", lines));
            return path;
        }
    }
}
