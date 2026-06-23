# Architecture

Addressables Build Inspector separates parsing, analysis, services, utilities, and UI.

## Models

Models live in `Editor/Models`:

- `BuildReportData`: parsed report root.
- `BundleData`: bundle name, size, and contained assets.
- `AssetData`: asset name, path, size, and bundle membership.
- `DuplicateAssetData`: duplicate analysis result.
- `BuildOverviewData`: dashboard summary.

## Parsing

Parsing lives in `Editor/Parsing`.

`IBuildLayoutParser` defines the extension point. `BuildLayoutParser` is the MVP text parser. It is tolerant by design: it recognizes common bundle lines, size lines, and asset path lines without requiring the UI to know the report format.

Expected parser behavior:

- Return `BuildLayoutParseResult.Succeeded(report)` for usable reports.
- Return `BuildLayoutParseResult.Failed(message)` for file or format problems.
- Do not let expected parser errors escape into UI code as exceptions.

## Analysis

`BuildAnalysisService` lives in `Editor/Analysis`.

It computes:

- Largest bundles
- Largest assets
- Duplicate assets
- Duplicate waste
- Total build size
- Bundle count
- Asset count
- Overview summary

The service does not mutate the parsed report.

## UI

The UI lives in `Editor/UI` and `Editor/Windows`.

`AddressablesBuildInspectorWindow` loads the UXML and USS assets, wires the toolbar, creates the tab buttons, and renders recycled `ListView` tables. It calls the parser through `BuildLayoutReportLoader` and calls analysis through `BuildAnalysisService`.

The selected layout is dashboard-first:

1. Overview
2. Bundles
3. Assets
4. Duplicates

## Utilities

Utilities live in `Editor/Utilities`:

- `ByteFormatter`: consistent size formatting.
- `SearchUtility`: null-safe case-insensitive search.
- `TableBuilder`: reusable UI Toolkit table fragments.
