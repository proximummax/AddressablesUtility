# Architecture

Addressables Build Inspector separates parsing, analysis, services, utilities, and UI.

## Models

Models live in `Editor/Models`:

- `BuildReportData`: parsed report root.
- `BundleData`: bundle name, size, inferred local/remote location, and contained assets.
- `AssetData`: asset name, path, size, and bundle membership.
- `DuplicateAssetData`: duplicate analysis result.
- `BuildOverviewData`: dashboard summary.

## Parsing

Parsing lives in `Editor/Parsing`.

`IBuildLayoutParser` defines the extension point. `BuildReportParser` is the composite parser used by the UI. It selects `BuildReportJsonParser` for JSON files and falls back to `BuildLayoutParser` for text Build Layout reports.

`BuildLayoutParser` is tolerant by design: it recognizes common bundle lines, size lines, local/remote load path lines, dependency lines, and asset path lines without requiring the UI to know the report format.

Expected parser behavior:

- Return `BuildLayoutParseResult.Succeeded(report)` for usable reports.
- Return `BuildLayoutParseResult.Failed(message)` for file or format problems.
- Do not let expected parser errors escape into UI code as exceptions.
- Preserve source path and load time so the window can restore the last report.

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
5. Optimization
6. Dependencies
7. Build Diff

Large or derived data is generated lazily. The window does not build dependency trees or optimization reports during initial report load.

## Utilities

Utilities live in `Editor/Utilities`:

- `ByteFormatter`: consistent size formatting.
- `SearchUtility`: null-safe case-insensitive search.
- `TableBuilder`: reusable UI Toolkit table fragments.

## Dependencies

Dependency Explorer lives in `Editor/Dependencies`.

- `IDependencyProvider`: abstraction for dependency sources.
- `BuildLayoutDependencyProvider`: reads dependency edges captured from Build Layout text.
- `AssetDatabaseDependencyProvider`: fallback wrapper around `AssetDatabase.GetDependencies`.
- `DependencyGraphService`: dependency, reverse dependency, shared asset, most-referenced, and chain-depth queries.
- `DependencyGraphCache`: avoids repeated dependency and reverse lookup work.
- `DependencyTreeBuilder`: builds graph-safe trees with circular reference protection.
- `DependencySearchService`: searches by asset name and path.

The UI creates dependency trees only when a user selects an asset. Tree and details panels are scrollable to keep large scene or prefab graphs contained.

`DependencyGraphService` builds a reverse dependency index lazily so repeated `GetReferencedBy` calls do not rescan the full report.

## Diff

Build Diff Analyzer lives in `Editor/Diff`.

- `BuildDiffAnalyzer`: compares parsed reports and generates bundle diffs, asset diffs, growth reasons, duplicate regressions, and insights.
- `BuildDiffService`: loads old/new report files and delegates comparison.
- `DiffExportService`: writes CSV and JSON reports.
- `BuildDiffReport`, `BundleDiff`, `AssetDiff`, `GrowthReason`, and `DiffInsight`: serializable comparison models.

The analyzer uses dictionary lookups by bundle name and asset path so it scales to large reports.

## Optimization

Duplicate Optimization Advisor lives in `Editor/Optimization`.

- `DuplicateOptimizationService`: facade for analysis and simulation.
- `OptimizationAnalyzer`: creates candidates, cause classifications, recommendations, impact estimates, simulations, shared-group recommendations, and insights.
- `DuplicateCauseAnalyzer`: classifies duplicated assets by path/type.
- `DependencyPathTracer`: traces dependency paths through `DependencyGraphService`.
- `SharedGroupAdvisor`: selects the strongest shared group candidates.
- `OptimizationInsightGenerator`: produces ranked findings.
- `OptimizationReportExportService`: writes CSV, JSON, and Markdown reports.

The subsystem is non-destructive. It does not edit Addressables groups, move assets, or write project settings.
