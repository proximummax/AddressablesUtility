const fs = require("fs");
const path = require("path");

const inputPath = process.argv[2];
const outputPath = process.argv[3] || path.join(__dirname, "addressables-build-inspector-demo.html");

if (!inputPath) {
  console.error("Usage: node generate-addressables-html-demo.js <buildlayout.json> [output.html]");
  process.exit(1);
}

function refId(value) {
  return value && typeof value === "object" ? value.rid || 0 : 0;
}

function bytes(value) {
  return Math.max(0, Number(value || 0));
}

function fileName(value) {
  const normalized = String(value || "").replace(/\\/g, "/");
  const index = normalized.lastIndexOf("/");
  return index >= 0 ? normalized.slice(index + 1) : normalized;
}

function formatBytes(value) {
  const units = ["B", "KB", "MB", "GB"];
  let size = Math.max(0, Number(value || 0));
  let unit = 0;
  while (size >= 1024 && unit < units.length - 1) {
    size /= 1024;
    unit += 1;
  }

  return unit === 0 ? `${Math.round(size)} ${units[unit]}` : `${size.toFixed(size >= 10 ? 1 : 2)} ${units[unit]}`;
}

function normalize(value) {
  return String(value || "").replace(/\\/g, "/").trim();
}

function severityFor(waste, totalSize) {
  const ratio = totalSize > 0 ? waste / totalSize : 0;
  if (ratio >= 0.05) return "Critical";
  if (ratio >= 0.02) return "High";
  if (waste > 0) return "Medium";
  return "Low";
}

