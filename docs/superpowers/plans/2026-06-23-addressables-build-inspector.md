# Addressables Build Inspector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an Asset Store-ready Unity Editor-only package that loads Addressables Build Layout text reports, analyzes bundle and asset size data, and surfaces duplicate asset waste in a UI Toolkit EditorWindow.

**Architecture:** Use an editor-only assembly under `Assets/AddressablesBuildInspector/Editor`. Keep models, parsing, analysis, services, UI helpers, and the window separated. The MVP supports tolerant text parsing behind `IBuildLayoutParser` so future parsers can be added without changing UI code.

**Tech Stack:** Unity 2022.3+, C#, UI Toolkit, NUnit EditMode tests, no external dependencies.

---

## File Structure

- `Assets/AddressablesBuildInspector/Editor/AddressablesBuildInspector.Editor.asmdef`: editor-only assembly.
- `Assets/AddressablesBuildInspector/Editor/Models/*.cs`: plain report, bundle, asset, duplicate, and summary data.
- `Assets/AddressablesBuildInspector/Editor/Parsing/*.cs`: parser interface, parse result, and tolerant text parser.
- `Assets/AddressablesBuildInspector/Editor/Analysis/BuildAnalysisService.cs`: derived metrics and sorted analysis queries.
- `Assets/AddressablesBuildInspector/Editor/Services/BuildLayoutReportLoader.cs`: editor file picking and parser invocation.
- `Assets/AddressablesBuildInspector/Editor/Utilities/*.cs`: byte formatting, search matching, and UI table helpers.
- `Assets/AddressablesBuildInspector/Editor/UI/AddressablesBuildInspectorWindow.uxml`: EditorWindow layout shell.
- `Assets/AddressablesBuildInspector/Editor/UI/AddressablesBuildInspectorWindow.uss`: Unity-like styling.
- `Assets/AddressablesBuildInspector/Editor/Windows/AddressablesBuildInspectorWindow.cs`: dockable UI Toolkit window.
- `Assets/AddressablesBuildInspector/Editor/Tests/*.cs`: focused EditMode tests for parsing, formatting, and analysis.
- `Assets/AddressablesBuildInspector/Samples/SampleBuildLayout.txt`: small sample report for manual testing.
- `Assets/AddressablesBuildInspector/Documentation/*.md`: README, architecture, setup, extension points.

## Task 1: Assembly, Models, and Test Assembly

**Files:**
- Create: `Assets/AddressablesBuildInspector/Editor/AddressablesBuildInspector.Editor.asmdef`
- Create: `Assets/AddressablesBuildInspector/Editor/Models/BuildReportData.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Models/BundleData.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Models/AssetData.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Models/DuplicateAssetData.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Models/BuildOverviewData.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Tests/AddressablesBuildInspector.Editor.Tests.asmdef`

- [ ] **Step 1: Create the editor assembly definition**

Create `AddressablesBuildInspector.Editor.asmdef` with `includePlatforms` set to `Editor`.

- [ ] **Step 2: Create plain model classes**

Use mutable lists for Unity editor friendliness and read-only computed properties where useful. Keep model constructors small and deterministic.

- [ ] **Step 3: Create the editor test assembly**

Create a Unity Test Framework asmdef referencing `AddressablesBuildInspector.Editor` and `TestAssemblies`.

## Task 2: Utilities with Tests

**Files:**
- Create: `Assets/AddressablesBuildInspector/Editor/Utilities/ByteFormatter.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Utilities/SearchUtility.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Tests/ByteFormatterTests.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Tests/SearchUtilityTests.cs`

- [ ] **Step 1: Write formatter and search tests**

Cover bytes, KB, MB, GB, empty query matching, case-insensitive matching, and null-safe field handling.

- [ ] **Step 2: Implement `ByteFormatter` and `SearchUtility`**

Use invariant culture for formatted sizes. Avoid allocations beyond the final formatted string.

- [ ] **Step 3: Run Unity EditMode tests when Unity CLI is available**

Run Unity Test Runner for the editor test assembly. Expected result: all utility tests pass.

## Task 3: Analysis Service with Tests

**Files:**
- Create: `Assets/AddressablesBuildInspector/Editor/Analysis/BuildAnalysisService.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Tests/BuildAnalysisServiceTests.cs`

- [ ] **Step 1: Write tests for counts, largest items, duplicates, and waste**

Use an in-memory `BuildReportData` with two bundles and one duplicated asset. Expected duplicate waste is `(BundleCount - 1) * AssetSize`.

