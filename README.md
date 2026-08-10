<div align="center">

# 📦 Addressables Build Inspector

**Understand your Unity Addressables build in minutes — not hours.**

Inspect build layout reports, hunt down duplicated assets, trace dependency chains, diff two builds, and turn it all into AI‑ready prompts — all from one Editor window, with zero external dependencies.

[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity&logoColor=white)](https://unity.com)
[![Asset Store](https://img.shields.io/badge/Asset%20Store-Get%20it%20free-blue?logo=unity)](https://assetstore.unity.com/packages/tools/utilities/addressables-build-inspector-388716?aid=1100lebp8)
[![Editor Only](https://img.shields.io/badge/Runtime-Editor--only-informational)]()
[![No Dependencies](https://img.shields.io/badge/Dependencies-none-success)]()

<!-- 💡 Replace with an actual screenshot or GIF of the Overview / Optimization tab -->
<img src="docs/images/overview.png" width="800" alt="Addressables Build Inspector — Overview tab" />

</div>

---

## Why this tool exists

Addressables build reports are goldmines of information — and painful to read by hand. **Addressables Build Inspector** turns a raw build layout report (text or JSON) into a searchable, sortable Editor window that answers the questions that actually matter:

- Which bundles are eating my build size?
- What's duplicated across bundles, and how much am I wasting because of it?
- Why is this asset even included — what's pulling it in?
- What changed between yesterday's build and today's?
- Can I hand this to an LLM to get a second opinion?

No setup, no package dependencies, no runtime footprint. Drop it in, load a report, and start optimizing.

## ✨ Features

| | |
|---|---|
| 📊 **Overview** | Total build size, bundle & asset counts, duplicate count, estimated waste, largest bundle/asset at a glance |
| 🧱 **Bundles** | Sortable table — name, size, asset count |
| 🗂 **Assets** | Sortable table — name, path, size, bundle count |
| 🧬 **Duplicates** | Every duplicated asset, ranked by estimated wasted size |
| 🛠 **Optimization Advisor** | Classifies duplicate causes, recommends shared groups, simulates savings *non‑destructively*, exports CSV / JSON / Markdown |
| 🔗 **Dependencies** | Full dependency tree + reverse references per asset, with circular‑reference safety |
| 📈 **Build Diff** | Compare two reports: growth, count deltas, top‑growing bundles, added/removed/modified assets, ranked smart insights |
| 🤖 **AI Report** | Generates offline, LLM‑ready prompts (Quick Review, Optimization Consultant, Build Growth Investigation, Duplicate Investigation, Technical Audit) — paste straight into your favorite AI assistant |

All tables are click‑to‑sort. All exports (CSV / JSON / Markdown) happen locally — nothing leaves your machine.

## 🚀 Installation

**Option A — Unity Package**

1. Download [`AddressablesUtility.unitypackage`](./AddressablesUtility.unitypackage) from this repo (or grab it free from the [Asset Store](https://assetstore.unity.com/packages/tools/utilities/addressables-build-inspector-388716?aid=1100lebp8)).
2. `Assets → Import Package → Custom Package…` in your Unity project.

**Option B — Copy the folder**

Copy `Assets/AddressablesBuildInspector` into your project's `Assets` folder. That's it — the tool is Editor‑only and adds a single menu item.

**Requirements**

- Unity **2022.3** or newer
- An Addressables Build Layout report (text or supported JSON) — see below
- No runtime dependencies, no external packages

## ⚡ Quick Start

1. Build your Addressables content and generate a Build Layout report (location depends on your Addressables package version/settings).
2. Open `Tools → Addressables Build Inspector`.
3. Click **Load Report** and select the generated report.
4. Start with **Overview → Duplicates → Optimization** to find quick wins.
5. Use **Dependencies** to understand *why* something is pulled into your build.
6. Use **Build Diff** to compare two reports over time and catch regressions before they ship.

The window remembers your last successfully loaded report per‑user via `EditorPrefs`, so reopening it picks up right where you left off.

> Want to try it without a real project? A synthetic sample report is included at `Assets/AddressablesBuildInspector/Samples/SampleBuildLayout.txt`, plus `_Old`/`_New` variants for testing Build Diff.

## 🤖 AI Report — talk to your build

The **AI Report** tab turns your loaded build data into a structured, copy‑paste‑ready prompt for any LLM (Claude, ChatGPT, etc.) — entirely offline, no API keys, no data leaves the Editor.

| Mode | Best for |
|---|---|
| Quick Review | A fast, high‑level pass over the build |
| Optimization Consultant | Actionable recommendations to cut size |
| Build Growth Investigation | Explaining *why* a build got bigger |
| Duplicate Investigation | Deep dive into duplication root causes |
| Technical Audit | A thorough, detail‑heavy report for reviewers |

## 🧩 JSON report schema

If you're feeding in a JSON report instead of the native text layout, use this shape:

```json
{
  "bundles": [
    {
      "name": "remote.bundle",
      "FileSize": 2048,
      "assets": [
        { "name": "Hero.png", "path": "Assets/Textures/Hero.png", "Size": 1024 }
      ]
    }
  ],
  "dependencies": [
    { "source": "Assets/Hero.prefab", "target": "Assets/Textures/Hero.png" }
  ]
}
```

Size fields are flexible — `sizeBytes`, `SizeBytes`, `FileSize`, `Size`, or `TotalSize` are all recognized, since different report producers use different casing/naming.

## 🧮 Duplicate waste formula

```
EstimatedWaste = (BundleCount - 1) × AssetSize
```

A practical first‑pass estimate of avoidable bundle growth caused by an asset being packed into more than one bundle.

## 🗺️ Roadmap

- [ ] Screenshots / short demo GIF in this README
- [ ] UPM package support (`package.json` + git URL install)
- [ ] Automated tests in CI
- [ ] More AI report templates

Have an idea? Open an [issue](../../issues) — feedback shapes what gets built next.

## 🐛 Bug reports & feature requests

Found something broken, or have an idea for a new tab/export/AI mode? Please [open an issue](../../issues/new/choose) — there are dedicated forms for **🐛 Bug Report** and **💡 Feature Request** that ask for exactly the details needed to act on it quickly (Unity version, report type, repro steps, etc).

## 🤝 Contributing

Issues and PRs are welcome. If you're proposing a larger change, please open an issue first so we can align on the approach.

## 📄 License

See the [Asset Store EULA](https://unity.com/legal/as-terms) for the packaged release. *(A repository license file is planned — until then, treat this repo as source‑available for reference alongside the Asset Store listing.)*

## 🔗 Links

- 🛒 [Asset Store listing](https://assetstore.unity.com/packages/tools/utilities/addressables-build-inspector-388716?aid=1100lebp8) — free
- 📚 [Full documentation](Assets/AddressablesBuildInspector/Documentation/README.md)
- 🛠 [Setup guide](Assets/AddressablesBuildInspector/Documentation/Setup.md)

---

<div align="center">
Made by <a href="https://assetstore.unity.com/publishers/149966">Proximum</a> · If this saved you a build‑size headache, a ⭐ on the repo goes a long way.
</div>
