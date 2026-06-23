# Addressables Build Inspector v0.4 Design

Date: 2026-06-23

## Scope

This change adds JSON report loading, report session persistence, faster report opening, Remote/Local filtering, clearer sortable headers, a more robust Dependencies tab, and the v0.4 Duplicate Optimization Advisor.

## Loading and Persistence

Parsing moves behind `BuildReportParser`, a composite parser that selects JSON or text based on extension and content. The existing text parser remains supported. `BuildReportJsonParser` supports a simple Asset Store-documented JSON schema with bundles, assets, dependencies, and bundle location metadata.

The EditorWindow stores the last successfully loaded report path in `EditorPrefs`. On reopen, the window restores that path and schedules a delayed reload so UI creation remains responsive.

## Performance

Report loading should parse and render core Overview/Bundles/Assets first. Dependency-heavy data is built lazily when the Dependencies tab is opened. Optimization data is built lazily when the Optimization tab is opened. Overview cards that require heavy graph walks use cached summaries and do not block first paint.

## Remote and Local Filtering

Bundles receive a `BundleLocation` value: Unknown, Local, or Remote. Text parsing infers location from Remote/Local keywords and URL-like paths. JSON parsing reads explicit `location`/`loadPath`/`isRemote` fields. The main toolbar exposes a location filter for Bundles, Assets, Duplicates, and Optimization.

## Dependencies UX

The Dependencies tab gets explicit headings for asset search, dependency tree, and details. Search shows all assets by default. The tree and details panels are wrapped in scroll views so large scenes and large dependency graphs do not break layout.

## Optimization Advisor

The new `Editor/Optimization` subsystem is isolated from UI. It analyzes duplicate assets, estimates severity, detects likely causes, recommends fixes, traces dependency paths, simulates savings, generates insights, and exports CSV/JSON/Markdown.

The UI adds an Optimization tab with dashboard cards, searchable/sortable candidate table, detail panel, and export buttons.