- [ ] **Step 2: Implement `BuildAnalysisService`**

Return deterministic sorted lists. Do not mutate source report data.

- [ ] **Step 3: Run EditMode tests**

Expected result: utility and analysis tests pass.

## Task 4: Parser with Tests

**Files:**
- Create: `Assets/AddressablesBuildInspector/Editor/Parsing/IBuildLayoutParser.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Parsing/BuildLayoutParseResult.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Parsing/BuildLayoutParser.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Tests/BuildLayoutParserTests.cs`

- [ ] **Step 1: Write parser tests around a representative text report**

Cover bundle extraction, asset extraction, duplicate membership, size parsing, empty file failure, and unrecognized report failure.

- [ ] **Step 2: Implement parse result and parser interface**

`BuildLayoutParseResult` exposes `Success`, `Report`, and `ErrorMessage`. `IBuildLayoutParser.Parse(string filePath)` returns that result and never throws for expected file problems.

- [ ] **Step 3: Implement tolerant text parsing**

Recognize `Bundle:`, `Bundle Name:`, `.bundle` lines, `Size:` lines, and asset path lines containing `Assets/` or `Packages/`. Merge assets by normalized path and append bundle membership.

- [ ] **Step 4: Run EditMode tests**

Expected result: parser tests pass with the representative sample.

## Task 5: UI Toolkit Window

**Files:**
- Create: `Assets/AddressablesBuildInspector/Editor/UI/AddressablesBuildInspectorWindow.uxml`
- Create: `Assets/AddressablesBuildInspector/Editor/UI/AddressablesBuildInspectorWindow.uss`
- Create: `Assets/AddressablesBuildInspector/Editor/Utilities/TableBuilder.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Services/BuildLayoutReportLoader.cs`
- Create: `Assets/AddressablesBuildInspector/Editor/Windows/AddressablesBuildInspectorWindow.cs`

- [ ] **Step 1: Create UXML shell and USS styling**

Include toolbar, status box, tab buttons, overview container, and table containers.

- [ ] **Step 2: Implement report loader service**

Use `EditorUtility.OpenFilePanel` with text-like extensions and delegate parsing to `IBuildLayoutParser`.

- [ ] **Step 3: Implement table helper methods**

Provide reusable header rows, labels, and empty states for ListView rows.

- [ ] **Step 4: Implement EditorWindow**

Add menu item `Tools/Addressables Build Inspector`. Load UXML/USS with `AssetDatabase.LoadAssetAtPath`. Build dashboard-first tabs. Use recycled `ListView` rows for Bundles, Assets, and Duplicates.

- [ ] **Step 5: Manual smoke test in Unity**

Open the menu item, load the sample report, switch tabs, search, and sort each table.

## Task 6: Documentation and Sample

**Files:**
- Create: `Assets/AddressablesBuildInspector/Samples/SampleBuildLayout.txt`
- Create: `Assets/AddressablesBuildInspector/Documentation/README.md`
- Create: `Assets/AddressablesBuildInspector/Documentation/Architecture.md`
- Create: `Assets/AddressablesBuildInspector/Documentation/Setup.md`
- Create: `Assets/AddressablesBuildInspector/Documentation/ExtensionPoints.md`

- [ ] **Step 1: Add a small sample Build Layout report**

Include at least two bundles and one duplicated asset to show duplicate waste.

- [ ] **Step 2: Write README and setup instructions**

Document supported Unity version, menu path, how to generate a build layout report, and how to load it.

- [ ] **Step 3: Write architecture and extension point docs**

Explain parser, analysis, UI boundaries, and where to add future parsers, CSV export, diff comparison, and trend tracking.

## Task 7: Verification

**Files:**
- Inspect all package files created above.

- [ ] **Step 1: Run file and marker scans**

Run a marker scan across `Assets/AddressablesBuildInspector` and `docs/superpowers` for unfinished-work tokens and unimplemented exception throws.
Expected: no production unfinished-work markers.

- [ ] **Step 2: Run Unity tests if Unity CLI is available**

Run Unity in batchmode EditMode tests if an executable is discoverable. Expected: all tests pass.

- [ ] **Step 3: If Unity CLI is unavailable, run static consistency checks**

Check file paths, asmdef JSON, namespace consistency, UXML asset paths, and parser sample coverage.

- [ ] **Step 4: Record limitations**

If Unity could not be run from this environment, report that clearly in the final answer.
