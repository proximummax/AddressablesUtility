const fs = require("fs");
const path = require("path");

const MB = 1024 * 1024;

const outputPath = path.join(__dirname, "addressables-build-inspector-marketing-demo.json");
const diffOldOutputPath = path.join(__dirname, "addressables-build-inspector-build-diff-old.json");
const diffNewOutputPath = path.join(__dirname, "addressables-build-inspector-build-diff-new.json");

const duplicateAssets = [
  {
    name: "Environment_Atlas_4K.png",
    path: "Assets/Art/Shared/Textures/Environment_Atlas_4K.png",
    sizeMb: 48,
    bundles: ["city_environment", "mall_environment", "cafe_environment", "park_environment", "beach_environment"]
  },
  {
    name: "Ambience_City_Loop.wav",
    path: "Assets/Audio/Shared/Ambience/Ambience_City_Loop.wav",
    sizeMb: 38,
    bundles: ["city_audio", "mall_audio", "cafe_audio"]
  },
  {
    name: "Shared_Ui_IconAtlas.png",
    path: "Assets/UI/Shared/Shared_Ui_IconAtlas.png",
    sizeMb: 24,
    bundles: ["ui_core", "ui_store", "ui_events", "ui_settings"]
  },
  {
    name: "HeroCharacter_Common.mat",
    path: "Assets/Characters/Shared/Materials/HeroCharacter_Common.mat",
    sizeMb: 18,
    bundles: ["avatar_core", "avatar_customization", "avatar_shop", "avatar_events"]
  },
  {
    name: "CommonShaderVariants.asset",
    path: "Assets/Rendering/CommonShaderVariants.asset",
    sizeMb: 12,
    bundles: ["city_environment", "mall_environment", "avatar_core"]
  }
];

const bundleDefinitions = [
  ["city_environment", 350],
  ["mall_environment", 241],
  ["cafe_environment", 198],
  ["park_environment", 210],
  ["beach_environment", 162],
  ["city_audio", 142],
  ["mall_audio", 118],
  ["cafe_audio", 104],
  ["ui_core", 96],
  ["ui_store", 86],
  ["ui_events", 78],
  ["ui_settings", 64],
  ["avatar_core", 380],
  ["avatar_customization", 236],
  ["avatar_shop", 206],
  ["avatar_events", 172],
  ["tutorial_scenes", 204],
  ["seasonal_content", 240]
];

function bytes(mb) {
  return Math.round(mb * MB);
}

function createUniqueAsset(bundleName, index, sizeMb) {
  const safeName = bundleName.replace(/_/g, "-");
  return {
    name: `${safeName}-content-${index}.asset`,
    path: `Assets/MarketingDemo/${safeName}/content-${index}.asset`,
    sizeBytes: bytes(sizeMb)
  };
}

const bundles = bundleDefinitions.map(([name, sizeMb], bundleIndex) => {
  const assets = [];

  duplicateAssets
    .filter((asset) => asset.bundles.includes(name))
    .forEach((asset) => {
      assets.push({
        name: asset.name,
        path: asset.path,
        sizeBytes: bytes(asset.sizeMb)
      });
    });

  for (let index = 1; index <= 7; index += 1) {
    const size = 8 + ((bundleIndex + 1) * (index + 2)) % 29;
    assets.push(createUniqueAsset(name, index, size));
  }

  return {
    name: `${name}.bundle`,
    sizeBytes: bytes(sizeMb),
    assets
  };
});

const dependencies = [];
for (const duplicate of duplicateAssets) {
  for (const bundleName of duplicate.bundles) {
    dependencies.push({
      source: `Assets/MarketingDemo/${bundleName.replace(/_/g, "-")}/content-1.asset`,
      target: duplicate.path
    });
  }
}

dependencies.push(
  {
    source: "Assets/MarketingDemo/avatar-core/content-2.asset",
    target: "Assets/Rendering/CommonShaderVariants.asset"
  },
  {
    source: "Assets/MarketingDemo/ui-store/content-3.asset",
    target: "Assets/UI/Shared/Shared_Ui_IconAtlas.png"
  },
  {
    source: "Assets/MarketingDemo/city-environment/content-4.asset",
    target: "Assets/Art/Shared/Textures/Environment_Atlas_4K.png"
  }
);

const report = {
  metadata: {
    name: "Addressables Build Inspector Marketing Demo",
    purpose: "Synthetic report for Unity Asset Store screenshots and short product videos.",
    expectedKpis: {
      buildSize: "3.21 GB",
      duplicateWaste: "418 MB",
      potentialSavings: "418 MB",
      healthScore: "72 / 100"
    }
  },
  bundles,
  dependencies
};

fs.writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
console.log(`Wrote ${outputPath}`);

