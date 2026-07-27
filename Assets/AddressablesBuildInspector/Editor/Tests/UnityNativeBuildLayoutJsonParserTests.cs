using System.IO;
using System.Linq;
using AddressablesBuildInspector.Editor.Analysis;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Parsing;
using NUnit.Framework;
using UnityEngine;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class UnityNativeBuildLayoutJsonParserTests
    {
        [Test]
        public void Parse_ExtractsBundlesAndSizesFromUnityNativeJson()
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
            Assert.AreEqual(1024, result.Report.Bundles.Single(bundle => bundle.Name == "local_group_assets_all_abc123.bundle").SizeBytes);
            Assert.AreEqual(2048, result.Report.Bundles.Single(bundle => bundle.Name == "remote_group_assets_all_def456.bundle").SizeBytes);
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
            Assert.AreEqual(10, result.Report.Bundles[0].SizeBytes);
        }

        [Test]
        public void BuildReportParser_OpensUnityNativeJsonFastAndLoadsDependenciesOnDemand()
        {
            string path = WriteTempJson(
                "[",
                "  {",
                "    \"rid\": 10,",
                "    \"type\": { \"class\": \"BuildLayout/ExplicitAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"AssetPath\": \"Assets/Textures/Hero.png\",",
                "      \"TotalSize\": 128",
                "    }",
                "  },",
                "  {",
                "    \"rid\": 11,",
                "    \"type\": { \"class\": \"BuildLayout/ExplicitAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"AssetPath\": \"Assets/Characters/Hero.prefab\",",
                "      \"TotalSize\": 256,",
                "      \"Dependencies\": [ { \"rid\": 10 } ]",
                "    }",
                "  },",
                "  {",
                "    \"rid\": 20,",
                "    \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"Name\": \"characters.bundle\",",
                "      \"FileSize\": 2048,",
                "      \"Files\": [ { \"Assets\": [ { \"rid\": 10 }, { \"rid\": 11 } ] } ]",
                "    }",
                "  }",
                "]");

            var parser = new BuildReportParser();

            BuildLayoutParseResult fastResult = parser.Parse(path);
            BuildLayoutParseResult fullResult = parser.ParseWithDependencies(path);

            Assert.IsTrue(fastResult.Success, fastResult.ErrorMessage);
            Assert.IsFalse(fastResult.Report.DependenciesLoaded);
            Assert.AreEqual(0, fastResult.Report.Dependencies.Count);
            Assert.AreEqual(2, fastResult.Report.Assets.Count);
            Assert.AreEqual(2, fastResult.Report.Bundles.Single().AssetCount);
            Assert.IsTrue(fullResult.Success, fullResult.ErrorMessage);
            Assert.IsTrue(fullResult.Report.DependenciesLoaded);
            Assert.AreEqual("Assets/Characters/Hero.prefab -> Assets/Textures/Hero.png", fullResult.Report.Dependencies.Single());
        }

        [Test]
        public void LooksLikeUnityNativeBuildLayout_DetectsRootObjectWithLargeMetadataHeader()
        {
            string padding = new string('x', 60000);
            string path = WriteTempJson(
                "{",
                "  \"BuildTarget\": 9,",
                "  \"PackageVersion\": \"com.unity.addressables: 1.28.1\",",
                "  \"Padding\": \"" + padding + "\",",
                "  \"BuiltInBundles\": [],",
                "  \"references\": {",
                "    \"version\": 2,",
                "    \"RefIds\": [",
                "      {",
                "        \"rid\": 1,",
                "        \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "        \"data\": {",
                "          \"Name\": \"group_assets_all_abc123.bundle\",",
                "          \"FileSize\": 512,",
                "          \"LoadPath\": \"{UnityEngine.AddressableAssets.Addressables.RuntimePath}/group_assets_all_abc123.bundle\"",
                "        }",
                "      }",
                "    ]",
                "  }",
                "}");

            Assert.IsTrue(UnityNativeBuildLayoutJsonParser.LooksLikeUnityNativeBuildLayout(path));

            var parser = new BuildReportParser();
            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(1, result.Report.Bundles.Count);
        }

        [Test]
        public void Parse_ExtractsExplicitAssetsFromUnityNativeJson()
        {
            string path = WriteTempJson(
                "[",
                "  {",
                "    \"rid\": 1,",
                "    \"type\": { \"class\": \"BuildLayout/ExplicitAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"AssetPath\": \"Assets/Content/Hero.prefab\",",
                "      \"Size\": 128",
                "    }",
                "  }",
                "]");

            var parser = new UnityNativeBuildLayoutJsonParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(1, result.Report.Assets.Count);
            Assert.AreEqual("Assets/Content/Hero.prefab", result.Report.Assets[0].Path);
        }

        [Test]
        public void Parse_ReconstructsBundleMembershipsAndDependenciesFromNativeReferences()
        {
            string path = WriteTempJson(
                "[",
                "  {",
                "    \"rid\": 10,",
                "    \"type\": { \"class\": \"BuildLayout/ExplicitAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"AssetPath\": \"Assets/Textures/Hero.png\",",
                "      \"TotalSize\": 128",
                "    }",
                "  },",
                "  {",
                "    \"rid\": 11,",
                "    \"type\": { \"class\": \"BuildLayout/ExplicitAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"AssetPath\": \"Assets/Characters/Hero.prefab\",",
                "      \"TotalSize\": 256,",
                "      \"Dependencies\": [ { \"rid\": 10 } ]",
                "    }",
                "  },",
                "  {",
                "    \"rid\": 20,",
                "    \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"Name\": \"characters.bundle\",",
                "      \"FileSize\": 2048,",
                "      \"Files\": [ { \"Assets\": [ { \"rid\": 10 }, { \"rid\": 11 } ] } ]",
                "    }",
                "  },",
                "  {",
                "    \"rid\": 30,",
                "    \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" },",
                "    \"data\": {",
                "      \"Name\": \"ui.bundle\",",
                "      \"FileSize\": 1024,",
                "      \"Files\": [ { \"Assets\": [ { \"rid\": 10 } ] } ]",
                "    }",
                "  }",
                "]");

            var parser = new UnityNativeBuildLayoutJsonParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            AssetData texture = result.Report.Assets.Single(asset => asset.Path == "Assets/Textures/Hero.png");
            Assert.AreEqual(128, texture.SizeBytes);
            Assert.AreEqual(2, texture.BundleCount);
            Assert.Contains("characters.bundle", texture.BundleNames);
            Assert.Contains("ui.bundle", texture.BundleNames);
            Assert.IsTrue(result.Report.Bundles.Single(bundle => bundle.Name == "characters.bundle").Assets.Any(asset => asset.Path == "Assets/Characters/Hero.prefab"));
            Assert.AreEqual("Assets/Characters/Hero.prefab -> Assets/Textures/Hero.png", result.Report.Dependencies.Single());
        }

        [Test]
        public void Parse_ReadsOtherAssetsAndIncludesStreamedSizeInDuplicateWaste()
        {
            string path = WriteTempJson(
                "{",
                "  \"BuildTarget\": 9,",
                "  \"PackageVersion\": \"com.unity.addressables: 1.28.1\",",
                "  \"DuplicatedAssets\": [",
                "    { \"AssetGuid\": \"texture-guid\", \"DuplicatedObjects\": [ { \"IncludedInBundleFiles\": [ { \"rid\": 20 }, { \"rid\": 21 } ] } ] }",
                "  ],",
                "  \"Groups\": [ { \"rid\": 1 } ],",
                "  \"references\": {",
                "    \"version\": 2,",
                "    \"RefIds\": [",
                "      { \"rid\": 10, \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"Name\": \"sewing_assets.bundle\",",
                "        \"FileSize\": 5000000,",
                "        \"Files\": [ { \"rid\": 20 } ]",
                "      } },",
                "      { \"rid\": 11, \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"Name\": \"sewing_scenes.bundle\",",
                "        \"FileSize\": 4000000,",
                "        \"Files\": [ { \"rid\": 21 } ]",
                "      } },",
                "      { \"rid\": 20, \"type\": { \"class\": \"BuildLayout/File\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"Name\": \"archive:/sewing_assets\",",
                "        \"Bundle\": { \"rid\": 10 },",
                "        \"Assets\": [ { \"rid\": 30 } ],",
                "        \"OtherAssets\": [ { \"rid\": 40 } ]",
                "      } },",
                "      { \"rid\": 21, \"type\": { \"class\": \"BuildLayout/File\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"Name\": \"archive:/sewing_scenes\",",
                "        \"Bundle\": { \"rid\": 11 },",
                "        \"Assets\": [ { \"rid\": 31 } ],",
                "        \"OtherAssets\": [ { \"rid\": 41 } ]",
                "      } },",
                "      { \"rid\": 30, \"type\": { \"class\": \"BuildLayout/ExplicitAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"AssetPath\": \"Assets/Sewing/SceneRoot.prefab\",",
                "        \"SerializedSize\": 100,",
                "        \"StreamedSize\": 0,",
                "        \"File\": { \"rid\": 20 },",
                "        \"Bundle\": { \"rid\": 10 },",
                "        \"InternalReferencedOtherAssets\": [ { \"rid\": 40 } ]",
                "      } },",
                "      { \"rid\": 31, \"type\": { \"class\": \"BuildLayout/ExplicitAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"AssetPath\": \"Assets/Sewing/Scene.unity\",",
                "        \"SerializedSize\": 200,",
                "        \"StreamedSize\": 0,",
                "        \"File\": { \"rid\": 21 },",
                "        \"Bundle\": { \"rid\": 11 },",
                "        \"InternalReferencedOtherAssets\": [ { \"rid\": 41 } ]",
                "      } },",
                "      { \"rid\": 40, \"type\": { \"class\": \"BuildLayout/DataFromOtherAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"AssetGuid\": \"texture-guid\",",
                "        \"AssetPath\": \"Assets/Sewing/selection_card.png\",",
                "        \"SerializedSize\": 17928,",
                "        \"StreamedSize\": 1324000,",
                "        \"File\": { \"rid\": 20 },",
                "        \"Objects\": [ { \"LocalIdentifierInFile\": 2800000, \"SerializedSize\": 228, \"StreamedSize\": 1324000 } ]",
                "      } },",
                "      { \"rid\": 41, \"type\": { \"class\": \"BuildLayout/DataFromOtherAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"AssetGuid\": \"texture-guid\",",
                "        \"AssetPath\": \"Assets/Sewing/selection_card.png\",",
                "        \"SerializedSize\": 17916,",
                "        \"StreamedSize\": 1324000,",
                "        \"File\": { \"rid\": 21 },",
                "        \"Objects\": [ { \"LocalIdentifierInFile\": 2800000, \"SerializedSize\": 216, \"StreamedSize\": 1324000 } ]",
                "      } }",
                "    ]",
                "  }",
                "}");

            BuildLayoutParseResult result = UnityNativeBuildLayoutJsonParser.ParseViaTextExtractionOnly(path, false);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            AssetData texture = result.Report.Assets.Single(asset => asset.Path == "Assets/Sewing/selection_card.png");
            Assert.AreEqual(1341928, texture.SizeBytes);
            Assert.AreEqual(2, texture.BundleCount);
            Assert.Contains("sewing_assets.bundle", texture.BundleNames);
            Assert.Contains("sewing_scenes.bundle", texture.BundleNames);

            var analysis = new BuildAnalysisService(result.Report);
            Assert.AreEqual(1, analysis.GetDuplicateAssets().Count);
            Assert.AreEqual(1341928, analysis.CalculateDuplicateWaste());
        }

        [Test]
        public void Parse_ReconstructsUnity2022FileAssetLinksAndAssetSizes()
        {
            string path = WriteTempJson(
                "{",
                "  \"BuildTarget\": 9,",
                "  \"PackageVersion\": \"com.unity.addressables: 1.28.1\",",
                "  \"DuplicatedAssets\": [",
                "    { \"AssetGuid\": \"duplicate-only-guid\", \"DuplicatedObjects\": [ { \"IncludedInBundleFiles\": [ { \"rid\": 20 }, { \"rid\": 21 } ] } ] }",
                "  ],",
                "  \"Groups\": [ { \"rid\": 1 } ],",
                "  \"references\": {",
                "    \"version\": 2,",
                "    \"RefIds\": [",
                "      { \"rid\": 10, \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"Name\": \"characters.bundle\",",
                "        \"FileSize\": 2048,",
                "        \"Files\": [ { \"rid\": 20 } ],",
                "        \"BundleDependencies\": [ { \"AssetDependencies\": [ { \"rootAsset\": { \"rid\": 30 }, \"dependencyAsset\": { \"rid\": 40 } } ] } ]",
                "      } },",
                "      { \"rid\": 11, \"type\": { \"class\": \"BuildLayout/Bundle\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"Name\": \"ui.bundle\",",
                "        \"FileSize\": 1024,",
                "        \"Files\": [ { \"rid\": 21 } ]",
                "      } },",
                "      { \"rid\": 20, \"type\": { \"class\": \"BuildLayout/File\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"Name\": \"archive:/characters\",",
                "        \"Bundle\": { \"rid\": 10 },",
                "        \"Assets\": [ { \"rid\": 30 }, { \"rid\": 40 }, { \"rid\": 42 } ]",
                "      } },",
                "      { \"rid\": 21, \"type\": { \"class\": \"BuildLayout/File\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"Name\": \"archive:/ui\",",
                "        \"Bundle\": { \"rid\": 11 },",
                "        \"Assets\": [ { \"rid\": 41 } ]",
                "      } },",
                "      { \"rid\": 30, \"type\": { \"class\": \"BuildLayout/ExplicitAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"AssetPath\": \"Assets/Characters/Hero.prefab\",",
                "        \"SerializedSize\": 64,",
                "        \"StreamedSize\": 8,",
                "        \"File\": { \"rid\": 20 },",
                "        \"Bundle\": { \"rid\": 10 },",
                "        \"InternalReferencedOtherAssets\": [ { \"rid\": 40 } ],",
                "        \"ExternallyReferencedAssets\": [ { \"rid\": 41 } ]",
                "      } },",
                "      { \"rid\": 40, \"type\": { \"class\": \"BuildLayout/DataFromOtherAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"AssetPath\": \"Assets/Shared/HeroTexture.png\",",
                "        \"SerializedSize\": 20,",
                "        \"StreamedSize\": 4,",
                "        \"File\": { \"rid\": 20 }",
                "      } },",
                "      { \"rid\": 41, \"type\": { \"class\": \"BuildLayout/DataFromOtherAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"AssetPath\": \"Assets/Shared/HeroTexture.png\",",
                "        \"SerializedSize\": 30,",
                "        \"StreamedSize\": 5,",
                "        \"File\": { \"rid\": 21 }",
                "      } },",
                "      { \"rid\": 42, \"type\": { \"class\": \"BuildLayout/DataFromOtherAsset\", \"ns\": \"UnityEditor.AddressableAssets.Build.Layout\", \"asm\": \"Unity.Addressables.Editor\" }, \"data\": {",
                "        \"AssetGuid\": \"duplicate-only-guid\",",
                "        \"AssetPath\": \"Assets/Shared/DuplicateOnly.asset\",",
                "        \"SerializedSize\": 12,",
                "        \"StreamedSize\": 3,",
                "        \"File\": { \"rid\": 20 }",
                "      } }",
                "    ]",
                "  }",
                "}");

            var parser = new UnityNativeBuildLayoutJsonParser();

            BuildLayoutParseResult result = parser.Parse(path);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            AssetData hero = result.Report.Assets.Single(asset => asset.Path == "Assets/Characters/Hero.prefab");
            Assert.AreEqual(72, hero.SizeBytes);
            Assert.AreEqual("characters.bundle", hero.BundleNames.Single());
            AssetData shared = result.Report.Assets.Single(asset => asset.Path == "Assets/Shared/HeroTexture.png");
            Assert.AreEqual(35, shared.SizeBytes);
            Assert.AreEqual(2, shared.BundleCount);
            Assert.Contains("characters.bundle", shared.BundleNames);
            Assert.Contains("ui.bundle", shared.BundleNames);
            Assert.AreEqual(2, result.Report.Bundles.Single(bundle => bundle.Name == "characters.bundle").AssetCount);
            Assert.IsTrue(result.Report.Dependencies.Contains("Assets/Characters/Hero.prefab -> Assets/Shared/HeroTexture.png"));
            AssetData duplicateOnly = result.Report.Assets.Single(asset => asset.Path == "Assets/Shared/DuplicateOnly.asset");
            Assert.AreEqual(15, duplicateOnly.SizeBytes);
            Assert.AreEqual(2, duplicateOnly.BundleCount);
            Assert.Contains("characters.bundle", duplicateOnly.BundleNames);
            Assert.Contains("ui.bundle", duplicateOnly.BundleNames);
        }

        private static string WriteTempJson(params string[] lines)
        {
            string path = Path.Combine(Application.temporaryCachePath, Path.GetRandomFileName() + ".json");
            File.WriteAllText(path, string.Join("\n", lines));
            return path;
        }
    }
}
