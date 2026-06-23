using System.IO;
using System.Linq;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Parsing;
using NUnit.Framework;
using UnityEngine;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class BuildLayoutParserTests
    {
        [Test]
        public void Parse_ExtractsBundlesAssetsSizesAndDuplicateMembership()
        {
            string path = WriteTempReport(
                "Addressables Build Layout Report",
                "Bundle: characters.bundle",
                "Size: 1.00 MB",
                "Assets:",
                "  Assets/Characters/Hero.prefab | Size: 512 KB",
                "  Assets/Shared/Icon.png | Size: 64 KB",
                "Bundle: environment.bundle",
                "Size: 2.00 MB",
                "Assets:",
                "  Assets/Scenes/Forest.unity | Size: 1.50 MB",
                "  Assets/Shared/Icon.png | Size: 64 KB");

            var parser = new BuildLayoutParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(2, result.Report.Bundles.Count);
            Assert.AreEqual(3, result.Report.Assets.Count);
            Assert.AreEqual(1048576, result.Report.Bundles[0].SizeBytes);
            AssetData icon = result.Report.Assets.Single(asset => asset.Path == "Assets/Shared/Icon.png");
            Assert.AreEqual(2, icon.BundleNames.Count);
            Assert.AreEqual(65536, icon.SizeBytes);
        }

        [Test]
        public void Parse_ReturnsFailureForEmptyFile()
        {
            string path = WriteTempReport(string.Empty);
            var parser = new BuildLayoutParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("empty", result.ErrorMessage.ToLowerInvariant());
        }

        [Test]
        public void Parse_ReturnsFailureForUnrecognizedReport()
        {
            string path = WriteTempReport("This is not a build layout report.");
            var parser = new BuildLayoutParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("recognized", result.ErrorMessage.ToLowerInvariant());
        }

        private static string WriteTempReport(params string[] lines)
        {
            string path = Path.Combine(Application.temporaryCachePath, Path.GetRandomFileName() + ".txt");
            File.WriteAllText(path, string.Join("\n", lines));
            return path;
        }
    }
}
