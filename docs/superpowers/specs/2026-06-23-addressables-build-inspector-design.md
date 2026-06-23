# Addressables Build Inspector Design

Date: 2026-06-23

## Goal

Build an Asset Store-ready Unity Editor-only utility named **Addressables Build Inspector**. The tool loads Unity Addressables Build Layout text reports, analyzes bundle and asset size data, highlights duplicated assets, and presents actionable findings in a dockable UI Toolkit EditorWindow.

## Package Shape

The MVP is an Asset Store style package under `Assets/AddressablesBuildInspector`. Runtime assemblies are not created. All code lives under `Editor` and compiles only in the Unity Editor.

Primary folders:

- `Editor/Models`
- `Editor/Parsing`
- `Editor/Analysis`
- `Editor/UI`
- `Editor/Services`
- `Editor/Utilities`
- `Editor/Windows`
- `Samples`
- `Documentation`

## Architecture

The implementation separates parsing, analysis, services, and UI:

- Models are plain serializable data containers: `BuildReportData`, `BundleData`, `AssetData`, and `DuplicateAssetData`.
- Parsing is behind `IBuildLayoutParser`. The MVP implementation, `BuildLayoutParser`, reads standard Addressables `BuildLayout.txt` style reports and uses tolerant text parsing so minor report variations do not crash the window.
- `BuildAnalysisService` accepts parsed data and exposes size, count, largest item, duplicate, and duplicate waste queries.
- UI services coordinate file picking, parse results, and friendly error messages without coupling parser internals to UI Toolkit controls.
- UI Toolkit views render a dashboard-first tab layout: Overview, Bundles, Assets, and Duplicates.

## Data Flow

1. User opens `Tools/Addressables Build Inspector`.
2. User clicks `Load Build Layout`.
3. The window asks a report loading service to select and parse a text report.
4. Parser returns a `BuildLayoutParseResult` containing either `BuildReportData` or a friendly error.
5. The analysis service computes summary stats and sorted projections.
6. UI refreshes visible tabs using cached lists and filtering helpers.

## Parser Scope

V1 supports text build layout reports. The parser is intentionally extensible, but additional formats such as JSON, CSV, build history, and diff comparison are out of scope for MVP.

The text parser extracts:

- Bundle names
- Bundle sizes
- Asset names
- Asset paths when present
- Asset sizes when present
- Bundle membership per asset
- Dependency-like lines when present, stored as metadata for future use

If a field cannot be found, the parser keeps the report usable with a sensible default rather than failing the entire import.

## UI Design

The selected UI direction is **dashboard-first tabs**:

- Toolbar: `Load Build Layout`, current file label, search field for table tabs.
- Overview: stat cards for total build size, bundle count, asset count, duplicate count, estimated duplicate waste, largest bundle, and largest asset.
- Bundles tab: sortable table with bundle name, size, and asset count.
- Assets tab: sortable table with asset name, path, size, and bundle count.
- Duplicates tab: sortable table ordered by estimated waste descending, with largest offenders visually emphasized.

The UI should remain useful when docked at moderate widths. It can be polished after the functional MVP is working.

## Error Handling

No raw parser exceptions should reach UI code. Errors are converted to friendly messages for:

- Missing or unreadable files
- Empty files
- Unsupported or unrecognized report format
- Parser failures
- Reports that contain no bundles or assets

## Performance

The tool should comfortably handle 500+ bundles and 10,000+ assets. UI lists use `ListView` with item recycling. Analysis results are cached and refreshed only after loading, sorting, or filtering changes.

## Testing Strategy

Use editor tests for logic that does not require opening an EditorWindow:

- Byte size formatting
- Search matching
- Duplicate waste calculations
- Parser behavior on sample text reports
- Analysis service sorting and counts

Compilation is verified by opening or building the Unity project when available. If Unity command-line verification is not available from the environment, use static inspection and any available C# compilation checks.

## Future Extension Points

The design leaves room for:

- Dependency graph view
- Build diff comparison
- Patch impact analyzer
- CSV export
- Bundle history tracking
- Build trend visualization
