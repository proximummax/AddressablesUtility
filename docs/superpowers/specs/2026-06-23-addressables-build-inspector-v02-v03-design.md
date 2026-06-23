# Addressables Build Inspector v0.2-v0.3 Design

Date: 2026-06-23

## Scope

Implement two sequential versions:

1. Version 0.2 Dependency Explorer
2. Version 0.3 Build Diff Analyzer

Both features are added as isolated subsystems under `Editor/Dependencies` and `Editor/Diff`. Existing bundle, asset, duplicate, parser, and overview behavior remains compatible.

## Version 0.2 Dependency Explorer

Dependency logic is built around `IDependencyProvider`, `DependencyGraphService`, `DependencyGraphCache`, `DependencySearchService`, and `DependencyTreeBuilder`.

The primary dependency source is parsed build layout dependency text. If no build layout dependency data is available for an asset, the UI/service can use `AssetDatabaseDependencyProvider`, which wraps `AssetDatabase.GetDependencies`. Dependency tree construction uses visited-path tracking and depth limits to prevent circular references and infinite recursion.

The UI adds a `Dependencies` tab with search results, an expandable dependency tree, and a detail panel showing selected node metadata, referenced-by data, bundle count, and duplicate waste.

## Version 0.3 Build Diff Analyzer

Diff logic is isolated under `Editor/Diff`. `BuildDiffService` parses two build reports and delegates to `BuildDiffAnalyzer`. The analyzer compares bundles by bundle name and assets by normalized asset path using dictionaries, avoiding O(n^2) scans.

The diff report includes bundle diffs, asset diffs, growth reasons, duplicate regression, and ranked insights. Export is provided through `DiffExportService` with CSV and JSON output, leaving room for future PDF export.

The UI adds a `Build Diff` tab with old/new report selection, compare action, overview metrics, insights, bundle diff table, asset diff table, growth details, and export buttons.

## Constraints

- Editor-only package
- UI Toolkit only
- No external dependencies
- XML docs for public types and members
- Unit-testable service logic
- No static global state

## Verification

Use focused EditMode tests for dependency services and diff services. Because Unity batchmode is currently blocked by Unity Licensing IPC in this environment, also run a compile-oriented `dotnet build` against Unity 2022.3 assemblies and a standalone validation harness for pure service logic.
