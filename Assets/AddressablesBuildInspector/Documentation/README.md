# Addressables Build Inspector

Addressables Build Inspector is a Unity Editor-only tool for inspecting Addressables Build Layout reports. It highlights bundle sizes, largest assets, duplicated assets, and estimated duplicate waste.

## Requirements

- Unity 2022.3 or newer
- Addressables Build Layout text report
- No runtime dependencies
- No external packages

## Open the Tool

Use the Unity menu:

`Tools > Addressables Build Inspector`

Click `Load Build Layout` and select a Build Layout text report.

## Main Views

- `Overview`: total build size, bundle count, asset count, duplicate count, estimated duplicate waste, largest bundle, and largest asset.
- `Bundles`: sortable bundle table with name, size, and asset count.
- `Assets`: sortable asset table with name, path, size, and bundle count.
- `Duplicates`: duplicated assets sorted by estimated waste.

## Duplicate Waste Formula

Estimated duplicate waste is calculated as:

`(BundleCount - 1) * AssetSize`

This gives a practical first-pass estimate of avoidable bundle growth caused by assets included in more than one bundle.

## Sample Report

A small sample report is included at:

`Assets/AddressablesBuildInspector/Samples/SampleBuildLayout.txt`

Use it to verify that the window loads, sorts, searches, and highlights duplicate assets.