const diffOldBundles = createDiffBundles([
  ["city_environment", 286],
  ["mall_environment", 230],
  ["cafe_environment", 178],
  ["city_audio", 96],
  ["ui_core", 84],
  ["avatar_core", 268],
  ["avatar_customization", 184],
  ["tutorial_scenes", 132],
  ["shared_assets", 118]
], [
  {
    name: "Environment_Atlas_4K.png",
    path: "Assets/Art/Shared/Textures/Environment_Atlas_4K.png",
    sizeMb: 42,
    bundles: ["shared_assets"]
  },
  {
    name: "Shared_Ui_IconAtlas.png",
    path: "Assets/UI/Shared/Shared_Ui_IconAtlas.png",
    sizeMb: 18,
    bundles: ["ui_core"]
  },
  {
    name: "HeroCharacter_Common.mat",
    path: "Assets/Characters/Shared/Materials/HeroCharacter_Common.mat",
    sizeMb: 14,
    bundles: ["avatar_core"]
  }
], "Old");

const diffNewBundles = createDiffBundles([
  ["city_environment", 338],
  ["mall_environment", 276],
  ["cafe_environment", 196],
  ["city_audio", 126],
  ["ui_core", 108],
  ["avatar_core", 312],
  ["avatar_customization", 224],
  ["tutorial_scenes", 150],
  ["shared_assets", 122],
  ["seasonal_event_2026", 244],
  ["cinematic_intro", 188],
  ["store_promo_assets", 132]
], [
  {
    name: "Environment_Atlas_4K.png",
    path: "Assets/Art/Shared/Textures/Environment_Atlas_4K.png",
    sizeMb: 48,
    bundles: ["shared_assets", "city_environment", "mall_environment", "seasonal_event_2026"]
  },
  {
    name: "Shared_Ui_IconAtlas.png",
    path: "Assets/UI/Shared/Shared_Ui_IconAtlas.png",
    sizeMb: 24,
    bundles: ["ui_core", "store_promo_assets", "seasonal_event_2026"]
  },
  {
    name: "HeroCharacter_Common.mat",
    path: "Assets/Characters/Shared/Materials/HeroCharacter_Common.mat",
    sizeMb: 18,
    bundles: ["avatar_core", "avatar_customization", "seasonal_event_2026"]
  },
  {
    name: "Seasonal_Cinematic_Intro.mov",
    path: "Assets/Cinematics/Seasonal/Seasonal_Cinematic_Intro.mov",
    sizeMb: 74,
    bundles: ["cinematic_intro"]
  },
  {
    name: "WinterEvent_Music_Loop.wav",
    path: "Assets/Audio/Events/WinterEvent_Music_Loop.wav",
    sizeMb: 42,
    bundles: ["seasonal_event_2026", "city_audio"]
  },
  {
    name: "StorePromo_Background_4K.png",
    path: "Assets/UI/Store/StorePromo_Background_4K.png",
    sizeMb: 36,
    bundles: ["store_promo_assets"]
  }
], "New");

const diffOldReport = createReport(
  "Addressables Build Inspector Build Diff Demo - Old Build",
  "Synthetic old build report for Unity Asset Store Build Diff screenshots.",
  diffOldBundles);

const diffNewReport = createReport(
  "Addressables Build Inspector Build Diff Demo - New Build",
  "Synthetic new build report for Unity Asset Store Build Diff screenshots.",
  diffNewBundles);

fs.writeFileSync(diffOldOutputPath, `${JSON.stringify(diffOldReport, null, 2)}\n`, "utf8");
fs.writeFileSync(diffNewOutputPath, `${JSON.stringify(diffNewReport, null, 2)}\n`, "utf8");
console.log(`Wrote ${diffOldOutputPath}`);
console.log(`Wrote ${diffNewOutputPath}`);

function createDiffBundles(definitions, featuredAssets, label) {
  return definitions.map(([name, sizeMb], bundleIndex) => {
    const assets = [];

    featuredAssets
      .filter((asset) => asset.bundles.includes(name))
      .forEach((asset) => {
        assets.push({
          name: asset.name,
          path: asset.path,
          sizeBytes: bytes(asset.sizeMb)
        });
      });

    for (let index = 1; index <= 6; index += 1) {
      const size = 6 + ((bundleIndex + 3) * (index + 5)) % 24;
      assets.push({
        name: `${label}_${name}_asset_${index}.asset`,
        path: `Assets/MarketingDiff/${label}/${name}/asset_${index}.asset`,
        sizeBytes: bytes(size)
      });
    }

    return {
      name: `${name}.bundle`,
      sizeBytes: bytes(sizeMb),
      assets
    };
  });
}

function createReport(name, purpose, reportBundles) {
  const reportDependencies = [];
  for (const bundle of reportBundles) {
    const source = bundle.assets.find((asset) => asset.path.includes("/asset_1.asset"));
    if (!source) {
      continue;
    }

    for (const target of bundle.assets.filter((asset) => !asset.path.includes("/asset_"))) {
      reportDependencies.push({
        source: source.path,
        target: target.path
      });
    }
  }

  return {
    metadata: {
      name,
      purpose
    },
    bundles: reportBundles,
    dependencies: reportDependencies
  };
}
