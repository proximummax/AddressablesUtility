# Extension Points

## Add Another Report Parser

Implement `IBuildLayoutParser` in `Editor/Parsing`.

The parser should return `BuildLayoutParseResult` and populate `BuildReportData`. UI code should not need to change if the new parser produces the same model.

Possible next parsers:

- Strict parser for a known Addressables package version
- JSON parser
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

## Add Bundle History Tracking

Store compact build summaries outside the parser:

- Build timestamp
- Total build size
- Bundle count
- Asset count
- Duplicate waste
- Largest offenders

History persistence should be an editor service, not static window state.
