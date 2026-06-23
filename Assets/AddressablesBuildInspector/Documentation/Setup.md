# Setup

## Import

Import the `Assets/AddressablesBuildInspector` folder into a Unity 2022.3+ project.

The package is editor-only. It adds one menu item:

`Tools > Addressables Build Inspector`

## Generate a Build Layout Report

In an Addressables project, enable Addressables build layout generation before building content. Unity writes a text report for the Addressables content build. The exact output location can vary by Addressables package version and project settings.

After the report is generated:

1. Open `Tools > Addressables Build Inspector`.
2. Click `Load Build Layout`.
3. Select the generated text report.
4. Review the Overview and Duplicates tabs first.

## Recommended Workflow

Use this tool after content builds and before release packaging. Start with the Duplicates tab, fix high-waste duplicated assets, rebuild Addressables content, then compare the new totals manually.

Build diff comparison is intentionally left as a future extension.
