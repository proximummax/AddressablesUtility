# Addressables Build Inspector v0.2-v0.3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Dependency Explorer and Build Diff Analyzer to the existing Unity Editor-only Addressables Build Inspector package.

**Architecture:** Add isolated `Editor/Dependencies` and `Editor/Diff` subsystems. Keep parser extensions small and preserve existing report models. UI remains a thin coordinator over testable services.

**Tech Stack:** Unity 2022.3+, C#, UI Toolkit, Unity Test Framework-compatible EditMode tests, no external dependencies.

---

## Task 1: Dependency Explorer Core

- [ ] Add tests for dependency provider extraction, graph traversal, reverse lookup, and circular reference protection.
- [ ] Add `DependencyNode`, `IDependencyProvider`, `BuildLayoutDependencyProvider`, `AssetDatabaseDependencyProvider`, `DependencyGraphCache`, `DependencyGraphService`, `DependencyTreeBuilder`, and `DependencySearchService`.
- [ ] Extend parser dependency capture to recognize `Dependency:` lines with `source -> target`.

## Task 2: Dependency Explorer UI

- [ ] Add `Dependencies` tab to UXML/window.
- [ ] Render search results, expandable tree rows, and selected node detail panel.
- [ ] Add overview cards for shared assets, most referenced asset, and largest dependency chain.

## Task 3: Build Diff Core

- [ ] Add tests for bundle diffs, asset diffs, growth reasons, duplicate regression, and insights.
- [ ] Add `DiffStatus`, `BuildDiffReport`, `BundleDiff`, `AssetDiff`, `GrowthReason`, `DuplicateRegression`, `DiffInsight`, `DiffViewModel`, `BuildDiffAnalyzer`, `BuildDiffService`, and `DiffExportService`.
- [ ] Use dictionaries for bundle and asset comparison.

## Task 4: Build Diff UI

- [ ] Add `Build Diff` tab to UXML/window.
- [ ] Add old/new report browse controls, compare action, overview panel, insights, bundle diff table, asset diff table, growth details, and CSV/JSON export buttons.
- [ ] Add overview cards for build growth, largest growth bundle, largest added asset, and duplicate regression after a diff is generated.

## Task 5: Documentation and Verification

- [ ] Update README, architecture, extension points, setup, and migration notes.
- [ ] Run marker scan.
- [ ] Run compile-oriented production/test builds.
- [ ] Run standalone validation harness for pure service logic.
