using System.IO;
using System.Linq;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Parsing;
using NUnit.Framework;
using UnityEngine;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class UnityNativeBuildLayoutJsonParserTests
    {
        [Test]
        public void Parse_ExtractsBundlesAndLocationsFromUnityNativeJson()
        {
            string path = WriteTempJson(
                "[",
                "  {",
                "    \"rid\": 1,",
                "    \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"Name\": \"local_group_assets_all_abc123.bundle\",",
                "      \"FileSize\": 1024,",
                "      \"LoadPath\": \"{UnityEngine.AddressableAssets.Addressables.RuntimePath}/StandaloneWindows64/local_group_assets_all_abc123.bundle\"",
                "    }",
                "  },",
                "  {",
                "    \"rid\": 2,",
                "    \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"Name\": \"remote_group_assets_all_def456.bundle\",",
                "      \"FileSize\": 2048,",
                "      \"LoadPath\": \"https://cdn.example.com/StandaloneWindows64/remote_group_assets_all_def456.bundle\"",
                "    }",
                "  }",
                "]");

            var parser = new UnityNativeBuildLayoutJsonParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(2, result.Report.Bundles.Count);
            Assert.AreEqual(
                BundleLocation.Local,
                result.Report.Bundles.Single(bundle => bundle.Name == "local_group_assets_all_abc123.bundle").Location);
            Assert.AreEqual(
                BundleLocation.Remote,
                result.Report.Bundles.Single(bundle => bundle.Name == "remote_group_assets_all_def456.bundle").Location);
        }

        [Test]
        public void BuildReportParser_SelectsUnityNativeParserForNativeJsonFiles()
        {
            string path = WriteTempJson(
                "{",
                "  \"rid\": 1,",
                "  \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "  \"data\": {",
                "    \"Name\": \"remote_group_assets_all_def456.bundle\",",
                "    \"FileSize\": 10,",
                "    \"LoadPath\": \"https://cdn.example.com/remote_group_assets_all_def456.bundle\"",
                "  }",
                "}");

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
