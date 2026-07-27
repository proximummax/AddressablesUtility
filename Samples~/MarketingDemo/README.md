# Addressables Build Inspector Marketing Demo

This folder contains a synthetic Addressables Build Layout report for Asset Store screenshots and short product videos.

Load this file in the Unity Editor window:

`Samples~/MarketingDemo/addressables-build-inspector-marketing-demo.json`

For the Build Diff tab, compare these two files:

- Old build: `Samples~/MarketingDemo/addressables-build-inspector-build-diff-old.json`
- New build: `Samples~/MarketingDemo/addressables-build-inspector-build-diff-new.json`

Recommended screenshot/video flow:

1. Open `Tools > Addressables Build Inspector`.
2. Click `Load Report`.
3. Select `addressables-build-inspector-marketing-demo.json`.
4. Capture the `Overview` tab first.
5. Capture the `Optimization` tab second.
6. Optional: open `Dependencies` and `AI Report` for short video clips.

Recommended Build Diff screenshot/video flow:

1. Open the `Build Diff` tab.
2. Select `addressables-build-inspector-build-diff-old.json` as the old build.
3. Select `addressables-build-inspector-build-diff-new.json` as the new build.
4. Click `Compare`.
5. Capture the KPI row, Smart Insights, bundle growth table, and asset diff table.

Expected KPI values are intentionally strong:

- Build Size: about `3.21 GB`
- Duplicate Waste: about `418 MB`
- Potential Savings: about `418 MB`
- Health Score: about `72 / 100`
- Top candidate: `Environment_Atlas_4K.png`

Expected Build Diff values:

- Total Growth: about `840 MB`
- Bundle Count Delta: `+3`
- Asset Count Delta: about `+21`
- Duplicate Waste Delta: about `+270 MB`
- Largest added bundle: `seasonal_event_2026.bundle`
- Largest added asset: `Seasonal_Cinematic_Intro.mov`

The report is synthetic and should not be presented as a real project build.

To regenerate the JSON after changing the scenario:

```bash
node Samples~/MarketingDemo/generate-marketing-demo-report.js
```