function parseBuildLayout(raw, sourceName = "buildlayout.json") {
  const json = typeof raw === "string" ? JSON.parse(raw) : raw;
  const refs = json.references && Array.isArray(json.references.RefIds) ? json.references.RefIds : [];
  const byRid = new Map(refs.map((item) => [item.rid, item]));
  const bundlesByRid = new Map();
  const filesByRid = new Map();
  const assetsByPath = new Map();
  const bundleRidByFileRid = new Map();

  for (const item of refs) {
    const type = item.type && item.type.class;
    const data = item.data || {};
    if (type === "BuildLayout/Bundle") {
      bundlesByRid.set(item.rid, {
        rid: item.rid,
        name: data.Name || data.InternalName || `Bundle ${item.rid}`,
        size: bytes(data.FileSize || data.Size || data.BundleSize || data.TotalSize),
        assetCount: Number(data.AssetCount || 0),
        loadPath: data.LoadPath || "",
        dependencies: (data.DependentBundles || data.BundleDependencies || data.Dependencies || []).map(refId).filter(Boolean),
      });
      for (const file of data.Files || []) {
        bundleRidByFileRid.set(refId(file), item.rid);
      }
    }
  }

  for (const item of refs) {
    if (item.type && item.type.class === "BuildLayout/File") {
      const data = item.data || {};
      filesByRid.set(item.rid, data);
      const bundleRid = refId(data.Bundle);
      if (bundleRid) bundleRidByFileRid.set(item.rid, bundleRid);
    }
  }

  function addAsset(item, explicitBundleRid) {
    const data = item.data || item || {};
    const assetPath = normalize(data.AssetPath || data.MainAssetPath || data.AddressableName || data.Filename || data.Path);
    if (!assetPath) return null;

    const serialized = bytes(data.SerializedSize);
    const streamed = bytes(data.StreamedSize);
    const size = bytes(data.TotalSize || data.FileSize || data.Size || serialized + streamed);
    const guid = data.Guid || data.AssetGuid || "";
    let asset = assetsByPath.get(assetPath);
    if (!asset) {
      asset = {
        name: fileName(assetPath),
        path: assetPath,
        guid,
        size,
        bundles: new Set(),
        references: new Set(),
        referencedBy: new Set(),
      };
      assetsByPath.set(assetPath, asset);
    } else if (size > asset.size) {
      asset.size = size;
    }

    const bundleRid = explicitBundleRid || refId(data.Bundle) || bundleRidByFileRid.get(refId(data.File));
    if (bundleRid && bundlesByRid.has(bundleRid)) {
      asset.bundles.add(bundlesByRid.get(bundleRid).name);
    }

    for (const group of [
      data.InternalReferencedOtherAssets,
      data.InternalReferencedExplicitAssets,
      data.ExternallyReferencedAssets,
      data.Dependencies,
      data.DirectDependencies,
      data.ReferencedAssets,
    ]) {
      for (const dependencyRef of group || []) {
        const dependency = byRid.get(refId(dependencyRef));
        const dependencyPath = normalize(dependency && dependency.data && (dependency.data.AssetPath || dependency.data.MainAssetPath || dependency.data.AddressableName));
        if (dependencyPath) asset.references.add(dependencyPath);
      }
    }

    return asset;
  }

  for (const item of refs) {
    const type = item.type && item.type.class;
    if (type === "BuildLayout/ExplicitAsset" || type === "BuildLayout/DataFromOtherAsset" || type === "BuildLayout/Asset") {
      addAsset(item, 0);
    }
  }

  for (const item of refs) {
    if (!(item.type && item.type.class === "BuildLayout/File")) continue;
    const bundleRid = bundleRidByFileRid.get(item.rid);
    for (const assetRef of (item.data && item.data.Assets) || []) {
      const assetItem = byRid.get(refId(assetRef));
      if (assetItem) addAsset(assetItem, bundleRid);
    }
  }

  const assetsByGuid = new Map();
  for (const asset of assetsByPath.values()) {
    if (asset.guid && !assetsByGuid.has(asset.guid)) assetsByGuid.set(asset.guid, asset);
  }

  for (const duplicated of json.DuplicatedAssets || []) {
    const asset = assetsByGuid.get(duplicated.AssetGuid || duplicated.Guid);
    if (!asset) continue;
    for (const object of duplicated.DuplicatedObjects || []) {
      for (const fileRef of object.IncludedInBundleFiles || []) {
        const bundle = bundlesByRid.get(bundleRidByFileRid.get(refId(fileRef)));
        if (bundle) asset.bundles.add(bundle.name);
      }
    }
  }

  for (const asset of assetsByPath.values()) {
    for (const dependencyPath of asset.references) {
      const dependency = assetsByPath.get(dependencyPath);
      if (dependency) dependency.referencedBy.add(asset.path);
    }
  }

  const bundles = Array.from(bundlesByRid.values()).map((bundle) => {
    const assets = Array.from(assetsByPath.values()).filter((asset) => asset.bundles.has(bundle.name));
    return {
      name: bundle.name,
      size: bundle.size,
      assetCount: Math.max(bundle.assetCount, assets.length),
      loadPath: bundle.loadPath,
      dependencies: bundle.dependencies.map((rid) => bundlesByRid.get(rid)?.name).filter(Boolean),
    };
  });

  const assets = Array.from(assetsByPath.values()).map((asset) => ({
    name: asset.name,
    path: asset.path,
    size: asset.size,
    bundleCount: asset.bundles.size,
    bundles: Array.from(asset.bundles).sort(),
    dependencyCount: asset.references.size,
    referencedByCount: asset.referencedBy.size,
  }));

  const totalSize = bundles.reduce((sum, bundle) => sum + bundle.size, 0);
  const duplicates = assets
    .filter((asset) => asset.bundleCount > 1)
    .map((asset) => ({
      name: asset.name,
      path: asset.path,
      size: asset.size,
      bundleCount: asset.bundleCount,
      waste: Math.max(0, asset.bundleCount - 1) * Math.max(0, asset.size),
      severity: severityFor(Math.max(0, asset.bundleCount - 1) * Math.max(0, asset.size), totalSize),
    }))
    .sort((a, b) => b.waste - a.waste);

  const potentialSavings = duplicates.reduce((sum, duplicate) => sum + duplicate.waste, 0);
  const health = Math.max(0, Math.min(100, 100 - Math.round(Math.min(45, (potentialSavings / Math.max(1, totalSize)) * 220)) - Math.min(20, Math.floor(duplicates.length / 10))));

  const optimizationCandidates = duplicates.slice(0, 120).map((item) => ({
    asset: item.name,
    path: item.path,
    bundles: item.bundleCount,
    waste: item.waste,
    savings: item.waste,
    severity: item.severity,
    recommendation: item.bundleCount >= 4 ? "Move to shared group" : "Review group packing",
  }));

  const dependencySamples = assets
    .filter((asset) => asset.dependencyCount > 0 || asset.referencedByCount > 0)
    .sort((a, b) => b.referencedByCount - a.referencedByCount || b.dependencyCount - a.dependencyCount)
    .slice(0, 160);

  return {
    sourceName,
    generatedAt: new Date().toISOString(),
    unityVersion: json.UnityVersion || "",
    packageVersion: json.PackageVersion || "",
    buildTarget: json.BuildTarget,
    duration: json.Duration,
    summary: {
      totalSize,
      bundleCount: bundles.length,
      assetCount: assets.length,
      duplicateCount: duplicates.length,
      duplicateWaste: potentialSavings,
      potentialSavings,
      health,
      largestBundle: bundles.slice().sort((a, b) => b.size - a.size)[0] || null,
      largestAsset: assets.slice().sort((a, b) => b.size - a.size)[0] || null,
    },
    bundles: bundles.sort((a, b) => b.size - a.size).slice(0, 220),
    assets: assets.sort((a, b) => b.size - a.size).slice(0, 260),
    duplicates: duplicates.slice(0, 180),
    optimizationCandidates,
    dependencySamples,
  };
}

