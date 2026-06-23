# Migration Notes: v0.1 to v0.4

## v0.2 Dependency Explorer

Version 0.2 adds the `Dependencies` tab and a new isolated `Editor/Dependencies` subsystem.

No v0.1 API was removed. Existing parsing, overview, bundle, asset, and duplicate workflows continue to work.

Reports with dependency lines such as:

`Dependency: Assets/Characters/Hero.prefab -> Assets/Shared/Icon.png`

will use those edges directly. Reports without dependency information use `AssetDatabase.GetDependencies` as a fallback for project assets.

## v0.3 Build Diff Analyzer

Version 0.3 adds the `Build Diff` tab and a new isolated `Editor/Diff` subsystem.

No v0.1 or v0.2 model was removed. Diff logic compares two parsed `BuildReportData` instances and generates a `BuildDiffReport`.

New capabilities:

- old/new Build Layout selection
- bundle diff
- asset diff
- growth reason analysis
- duplicate regression analysis
- smart insights
- CSV export
- JSON export

## Recommended Validation After Import

1. Open `Tools > Addressables Build Inspector`.
2. Load `Samples/SampleBuildLayout.txt`.
3. Check `Dependencies` and select `Hero.prefab`.
4. In `Build Diff`, select `SampleBuildLayout_Old.txt` and `SampleBuildLayout_New.txt`.
5. Click `Compare`.

## v0.4 JSON, Persistence, Location Filters, and Optimization

Version 0.4 adds:

- JSON report loading through `BuildReportParser` and `BuildReportJsonParser`
- remembered last report path in the EditorWindow
- Local/Remote bundle location inference
- toolbar location filter for Bundles, Assets, Duplicates, and Optimization
- visible sort markers in table headers
- scroll-contained Dependencies tree and details panels
- Duplicate Optimization Advisor in `Editor/Optimization`
- CSV, JSON, and Markdown optimization export

No existing model property was removed. `BundleData` now includes a `Location` property that defaults to `Unknown` for reports without local/remote information.

Updated validation:

1. Open `Tools > Addressables Build Inspector`.
2. Load `Samples/SampleBuildLayout.txt`.
3. Reopen the window and confirm the report is restored.
4. Switch the toolbar `Location` filter between `All`, `Remote`, and `Local`.
5. Open `Optimization` and confirm duplicate candidates and export buttons are available.
6. Open `Dependencies`, search or select an asset, and confirm the tree/details areas scroll instead of stretching the window.
