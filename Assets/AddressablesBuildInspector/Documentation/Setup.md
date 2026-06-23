# Setup

## Import

Import the `Assets/AddressablesBuildInspector` folder into a Unity 2022.3+ project.

The package is editor-only. It adds one menu item:

`Tools > Addressables Build Inspector`

## Generate a Build Layout Report

In an Addressables project, enable Addressables build layout generation before building content. Unity writes a text report for the Addressables content build. The exact output location can vary by Addressables package version and project settings.

After the report is generated:

1. Open `Tools > Addressables Build Inspector`.
2. Click `Load Report`.
3. Select the generated text or supported JSON report.
4. Review the Overview, Duplicates, and Optimization tabs first.
5. Use the toolbar `Location` filter to inspect Remote and Local bundles separately.
6. Open Dependencies to inspect why assets are included.
7. Open Build Diff to compare two generated reports.

## Recommended Workflow

Use this tool after content builds and before release packaging. Start with the Duplicates and Optimization tabs, fix high-waste duplicated assets, rebuild Addressables content, then compare the new totals.

Build Diff Analyzer can export CSV or JSON reports. Duplicate Optimization Advisor can export CSV, JSON, or Markdown reports.

The tool restores the last successfully loaded report when the window is reopened and the file still exists. This is stored per editor user with `EditorPrefs`.