const raw = fs.readFileSync(inputPath, "utf8");
const demoData = parseBuildLayout(raw, path.basename(inputPath));

const html = `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Addressables Build Inspector Demo</title>
  <style>
    :root {
      --bg:#1E1E1E; --panel:#252526; --elev:#2D2D30; --border:#3C3C3C; --soft:#343438;
      --accent:#4FC3F7; --text:#D4D4D4; --muted:#9E9E9E; --success:#4CAF50;
      --warning:#FFB74D; --danger:#EF5350; --critical:#E53935; --high:#FB8C00; --medium:#FDD835; --low:#43A047;
    }
    * { box-sizing: border-box; }
    body { margin:0; background:var(--bg); color:var(--text); font:12px/1.35 "Segoe UI", Arial, sans-serif; }
    button, input, select { font:inherit; }
    .app { height:100vh; display:grid; grid-template-rows:42px 30px 38px 1fr; overflow:hidden; background:var(--bg); }
    .toolbar { min-height:42px; display:flex; align-items:center; gap:6px; padding:8px 10px; background:var(--panel); border-bottom:1px solid var(--border); }
    .source { color:var(--muted); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; flex:1; }
    .btn { height:26px; padding:0 10px; border:1px solid var(--border); border-radius:4px; background:#313135; color:var(--text); cursor:pointer; }
    .btn.primary { border-color:#3F6E84; background:#244353; color:white; }
    .btn:hover { background:#38383D; border-color:#56565C; }
    .search { width:260px; height:26px; color:var(--text); background:#1B1B1C; border:1px solid var(--border); border-radius:4px; padding:0 9px; }
    .status { min-height:30px; padding:7px 12px; border-bottom:1px solid var(--border); background:#202124; color:var(--muted); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .tabbar { min-height:38px; display:flex; align-items:flex-end; padding-left:8px; padding-top:6px; background:#1B1B1C; border-bottom:1px solid var(--border); overflow-x:auto; }
    .tab { height:31px; min-width:98px; margin-right:3px; padding:0 10px; border:1px solid var(--border); border-bottom-width:0; border-radius:4px 4px 0 0; background:#2B2B2F; color:var(--muted); cursor:pointer; }
    .tab:hover { background:#34343A; color:var(--text); }
    .tab.active { background:var(--elev); color:var(--accent); font-weight:700; }
    .content { min-width:0; min-height:0; overflow:auto; padding:10px; background:var(--bg); }
    .view-caption { min-height:22px; margin-bottom:6px; color:var(--text); font-weight:700; }
    .grid { display:flex; flex-wrap:wrap; gap:8px; margin-bottom:10px; }
    .card { min-width:190px; max-width:280px; min-height:88px; flex:1 1 190px; background:var(--panel); border:1px solid var(--border); border-radius:6px; padding:10px; overflow:hidden; }
    .card.health { grid-column:span 2; border-left:4px solid var(--success); background:#292B2E; }
    .card.warn { border-left:4px solid var(--warning); }
    .label { color:var(--muted); font-size:11px; font-weight:700; }
    .value { margin-top:7px; color:white; font-size:18px; font-weight:700; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .meta { margin-top:5px; color:var(--muted); font-size:10px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .panel { background:var(--panel); border:1px solid var(--border); border-radius:0; overflow:hidden; margin-bottom:10px; }
    .panel-head { min-height:32px; display:flex; align-items:center; justify-content:space-between; gap:12px; padding:0 10px; border-bottom:1px solid var(--border); background:#303036; font-weight:700; color:white; }
    table { width:100%; border-collapse:collapse; table-layout:fixed; }
    th { height:32px; padding:0 10px; color:var(--text); text-align:left; border-bottom:1px solid var(--border); background:#303036; font-size:12px; cursor:pointer; }
    td { height:30px; padding:0 10px; border-bottom:1px solid var(--soft); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; color:var(--text); }
    tr:nth-child(even) td { background:#28282B; }
    tr:hover td { background:#303036; }
    .num { text-align:right; font-variant-numeric:tabular-nums; }
    .danger { color:#FF8A80; font-weight:700; }
    .pill { display:inline-flex; align-items:center; justify-content:center; min-width:70px; height:20px; padding:0 8px; border:1px solid; border-radius:4px; font-size:10px; font-weight:800; }
    .Critical { color:white; background:#5B1E20; border-color:var(--critical); }
    .High { color:#FFE4C2; background:#4C2D10; border-color:var(--high); }
    .Medium { color:#FFF6B8; background:#443A12; border-color:var(--medium); }
    .Low { color:#C8F6CD; background:#1D3D22; border-color:var(--low); }
    .split { display:grid; grid-template-columns:1fr 340px; gap:10px; min-height:420px; }
    .detail { padding:10px; color:var(--muted); }
    .detail strong { display:block; color:white; margin-bottom:4px; }
    .ai-controls { display:grid; grid-template-columns:130px minmax(220px, 360px) 1fr auto auto auto; gap:8px; align-items:center; padding:10px; border-bottom:1px solid var(--border); background:#252528; }
    .ai-controls select { height:28px; color:var(--text); background:#3A3A3D; border:1px solid var(--border); border-radius:4px; padding:0 8px; }
    .ai-description { color:var(--muted); font-size:11px; line-height:1.35; min-width:180px; }
    .ai { width:100%; min-height:420px; resize:vertical; background:#1B1B1C; color:var(--text); border:0; padding:12px; white-space:pre; overflow:auto; }
    .hidden { display:none; }
    @media (max-width:1100px) { .tab{min-width:92px}.grid{display:grid;grid-template-columns:repeat(2,1fr)}.split{grid-template-columns:1fr}.search{width:180px}.ai-controls{grid-template-columns:1fr 1fr}.ai-description{grid-column:1 / -1} }
  </style>
</head>
<body>
<div class="app">
  <div class="toolbar">
    <button class="btn primary" id="loadBtn">Load JSON</button>
    <input id="fileInput" type="file" accept=".json" hidden>
    <button class="btn" id="resetBtn">Clear</button>
    <div class="source" id="sourceLabel"></div>
    <input class="search" id="search" placeholder="Search">
  </div>
  <div class="status" id="statusLabel"></div>
  <nav class="tabbar" id="tabs"></nav>
  <main class="content">
    <div class="view-caption" id="viewSubtitle"></div>
    <section id="overview"></section>
    <section id="tablePanel" class="panel"></section>
    <section id="aiPanel" class="panel hidden"></section>
  </main>
</div>
<script>
const DEMO_DATA = ${JSON.stringify(demoData)};
${clientScript()}
</script>
</body>
</html>`;

