# Extension Points

## Add Another Report Parser

Implement `IBuildLayoutParser` in `Editor/Parsing`.

The parser should return `BuildLayoutParseResult` and populate `BuildReportData`. UI code should not need to change if the new parser produces the same model. Add the parser to `BuildReportParser` when the decision can be made from extension or file content.

Possible next parsers:

- Strict parser for a known Addressables package version
- CSV parser
- Build cache parser

## Add CSV Export

Add an editor service that reads the current `BuildReportData` and `BuildAnalysisService` projections. Keep export code outside `AddressablesBuildInspectorWindow` so the UI remains a thin coordinator.

Recommended output files:

- Bundles CSV
- Assets CSV
- Duplicates CSV

## Add Build Diff Comparison

Add a second loaded `BuildReportData` and compare analysis summaries by asset path and bundle name.

Recommended comparison keys:

- Bundle name for bundle growth
- Asset path for asset growth
- Asset path plus bundle name for membership changes

## Add Dependency Graph View

Use the existing `BuildReportData.Dependencies` list as a starting point, then extend the parser to capture structured dependency relationships.

Keep graph data in a separate model so table analysis does not become coupled to graph rendering.

The current Dependency Explorer already isolates dependency logic behind `IDependencyProvider`. A future graph visualization can reuse `DependencyGraphService` and render the same `DependencyNode` tree with a graph canvas.

## Add Patch Impact Analyzer

Build Diff Analyzer already reports bundle and asset deltas plus growth reasons. A patch impact analyzer should build on `BuildDiffReport` and add platform-specific patch cost rules in a separate service.

## Add Optimization Rules

Duplicate Optimization Advisor is isolated in `Editor/Optimization`. Add new rules by extending:

- `DuplicateCauseAnalyzer` for asset-type or group-pattern classification.
- `OptimizationAnalyzer` for severity, recommendation, and impact estimates.
- `SharedGroupAdvisor` for grouping strategy.
- `OptimizationReportExportService` for additional report formats.

Keep rules non-destructive unless a future workflow explicitly adds a user-reviewed apply step.

## Add Historical Build Tracking

Persist compact `BuildDiffReport` summaries or `BuildReportData` snapshots outside the EditorWindow. Keep storage in a service so CI integration and trend visualization can share the same API.

## Add Bundle History Tracking

Store compact build summaries outside the parser:

- Build timestamp
- Total build size
- Bundle count
- Asset count
- Duplicate waste
- Largest offenders

History persistence should be an editor service, not static window state.
