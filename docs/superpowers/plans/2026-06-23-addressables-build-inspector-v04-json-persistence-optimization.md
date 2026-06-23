# Addressables Build Inspector v0.4 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add JSON reports, session persistence, faster loading, Remote/Local filtering, Dependencies UX fixes, and Duplicate Optimization Advisor.

**Architecture:** Keep parsing extensible with a composite parser. Keep optimization logic isolated under `Editor/Optimization`. Keep heavy graph/optimization computations lazy in the EditorWindow.

**Tech Stack:** Unity 2022.3+, C#, UI Toolkit, no external dependencies.

---

## Tasks

- [ ] Add parser tests for JSON and Remote/Local bundle location.
- [ ] Add optimization service tests for candidate severity, cause, recommendation, simulation, and export.
- [ ] Implement `BundleLocation`, JSON parser, composite parser, and parser loader support for txt/json.
- [ ] Add EditorPrefs-backed last report persistence.
- [ ] Refactor load flow so expensive dependency and optimization summaries are lazy.
- [ ] Improve Dependencies tab headings, all-assets default list, and scrollable tree/details.
- [ ] Add Remote/Local filter to core tables.
- [ ] Add sortable header affordance.
- [ ] Implement `Editor/Optimization` subsystem and Optimization tab.
- [ ] Update docs and migration notes.
- [ ] Run compile and validation checks.