fs.writeFileSync(outputPath, html, "utf8");
console.log(`Wrote ${outputPath}`);
console.log(`Demo: ${demoData.summary.bundleCount} bundles, ${demoData.summary.assetCount} assets, ${demoData.summary.duplicateCount} duplicates`);

function clientScript() {
  return String.raw`
${refId.toString()}
${bytes.toString()}
${fileName.toString()}
${normalize.toString()}
${severityFor.toString()}
${parseBuildLayout.toString()}
let data = DEMO_DATA;
let active = "optimization";
let query = "";
let selectedCandidate = null;
let aiMode = "OptimizationConsultant";
let generatedAiPrompt = "";
const tabs = document.getElementById("tabs");
const viewSubtitle = document.getElementById("viewSubtitle");
const sourceLabel = document.getElementById("sourceLabel");
const statusLabel = document.getElementById("statusLabel");
const overview = document.getElementById("overview");
const tablePanel = document.getElementById("tablePanel");
const aiPanel = document.getElementById("aiPanel");
const search = document.getElementById("search");
const loadBtn = document.getElementById("loadBtn");
const resetBtn = document.getElementById("resetBtn");
const fileInput = document.getElementById("fileInput");

const views = [
  ["overview", "Overview", "Build health, size, duplicate waste, and top offenders."],
  ["optimization", "Optimization", "Actionable duplicate waste candidates ranked by potential savings."],
  ["bundles", "Bundles", "Largest bundles and dependency counts."],
  ["assets", "Assets", "Largest assets and bundle membership."],
  ["duplicates", "Duplicates", "Assets included by multiple bundles."],
  ["dependencies", "Dependencies", "Most referenced assets and dependency density."],
  ["ai", "AI Report", "Copy-ready prompt for technical review and fix planning."]
];

const aiReportTypes = {
  QuickReview: {
    label: "Quick Review",
    description: "Fast executive summary with the biggest risks and highest ROI candidates.",
    focus: "Find the most important Addressables issues a developer should notice first."
  },
  OptimizationConsultant: {
    label: "Optimization Consultant",
    description: "Detailed optimization plan focused on duplicate waste and bundle size reduction.",
    focus: "Create a practical optimization plan that preserves current loading behavior."
  },
  DuplicateInvestigation: {
    label: "Duplicate Investigation",
    description: "Deep dive into duplicated assets, shared bundle candidates, and validation risks.",
    focus: "Investigate duplicate assets and propose low-risk fixes without creating unnecessary shared bundles."
  },
  TechnicalAudit: {
    label: "Technical Audit",
    description: "Reviewer-style audit for architecture, performance, memory, UX, docs, and error handling.",
    focus: "Review the report as a senior Unity Asset Store reviewer and identify production risks."
  },
  FixPlan: {
    label: "Fix Plan Prompt",
    description: "Step-by-step implementation plan prompt with validation and rollback sections.",
    focus: "Create a step-by-step implementation plan prioritized by ROI and project risk."
  }
};

function fmtBytes(value) {
  const units = ["B", "KB", "MB", "GB"];
  let size = Math.max(0, Number(value || 0));
  let unit = 0;
  while (size >= 1024 && unit < units.length - 1) { size /= 1024; unit++; }
  return unit === 0 ? Math.round(size) + " " + units[unit] : size.toFixed(size >= 10 ? 1 : 2) + " " + units[unit];
}
function esc(value) {
  return String(value ?? "").replace(/[&<>"']/g, (c) => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&#039;" }[c]));
}
function healthText(score) {
  if (score >= 90) return "Excellent";
  if (score >= 70) return "Needs attention";
  return "High risk";
}
function filtered(rows, fields) {
  const q = query.trim().toLowerCase();
  if (!q) return rows;
  return rows.filter((row) => fields.some((field) => String(row[field] ?? "").toLowerCase().includes(q)));
}
function renderTabs() {
  tabs.innerHTML = views.map(([id, label]) => '<button class="tab '+(id===active?'active':'')+'" data-tab="'+id+'">'+label+'</button>').join("");
  tabs.querySelectorAll("button").forEach((button) => button.onclick = () => { active = button.dataset.tab; query = ""; search.value = ""; render(); });
}
function card(label, value, meta = "", cls = "") {
  return '<div class="card '+cls+'"><div class="label">'+esc(label)+'</div><div class="value">'+esc(value)+'</div><div class="meta">'+esc(meta)+'</div></div>';
}
function renderOverviewCards() {
  const s = data.summary;
  return '<div class="grid">' +
    card("Addressables Health", s.health + " / 100", healthText(s.health), "health") +
    card("Potential Savings", fmtBytes(s.potentialSavings), "Estimated duplicate waste", "warn") +
    card("Build Size", fmtBytes(s.totalSize), data.unityVersion || "") +
    card("Bundle Count", s.bundleCount, "Addressable bundles") +
    card("Asset Count", s.assetCount, "Tracked assets") +
    card("Duplicate Assets", s.duplicateCount, "Included in 2+ bundles", "warn") +
    card("Largest Bundle", s.largestBundle?.name || "None", fmtBytes(s.largestBundle?.size || 0)) +
    card("Largest Asset", s.largestAsset?.name || "None", fmtBytes(s.largestAsset?.size || 0)) +
  '</div>';
}
function table(columns, rows, rowHtml) {
  return '<table><thead><tr>' + columns.map((c) => '<th class="'+(c.num?'num':'')+'">'+esc(c.label)+'</th>').join("") + '</tr></thead><tbody>' + rows.map(rowHtml).join("") + '</tbody></table>';
}
function panel(title, body, meta = "") {
  return '<div class="panel-head"><span>'+esc(title)+'</span><span class="meta">'+esc(meta)+'</span></div>' + body;
}
function severity(value) { return '<span class="pill '+esc(value)+'">'+esc(String(value).toUpperCase())+'</span>'; }
function renderTableView() {
  aiPanel.classList.add("hidden");
  tablePanel.classList.remove("hidden");
  if (active === "overview") {
    const rows = data.duplicates.slice(0, 10);
    tablePanel.innerHTML = panel("Top Duplicate Assets", table([
      {label:"Asset"}, {label:"Waste", num:true}, {label:"Bundles", num:true}, {label:"Severity"}
    ], rows, (r) => '<tr><td title="'+esc(r.path)+'">'+esc(r.name)+'</td><td class="num danger">'+fmtBytes(r.waste)+'</td><td class="num">'+r.bundleCount+'</td><td>'+severity(r.severity)+'</td></tr>'));
    return;
  }
  if (active === "optimization") {
    const rows = filtered(data.optimizationCandidates, ["asset", "path", "recommendation"]).slice(0, 80);
    if (!selectedCandidate && rows.length) selectedCandidate = rows[0];
    tablePanel.innerHTML = '<div class="split"><div class="panel">' + panel("Optimization Candidates", table([
      {label:"Asset"}, {label:"Bundles", num:true}, {label:"Waste", num:true}, {label:"Savings", num:true}, {label:"Severity"}, {label:"Recommendation"}
    ], rows, (r, i) => '<tr data-path="'+esc(r.path)+'"><td title="'+esc(r.path)+'">'+esc(r.asset)+'</td><td class="num">'+r.bundles+'</td><td class="num danger">'+fmtBytes(r.waste)+'</td><td class="num danger">'+fmtBytes(r.savings)+'</td><td>'+severity(r.severity)+'</td><td>'+esc(r.recommendation)+'</td></tr>')) +
    '</div><aside class="panel detail"><strong>Selected Candidate</strong><div id="candidateDetail"></div></aside></div>';
    tablePanel.querySelectorAll("tbody tr").forEach((tr, index) => tr.onclick = () => { selectedCandidate = rows[index]; updateCandidate(); });
    updateCandidate();
    return;
  }
  if (active === "bundles") {
    const rows = filtered(data.bundles, ["name", "loadPath"]).slice(0, 120);
    tablePanel.innerHTML = panel("Bundles", table([{label:"Bundle"}, {label:"Size", num:true}, {label:"Assets", num:true}, {label:"Dependencies", num:true}], rows, (r) => '<tr><td title="'+esc(r.name)+'">'+esc(r.name)+'</td><td class="num">'+fmtBytes(r.size)+'</td><td class="num">'+r.assetCount+'</td><td class="num">'+(r.dependencies?.length || 0)+'</td></tr>'), rows.length + " shown");
    return;
  }
  if (active === "assets") {
    const rows = filtered(data.assets, ["name", "path"]).slice(0, 140);
    tablePanel.innerHTML = panel("Assets", table([{label:"Asset"}, {label:"Path"}, {label:"Size", num:true}, {label:"Bundles", num:true}], rows, (r) => '<tr><td>'+esc(r.name)+'</td><td title="'+esc(r.path)+'">'+esc(r.path)+'</td><td class="num">'+fmtBytes(r.size)+'</td><td class="num">'+r.bundleCount+'</td></tr>'), rows.length + " shown");
    return;
  }
  if (active === "duplicates") {
    const rows = filtered(data.duplicates, ["name", "path", "severity"]).slice(0, 140);
    tablePanel.innerHTML = panel("Duplicate Assets", table([{label:"Asset"}, {label:"Path"}, {label:"Waste", num:true}, {label:"Bundles", num:true}, {label:"Severity"}], rows, (r) => '<tr><td>'+esc(r.name)+'</td><td title="'+esc(r.path)+'">'+esc(r.path)+'</td><td class="num danger">'+fmtBytes(r.waste)+'</td><td class="num">'+r.bundleCount+'</td><td>'+severity(r.severity)+'</td></tr>'), rows.length + " shown");
    return;
  }
  if (active === "dependencies") {
    const rows = filtered(data.dependencySamples, ["name", "path"]).slice(0, 140);
    tablePanel.innerHTML = panel("Dependency Hotspots", table([{label:"Asset"}, {label:"Path"}, {label:"Dependencies", num:true}, {label:"Referenced By", num:true}, {label:"Bundles", num:true}], rows, (r) => '<tr><td>'+esc(r.name)+'</td><td title="'+esc(r.path)+'">'+esc(r.path)+'</td><td class="num">'+r.dependencyCount+'</td><td class="num">'+r.referencedByCount+'</td><td class="num">'+r.bundleCount+'</td></tr>'), rows.length + " shown");
  }
}
function updateCandidate() {
  const target = document.getElementById("candidateDetail");
  if (!target || !selectedCandidate) return;
  target.innerHTML = '<p><strong>'+esc(selectedCandidate.asset)+'</strong></p><p>'+esc(selectedCandidate.path)+'</p><p>Potential savings: <b>'+fmtBytes(selectedCandidate.savings)+'</b></p><p>Bundles: '+selectedCandidate.bundles+'</p><p>'+severity(selectedCandidate.severity)+'</p><p>Recommended action: '+esc(selectedCandidate.recommendation)+'. Validate loading behavior after moving this asset.</p>';
}
function aiLineItems(rows, mapper, emptyText) {
  if (!rows.length) return ["- " + emptyText];
  return rows.map((row, index) => (index + 1) + ". " + mapper(row));
}
function buildAiPrompt(mode) {
  const type = aiReportTypes[mode] || aiReportTypes.OptimizationConsultant;
  const summary = data.summary;
  const candidates = data.optimizationCandidates.slice(0, mode === "QuickReview" ? 8 : 18);
  const duplicates = data.duplicates.slice(0, mode === "DuplicateInvestigation" ? 25 : 12);
  const bundles = data.bundles.slice().sort((a, b) => (b.size || 0) - (a.size || 0)).slice(0, 12);
  const dependencies = data.dependencySamples.slice().sort((a, b) => (b.dependencyCount || 0) - (a.dependencyCount || 0)).slice(0, 12);
  const lines = [
    "Addressables Build Inspector AI Report",
    "",
    "Report type: " + type.label,
    "Focus: " + type.focus,
    "",
    "Build summary:",
    "- Source: " + data.sourceName,
    "- Unity version: " + (data.unityVersion || "unknown"),
    "- Build size: " + fmtBytes(summary.totalSize),
    "- Bundles: " + summary.bundleCount,
    "- Assets: " + summary.assetCount,
    "- Duplicate assets: " + summary.duplicateCount,
    "- Potential duplicate savings: " + fmtBytes(summary.potentialSavings),
    "- Health score: " + summary.health + " / 100",
    "- Largest bundle: " + (summary.largestBundle?.name || "None") + " (" + fmtBytes(summary.largestBundle?.size || 0) + ")",
    "- Largest asset: " + (summary.largestAsset?.path || summary.largestAsset?.name || "None") + " (" + fmtBytes(summary.largestAsset?.size || 0) + ")",
    "",
    "Top optimization candidates:",
    ...aiLineItems(candidates, (c) => c.path + " | waste " + fmtBytes(c.waste) + " | savings " + fmtBytes(c.savings) + " | bundles " + c.bundles + " | severity " + c.severity + " | " + c.recommendation, "No optimization candidates were found."),
    "",
    "Top duplicate assets:",
    ...aiLineItems(duplicates, (d) => d.path + " | waste " + fmtBytes(d.waste) + " | bundles " + d.bundleCount + " | severity " + d.severity, "No duplicated assets were found."),
    "",
    "Largest bundles:",
    ...aiLineItems(bundles, (b) => b.name + " | size " + fmtBytes(b.size) + " | assets " + b.assetCount + " | dependencies " + (b.dependencies?.length || 0), "No bundle data was found."),
    "",
    "Dependency hotspots:",
    ...aiLineItems(dependencies, (d) => d.path + " | dependencies " + d.dependencyCount + " | referenced by " + d.referencedByCount + " | bundles " + d.bundleCount, "No dependency hotspot data was found."),
    ""
  ];
  if (mode === "QuickReview") {
    lines.push("Output:", "1. Three-second value summary.", "2. Top 5 issues by ROI.", "3. Immediate next actions.", "4. Validation checklist.");
  } else if (mode === "DuplicateInvestigation") {
    lines.push("Output:", "1. Duplicate root-cause hypotheses.", "2. Shared bundle candidates and cases to avoid.", "3. Risk-ranked fixes.", "4. Validation steps for loading behavior and memory.");
  } else if (mode === "TechnicalAudit") {
    lines.push("Output:", "1. Asset Store rejection risks.", "2. Architecture and maintainability concerns.", "3. UI/UX and documentation gaps.", "4. Performance, memory allocation, and error handling risks.", "5. Refactoring recommendations that do not change public functionality.");
  } else if (mode === "FixPlan") {
    lines.push("Analyze the following Addressables optimization report.", "", "Create a step-by-step implementation plan.", "", "Requirements:", "- Minimize project risk.", "- Preserve current loading behavior.", "- Avoid unnecessary shared bundles.", "- Prioritize highest ROI changes.", "", "Output:", "1. Immediate fixes.", "2. Medium priority fixes.", "3. Long-term improvements.", "4. Validation checklist after each change.", "5. Rollback strategy.");
  } else {
    lines.push("Output:", "1. Immediate fixes.", "2. Medium priority fixes.", "3. Long-term improvements.", "4. Validation checklist after each change.", "5. Rollback strategy.");
  }
  return lines.join("\\n");
}
function downloadText(filename, text, mime) {
  const blob = new Blob([text], { type: mime || "text/plain" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
function renderAi() {
  tablePanel.classList.add("hidden");
  aiPanel.classList.remove("hidden");
  const type = aiReportTypes[aiMode] || aiReportTypes.OptimizationConsultant;
  const text = generatedAiPrompt || "Choose a report type and click Generate Report. The demo creates an AI-ready prompt locally from the loaded Addressables build layout.";
  aiPanel.innerHTML = '<div class="panel-head"><span>AI Report</span><span class="meta">'+esc(data.sourceName)+'</span></div>' +
    '<div class="ai-controls"><label class="label" for="aiReportType">Report Type</label><select id="aiReportType">' +
    Object.keys(aiReportTypes).map((key) => '<option value="'+key+'" '+(key===aiMode?'selected':'')+'>'+esc(aiReportTypes[key].label)+'</option>').join("") +
    '</select><div class="ai-description" id="aiDescription">'+esc(type.description)+'</div><button class="btn primary" id="generateAi">Generate Report</button><button class="btn" id="copyAi">Copy</button><button class="btn" id="exportAi">Export TXT</button></div>' +
    '<textarea class="ai" readonly>'+esc(text)+'</textarea>';
  document.getElementById("aiReportType").onchange = (event) => { aiMode = event.target.value; generatedAiPrompt = ""; renderAi(); };
  document.getElementById("generateAi").onclick = () => { generatedAiPrompt = buildAiPrompt(aiMode); statusLabel.textContent = "Generated " + aiReportTypes[aiMode].label + " report prompt."; renderAi(); };
  document.getElementById("copyAi").onclick = () => {
    if (!generatedAiPrompt) generatedAiPrompt = buildAiPrompt(aiMode);
    navigator.clipboard?.writeText(generatedAiPrompt);
    statusLabel.textContent = "Copied " + aiReportTypes[aiMode].label + " report prompt.";
    renderAi();
  };
  document.getElementById("exportAi").onclick = () => {
    if (!generatedAiPrompt) generatedAiPrompt = buildAiPrompt(aiMode);
    downloadText("addressables-ai-report-" + aiMode + ".txt", generatedAiPrompt, "text/plain");
    statusLabel.textContent = "Exported " + aiReportTypes[aiMode].label + " report prompt.";
    renderAi();
  };
}
function render() {
  renderTabs();
  const view = views.find(([id]) => id === active);
  viewSubtitle.textContent = view[2];
  statusLabel.textContent = "Loaded " + data.summary.bundleCount + " bundles and " + data.summary.assetCount + " assets from " + data.sourceName + ".";
  sourceLabel.textContent = data.sourceName + " · Unity " + (data.unityVersion || "unknown") + " · " + data.summary.bundleCount + " bundles";
  overview.innerHTML = active === "overview" || active === "optimization" ? renderOverviewCards() : "";
  if (active === "ai") renderAi(); else renderTableView();
}
search.oninput = () => { query = search.value; renderTableView(); };
loadBtn.onclick = () => fileInput.click();
resetBtn.onclick = () => { data = DEMO_DATA; active = "optimization"; query = ""; selectedCandidate = null; aiMode = "OptimizationConsultant"; generatedAiPrompt = ""; search.value = ""; render(); };
fileInput.onchange = async () => {
  const file = fileInput.files[0];
  if (!file) return;
  const text = await file.text();
  data = parseBuildLayoutClient(text, file.name);
  active = "optimization"; query = ""; selectedCandidate = null; generatedAiPrompt = ""; search.value = ""; render();
};
function parseBuildLayoutClient(text, sourceName) {
  return parseBuildLayout(text, sourceName);
}
render();
`;
}
