# Addressables Build Inspector

Addressables Build Inspector is a Unity Editor-only tool for inspecting Addressables Build Layout reports. It highlights bundle sizes, largest assets, duplicated assets, estimated duplicate waste, dependency paths, build diffs, and duplicate optimization recommendations.

## Requirements

- Unity 2022.3 or newer
- Addressables Build Layout text report or supported JSON report
- No runtime dependencies
- No external packages

## Open the Tool

Use the Unity menu:

`Tools > Addressables Build Inspector`

Click `Load Report` and select a Build Layout text or JSON report.

The window remembers the last successfully loaded report path with `EditorPrefs`. Reopening the window restores that report when the file still exists.

## Main Views

- `Overview`: total build size, bundle count, asset count, duplicate count, estimated duplicate waste, largest bundle, and largest asset.
- `Bundles`: sortable bundle table with name, size, and asset count.
- `Assets`: sortable asset table with name, path, size, and bundle count.
- `Duplicates`: duplicated assets sorted by estimated waste.
- `Optimization`: duplicate cause classification, shared-group recommendations, simulated savings, dependency paths, and CSV/JSON/Markdown export.
- `Dependencies`: search all report assets, inspect dependency trees, and see reverse references.
- `Build Diff`: compare old and new Build Layout reports, review growth reasons, duplicate regression, and export CSV or JSON.

Table headers are clickable sort controls. Headers include a visible `Sort` marker and a tooltip.

## JSON Reports

JSON reports should use this schema:

```json
{
  "bundles": [
    {
      "name": "remote.bundle",
      "FileSize": 2048,
      "assets": [
        { "name": "Hero.png", "path": "Assets/Textures/Hero.png", "Size": 1024 }
      ]
    }
  ],
  "dependencies": [
    { "source": "Assets/Hero.prefab", "target": "Assets/Textures/Hero.png" }
  ]
}
```

Bundle and asset size fields can use `sizeBytes`, `SizeBytes`, `FileSize`, `Size`, or `TotalSize` depending on the report producer.

## Duplicate Waste Formula

Estimated duplicate waste is calculated as:

`(BundleCount - 1) * AssetSize`

This gives a practical first-pass estimate of avoidable bundle growth caused by assets included in more than one bundle.

## Sample Report

A small sample report is included at:

`Assets/AddressablesBuildInspector/Samples/SampleBuildLayout.txt`

Use it to verify that the window loads, sorts, searches, and highlights duplicate assets.

For Build Diff testing, use:

- `Assets/AddressablesBuildInspector/Samples/SampleBuildLayout_Old.txt`
- `Assets/AddressablesBuildInspector/Samples/SampleBuildLayout_New.txt`

## Dependency Explorer

The Dependencies tab uses dependency lines from the Build Layout report when available. If the report does not contain dependency edges, the tool can fall back to `AssetDatabase.GetDependencies` for project assets.

Dependency trees are graph-safe: circular references are marked and traversal stops at the repeated node.

The Dependencies view builds trees only after selecting an asset. Large trees and details are contained in scroll views so scene-sized graphs do not stretch the window.

## Build Diff Analyzer

The Build Diff tab compares two reports and shows:

- old and new build size
- total growth
- bundle and asset count delta
- duplicate waste delta
- top growing bundles
- added, removed, modified, and unchanged items
- ranked smart insights
- CSV and JSON export

## Duplicate Optimization Advisor

The Optimization tab classifies duplicated assets, estimates potential savings, traces dependency paths when dependency data exists, and suggests safe actions such as moving shared textures, materials, prefabs, audio, or ScriptableObjects into a shared group.

Simulation is non-destructive. It does not modify Addressables groups or project assets.
