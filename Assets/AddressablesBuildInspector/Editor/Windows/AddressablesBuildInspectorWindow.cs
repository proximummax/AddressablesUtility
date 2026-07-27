using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AddressablesBuildInspector.Editor.AI;
using AddressablesBuildInspector.Editor.Analysis;
using AddressablesBuildInspector.Editor.Dependencies;
using AddressablesBuildInspector.Editor.Diff;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Optimization;
using AddressablesBuildInspector.Editor.Parsing;
using AddressablesBuildInspector.Editor.Services;
using AddressablesBuildInspector.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AddressablesBuildInspector.Editor.Windows
{
    /// <summary>
    /// Dockable editor window for inspecting Addressables build layout reports.
    /// </summary>
    public sealed class AddressablesBuildInspectorWindow : EditorWindow
    {
        private const string WindowTitle = "Addressables Build Inspector";
        private const string UxmlPath = "Assets/AddressablesBuildInspector/Editor/UI/AddressablesBuildInspectorWindow.uxml";
        private const string UssPath = "Assets/AddressablesBuildInspector/Editor/UI/AddressablesBuildInspectorWindow.uss";
        private const string LastReportPathEditorPrefsKey = "AddressablesBuildInspector.LastReportPath";
        private const int LoadingAnimationIntervalMs = 33;

        private readonly List<BundleData> _allBundles = new List<BundleData>();
        private readonly List<BundleData> _visibleBundles = new List<BundleData>();
        private readonly List<AssetData> _allAssets = new List<AssetData>();
        private readonly List<AssetData> _visibleAssets = new List<AssetData>();
        private readonly List<DuplicateAssetData> _allDuplicates = new List<DuplicateAssetData>();
        private readonly List<DuplicateAssetData> _visibleDuplicates = new List<DuplicateAssetData>();
        private readonly List<DependencyNode> _dependencySearchResults = new List<DependencyNode>();
        private readonly List<OptimizationCandidate> _visibleOptimizationCandidates = new List<OptimizationCandidate>();
        private readonly List<BundleDiff> _visibleBundleDiffs = new List<BundleDiff>();
        private readonly List<AssetDiff> _visibleAssetDiffs = new List<AssetDiff>();

        private BuildReportParser _parser;
        private BuildLayoutReportLoader _loader;
        private BuildDiffService _buildDiffService;
        private DiffExportService _diffExportService;
        private DuplicateOptimizationService _optimizationService;
        private OptimizationReportExportService _optimizationExportService;
        private AiPromptGenerator _aiPromptGenerator;
        private AiPromptExporter _aiPromptExporter;
        private BuildReportData _report;
        private BuildDiffReport _diffReport;
        private OptimizationReport _optimizationReport;
        private DependencyGraphService _dependencyGraphService;
        private DependencySearchService _dependencySearchService;
        private DependencyTreeBuilder _dependencyTreeBuilder;
        private ActiveTab _activeTab = ActiveTab.Overview;
        private string _searchQuery = string.Empty;

        private Label _statusLabel;
        private Label _sourceLabel;
        private TextField _searchField;
        private VisualElement _tabBar;
        private VisualElement _overviewContainer;
        private VisualElement _bundlesContainer;
        private VisualElement _assetsContainer;
        private VisualElement _duplicatesContainer;
        private VisualElement _optimizationContainer;
        private VisualElement _dependenciesContainer;
        private VisualElement _buildDiffContainer;
        private VisualElement _aiReportContainer;
        private Button _overviewTabButton;
        private Button _bundlesTabButton;
        private Button _assetsTabButton;
        private Button _duplicatesTabButton;
        private Button _optimizationTabButton;
        private Button _dependenciesTabButton;
        private Button _buildDiffTabButton;
        private Button _aiReportTabButton;
        private Label _bundlesEmptyState;
        private Label _assetsEmptyState;
        private Label _duplicatesEmptyState;
        private ListView _bundlesListView;
        private ListView _assetsListView;
        private ListView _duplicatesListView;
        private VisualElement _optimizationDashboardContainer;
        private VisualElement _optimizationInsightsContainer;
        private VisualElement _optimizationDetailsContainer;
        private ListView _optimizationListView;
        private Label _optimizationEmptyState;
        private TextField _dependencySearchField;
        private ListView _dependencySearchListView;
        private VisualElement _dependencyTreeContainer;
        private VisualElement _dependencyDetailsContainer;
        private Label _dependencyEmptyState;
        private Label _oldBuildPathLabel;
        private Label _newBuildPathLabel;
        private VisualElement _diffOverviewContainer;
        private VisualElement _diffInsightsContainer;
        private VisualElement _diffGrowthDetailsContainer;
        private PopupField<string> _diffFilterField;
        private ListView _bundleDiffListView;
        private ListView _assetDiffListView;
        private Label _bundleDiffEmptyState;
        private Label _assetDiffEmptyState;
        private PopupField<AiReportMode> _aiReportModeField;
        private Label _aiReportModeDescriptionLabel;
        private TextField _aiReportPreviewField;
        private VisualElement _aiReportOutputContainer;
        private ScrollView _aiReportScrollView;
        private string _generatedAiPrompt = string.Empty;
        private string _oldBuildPath = string.Empty;
        private string _newBuildPath = string.Empty;

        private BundleSortColumn _bundleSortColumn = BundleSortColumn.Size;
        private AssetSortColumn _assetSortColumn = AssetSortColumn.Size;
        private DuplicateSortColumn _duplicateSortColumn = DuplicateSortColumn.Waste;
        private OptimizationSortColumn _optimizationSortColumn = OptimizationSortColumn.Savings;
        private bool _bundleSortAscending;
        private bool _assetSortAscending;
        private bool _duplicateSortAscending;
        private bool _optimizationSortAscending;
        private bool _optimizationGenerationQueued;
        private bool _optimizationGenerationInProgress;
        private bool _dependencyLoadQueued;
        private bool _dependencyLoadInProgress;
        private Task<BuildLayoutParseResult> _dependencyLoadTask;
        private string _dependencyLoadSourcePath = string.Empty;
        private bool _aiReportGenerationQueued;
        private bool _aiReportGenerationInProgress;
        private bool _generateAiReportAfterDependencyLoad;
        private Task<AiReportGenerationResult> _aiReportGenerationTask;
        private string _aiReportGenerationSourcePath = string.Empty;
        private AiReportMode _queuedAiReportMode;

        private enum ActiveTab
        {
            Overview,
            Bundles,
            Assets,
            Duplicates,
            Dependencies,
            BuildDiff,
            Optimization,
            AIReport
        }

        private enum BundleSortColumn
        {
            Name,
            Size,
            AssetCount
        }

        private enum AssetSortColumn
        {
            Name,
            Path,
            Size,
            BundleCount
        }

        private enum DuplicateSortColumn
        {
            Name,
            BundleCount,
            Size,
            Waste
        }

        private enum OptimizationSortColumn
        {
            Asset,
            BundleCount,
            Waste,
            Savings,
            Severity
        }

        /// <summary>
        /// Opens the inspector window.
        /// </summary>
        [MenuItem("Tools/Addressables Build Inspector")]
        public static void Open()
        {
            var window = GetWindow<AddressablesBuildInspectorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(760, 420);
            window.Show();
        }

        /// <summary>
        /// Builds the UI Toolkit visual tree.
        /// </summary>
        public void CreateGUI()
        {
            titleContent = new GUIContent(WindowTitle);
            _parser = new BuildReportParser();
            _loader = new BuildLayoutReportLoader(_parser);
            _buildDiffService = new BuildDiffService(_parser);
            _diffExportService = new DiffExportService();
            _optimizationExportService = new OptimizationReportExportService();
            _aiPromptGenerator = new AiPromptGenerator();
            _aiPromptExporter = new AiPromptExporter();

            rootVisualElement.Clear();
            LoadVisualTree();
            CacheElements();
            BuildToolbar();
            BuildTabs();
            BuildTables();
            SetActiveTab(ActiveTab.Overview);
            ShowStatus("Load a Build Layout report to begin.", StatusKind.Neutral);
            RenderOverview();
            RefreshAllTables();
            rootVisualElement.schedule.Execute(RestoreLastReportIfAvailable);
        }

        private void LoadVisualTree()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree != null)
            {
                visualTree.CloneTree(rootVisualElement);
            }
            else
            {
                BuildFallbackVisualTree();
            }

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
        }

        private void BuildFallbackVisualTree()
        {
            var root = new VisualElement { name = "abi-root" };
            root.AddToClassList("abi-root");

            root.Add(new VisualElement { name = "abi-toolbar" });
            root.Add(new Label { name = "abi-status-label" });
            root.Add(new VisualElement { name = "abi-tab-bar" });

            var content = new VisualElement { name = "abi-content" };
            content.Add(new VisualElement { name = "abi-overview-tab" });
            content.Add(new VisualElement { name = "abi-bundles-tab" });
            content.Add(new VisualElement { name = "abi-assets-tab" });
            content.Add(new VisualElement { name = "abi-duplicates-tab" });
            content.Add(new VisualElement { name = "abi-dependencies-tab" });
            content.Add(new VisualElement { name = "abi-build-diff-tab" });
            content.Add(new VisualElement { name = "abi-optimization-tab" });
            content.Add(new VisualElement { name = "abi-ai-report-tab" });
            root.Add(content);

            rootVisualElement.Add(root);
        }

        private void CacheElements()
        {
            _statusLabel = rootVisualElement.Q<Label>("abi-status-label");
            _tabBar = rootVisualElement.Q<VisualElement>("abi-tab-bar");
            _overviewContainer = rootVisualElement.Q<VisualElement>("abi-overview-tab");
            _bundlesContainer = rootVisualElement.Q<VisualElement>("abi-bundles-tab");
            _assetsContainer = rootVisualElement.Q<VisualElement>("abi-assets-tab");
            _duplicatesContainer = rootVisualElement.Q<VisualElement>("abi-duplicates-tab");
            _optimizationContainer = rootVisualElement.Q<VisualElement>("abi-optimization-tab");
            _dependenciesContainer = rootVisualElement.Q<VisualElement>("abi-dependencies-tab");
            _buildDiffContainer = rootVisualElement.Q<VisualElement>("abi-build-diff-tab");
            _aiReportContainer = rootVisualElement.Q<VisualElement>("abi-ai-report-tab");
        }

        private void BuildToolbar()
        {
            VisualElement toolbar = rootVisualElement.Q<VisualElement>("abi-toolbar");
            toolbar.Clear();
            toolbar.AddToClassList("abi-toolbar");

            var loadButton = new Button(LoadReport)
            {
                text = "Load Report",
                tooltip = "Select an Addressables Build Layout text or JSON report."
            };
            loadButton.AddToClassList("abi-load-button");

            var clearButton = new Button(ClearReport)
            {
                text = "Clear",
                tooltip = "Reset the inspector to its default empty state."
            };
            clearButton.AddToClassList("abi-clear-button");

            var titleBlock = new VisualElement();
            titleBlock.AddToClassList("abi-title-block");
            var title = new Label("Addressables Build Inspector");
            title.AddToClassList("abi-window-title");
            var subtitle = new Label("Build size, duplicate waste, dependencies, and optimization ROI");
            subtitle.AddToClassList("abi-window-subtitle");
            titleBlock.Add(title);
            titleBlock.Add(subtitle);

            _sourceLabel = new Label("No report loaded");
            _sourceLabel.AddToClassList("abi-source-label");

            _searchField = new TextField
            {
                value = string.Empty,
                tooltip = "Search the active table."
            };
            _searchField.AddToClassList("abi-search-field");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue ?? string.Empty;
                RefreshActiveTable();
            });

            toolbar.Add(loadButton);
            toolbar.Add(clearButton);
            toolbar.Add(titleBlock);
            toolbar.Add(_sourceLabel);
            toolbar.Add(_searchField);
        }

        private void BuildTabs()
        {
            _tabBar.Clear();
            _tabBar.AddToClassList("abi-tab-bar");

            _overviewTabButton = CreateTabButton("Overview", ActiveTab.Overview);
            _bundlesTabButton = CreateTabButton("Bundles", ActiveTab.Bundles);
            _assetsTabButton = CreateTabButton("Assets", ActiveTab.Assets);
            _duplicatesTabButton = CreateTabButton("Duplicates", ActiveTab.Duplicates);
            _dependenciesTabButton = CreateTabButton("Dependencies", ActiveTab.Dependencies);
            _buildDiffTabButton = CreateTabButton("Build Diff", ActiveTab.BuildDiff);
            _optimizationTabButton = CreateTabButton("Optimization", ActiveTab.Optimization);
            _aiReportTabButton = CreateTabButton("AI Report", ActiveTab.AIReport);

            _tabBar.Add(_overviewTabButton);
            _tabBar.Add(_bundlesTabButton);
            _tabBar.Add(_assetsTabButton);
            _tabBar.Add(_duplicatesTabButton);
            _tabBar.Add(_dependenciesTabButton);
            _tabBar.Add(_buildDiffTabButton);
            _tabBar.Add(_optimizationTabButton);
            _tabBar.Add(_aiReportTabButton);
        }

        private Button CreateTabButton(string title, ActiveTab tab)
        {
            var button = new Button(() => SetActiveTab(tab))
            {
                text = title
            };
            button.AddToClassList("abi-tab-button");
            return button;
        }

        private void BuildTables()
        {
            BuildBundlesTable();
            BuildAssetsTable();
            BuildDuplicatesTable();
            BuildOptimizationView();
            BuildDependenciesView();
            BuildDiffView();
            BuildAiReportView();
        }

        private void BuildBundlesTable()
        {
            _bundlesContainer.Clear();
            _bundlesContainer.Add(TableBuilder.CreateHeader(
                new TableColumnDefinition("Bundle Name", 5f, () => ToggleBundleSort(BundleSortColumn.Name), "Sort by bundle name"),
                new TableColumnDefinition("Size", 1.4f, () => ToggleBundleSort(BundleSortColumn.Size), "Sort by bundle size", numeric: true),
                new TableColumnDefinition("Asset Count", 1.2f, () => ToggleBundleSort(BundleSortColumn.AssetCount), "Sort by asset count", numeric: true)));

            _bundlesEmptyState = TableBuilder.CreateEmptyState("Load a report to inspect bundles.");
            _bundlesContainer.Add(_bundlesEmptyState);

            _bundlesListView = CreateListView(_visibleBundles, MakeBundleRow, BindBundleRow);
            _bundlesContainer.Add(_bundlesListView);
        }

        private void BuildAssetsTable()
        {
            _assetsContainer.Clear();
            _assetsContainer.Add(TableBuilder.CreateHeader(
                new TableColumnDefinition("Asset Name", 2f, () => ToggleAssetSort(AssetSortColumn.Name), "Sort by asset name"),
                new TableColumnDefinition("Path", 5f, () => ToggleAssetSort(AssetSortColumn.Path), "Sort by asset path"),
                new TableColumnDefinition("Size", 1.2f, () => ToggleAssetSort(AssetSortColumn.Size), "Sort by asset size", numeric: true),
                new TableColumnDefinition("Bundles", 1f, () => ToggleAssetSort(AssetSortColumn.BundleCount), "Sort by bundle count", numeric: true)));

            _assetsEmptyState = TableBuilder.CreateEmptyState("Load a report to inspect assets.");
            _assetsContainer.Add(_assetsEmptyState);

            _assetsListView = CreateListView(_visibleAssets, MakeAssetRow, BindAssetRow);
            _assetsContainer.Add(_assetsListView);
        }

        private void BuildDuplicatesTable()
        {
            _duplicatesContainer.Clear();
            _duplicatesContainer.Add(TableBuilder.CreateHeader(
                new TableColumnDefinition("Asset Name", 2f, () => ToggleDuplicateSort(DuplicateSortColumn.Name), "Sort by asset name"),
                new TableColumnDefinition("Bundle Count", 1.2f, () => ToggleDuplicateSort(DuplicateSortColumn.BundleCount), "Sort by bundle count", numeric: true),
                new TableColumnDefinition("Size", 1.2f, () => ToggleDuplicateSort(DuplicateSortColumn.Size), "Sort by asset size", numeric: true),
                new TableColumnDefinition("Estimated Waste", 1.4f, () => ToggleDuplicateSort(DuplicateSortColumn.Waste), "Sort by estimated duplicate waste", numeric: true)));

            _duplicatesEmptyState = TableBuilder.CreateEmptyState("No duplicate assets found in the loaded report.");
            _duplicatesContainer.Add(_duplicatesEmptyState);

            _duplicatesListView = CreateListView(_visibleDuplicates, MakeDuplicateRow, BindDuplicateRow);
            _duplicatesContainer.Add(_duplicatesListView);
        }

        private void BuildOptimizationView()
        {
            _optimizationContainer.Clear();

            var controls = new VisualElement();
            controls.AddToClassList("abi-optimization-controls");

            var exportCsvButton = new Button(() => ExportOptimization("csv")) { text = "Export CSV" };
            var exportJsonButton = new Button(() => ExportOptimization("json")) { text = "Export JSON" };
            var exportMarkdownButton = new Button(() => ExportOptimization("md")) { text = "Export Markdown" };
            var fixPlanPromptButton = new Button(GenerateFixPlanPrompt) { text = "Generate Fix Plan Prompt" };
            fixPlanPromptButton.tooltip = "Generate and copy an implementation-plan prompt for the current optimization report.";
            controls.Add(exportCsvButton);
            controls.Add(exportJsonButton);
            controls.Add(exportMarkdownButton);
            controls.Add(fixPlanPromptButton);
            _optimizationContainer.Add(controls);

            _optimizationDashboardContainer = new VisualElement();
            _optimizationDashboardContainer.AddToClassList("abi-overview-grid");
            _optimizationDashboardContainer.AddToClassList("abi-kpi-row");
            _optimizationContainer.Add(_optimizationDashboardContainer);

            var insightsScrollView = new ScrollView();
            insightsScrollView.AddToClassList("abi-optimization-insights-scroll");
            _optimizationInsightsContainer = new VisualElement();
            _optimizationInsightsContainer.AddToClassList("abi-optimization-insights");
            insightsScrollView.Add(_optimizationInsightsContainer);
            _optimizationContainer.Add(insightsScrollView);

            var tableLayout = new VisualElement();
            tableLayout.AddToClassList("abi-optimization-layout");

            var candidatePanel = new VisualElement();
            candidatePanel.AddToClassList("abi-optimization-table-panel");
            candidatePanel.Add(CreateSectionTitle("Optimization Candidates", "Ranked by duplicate waste, potential savings, severity, and implementation ROI."));
            candidatePanel.Add(TableBuilder.CreateHeader(
                new TableColumnDefinition("Asset", 3f, () => ToggleOptimizationSort(OptimizationSortColumn.Asset), "Sort by asset"),
                new TableColumnDefinition("Bundles", 0.9f, () => ToggleOptimizationSort(OptimizationSortColumn.BundleCount), "Sort by bundle count", numeric: true),
                new TableColumnDefinition("Waste", 1.1f, () => ToggleOptimizationSort(OptimizationSortColumn.Waste), "Sort by duplicate waste", numeric: true),
                new TableColumnDefinition("Savings", 1.1f, () => ToggleOptimizationSort(OptimizationSortColumn.Savings), "Sort by potential savings", numeric: true),
                new TableColumnDefinition("Severity", 1f, () => ToggleOptimizationSort(OptimizationSortColumn.Severity), "Sort by severity"),
                new TableColumnDefinition("Recommendation", 1.6f, () => ToggleOptimizationSort(OptimizationSortColumn.Savings), "Sort by potential savings")));

            _optimizationEmptyState = TableBuilder.CreateEmptyState("Load a report to inspect duplicate optimization candidates.");
            candidatePanel.Add(_optimizationEmptyState);
            _optimizationListView = CreateListView(_visibleOptimizationCandidates, MakeOptimizationRow, BindOptimizationRow);
            _optimizationListView.selectionType = SelectionType.Single;
            _optimizationListView.selectionChanged += OnOptimizationSelectionChanged;
            candidatePanel.Add(_optimizationListView);

            var detailsScrollView = new ScrollView();
            detailsScrollView.AddToClassList("abi-optimization-details-scroll");
            _optimizationDetailsContainer = new VisualElement();
            _optimizationDetailsContainer.AddToClassList("abi-optimization-details");
            _optimizationDetailsContainer.Add(CreateSectionTitle("Candidate Details", "Select a row to inspect cause, affected bundles, and dependency paths."));
            _optimizationDetailsContainer.Add(TableBuilder.CreateEmptyState("Select a candidate to inspect cause, dependency paths, and simulated savings."));
            detailsScrollView.Add(_optimizationDetailsContainer);

            tableLayout.Add(candidatePanel);
            tableLayout.Add(detailsScrollView);
            _optimizationContainer.Add(tableLayout);

            RenderOptimizationDashboard();
            RefreshOptimizationTable();
        }

        private void BuildDependenciesView()
        {
            _dependenciesContainer.Clear();

            var split = new VisualElement();
            split.AddToClassList("abi-dependency-layout");

            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("abi-dependency-panel");
            leftPanel.Add(CreatePaneHeading("Assets"));

            _dependencySearchField = new TextField("Search")
            {
                tooltip = "Search dependencies by asset name or path."
            };
            _dependencySearchField.RegisterValueChangedCallback(_ => RefreshDependencySearch());
            leftPanel.Add(_dependencySearchField);

            _dependencyEmptyState = TableBuilder.CreateEmptyState("Load a report to search dependency assets.");
            leftPanel.Add(_dependencyEmptyState);

            _dependencySearchListView = CreateListView(_dependencySearchResults, MakeDependencySearchRow, BindDependencySearchRow);
            _dependencySearchListView.selectionType = SelectionType.Single;
            _dependencySearchListView.selectionChanged += OnDependencySearchSelectionChanged;
            leftPanel.Add(_dependencySearchListView);

            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("abi-dependency-detail-layout");

            var treePanel = new VisualElement();
            treePanel.AddToClassList("abi-dependency-section");
            treePanel.Add(CreatePaneHeading("Dependency Tree"));
            var treeScrollView = new ScrollView();
            treeScrollView.AddToClassList("abi-dependency-scroll");
            _dependencyTreeContainer = new VisualElement();
            _dependencyTreeContainer.AddToClassList("abi-dependency-tree");
            treeScrollView.Add(_dependencyTreeContainer);
            treePanel.Add(treeScrollView);

            var detailsPanel = new VisualElement();
            detailsPanel.AddToClassList("abi-dependency-section");
            detailsPanel.Add(CreatePaneHeading("Details"));
            var detailsScrollView = new ScrollView();
            detailsScrollView.AddToClassList("abi-dependency-detail-scroll");
            _dependencyDetailsContainer = new VisualElement();
            _dependencyDetailsContainer.AddToClassList("abi-dependency-details");
            detailsScrollView.Add(_dependencyDetailsContainer);
            detailsPanel.Add(detailsScrollView);

            rightPanel.Add(treePanel);
            rightPanel.Add(detailsPanel);
            split.Add(leftPanel);
            split.Add(rightPanel);

            _dependenciesContainer.Add(split);
            ClearDependencySelection("Select an asset to inspect its dependency tree.");
        }

        private void BuildDiffView()
        {
            _buildDiffContainer.Clear();

            var controls = new VisualElement();
            controls.AddToClassList("abi-diff-controls");

            var oldButton = new Button(() => SelectDiffBuildPath(true)) { text = "Old Build Layout" };
            _oldBuildPathLabel = new Label("No old build selected");
            _oldBuildPathLabel.AddToClassList("abi-diff-path-label");

            var newButton = new Button(() => SelectDiffBuildPath(false)) { text = "New Build Layout" };
            _newBuildPathLabel = new Label("No new build selected");
            _newBuildPathLabel.AddToClassList("abi-diff-path-label");

            var compareButton = new Button(CompareDiffBuilds) { text = "Compare" };
            var exportCsvButton = new Button(() => ExportDiff("csv")) { text = "Export CSV" };
            var exportJsonButton = new Button(() => ExportDiff("json")) { text = "Export JSON" };

            _diffFilterField = new PopupField<string>("Filter", new List<string> { "All", "Changed", "Added", "Removed" }, 0);
            _diffFilterField.RegisterValueChangedCallback(_ => RefreshDiffTables());

            controls.Add(oldButton);
            controls.Add(_oldBuildPathLabel);
            controls.Add(newButton);
            controls.Add(_newBuildPathLabel);
            controls.Add(compareButton);
            controls.Add(_diffFilterField);
            controls.Add(exportCsvButton);
            controls.Add(exportJsonButton);
            _buildDiffContainer.Add(controls);

            _diffOverviewContainer = new VisualElement();
            _diffOverviewContainer.AddToClassList("abi-diff-overview");
            _buildDiffContainer.Add(_diffOverviewContainer);

            var diffInsightsScrollView = new ScrollView();
            diffInsightsScrollView.AddToClassList("abi-diff-insights-scroll");
            _diffInsightsContainer = new VisualElement();
            _diffInsightsContainer.AddToClassList("abi-diff-insights");
            diffInsightsScrollView.Add(_diffInsightsContainer);
            _buildDiffContainer.Add(diffInsightsScrollView);

            var tableLayout = new VisualElement();
            tableLayout.AddToClassList("abi-diff-table-layout");

            var bundlePanel = new VisualElement();
            bundlePanel.AddToClassList("abi-diff-table-panel");
            bundlePanel.Add(new Label("Bundle Diff"));
            bundlePanel.Add(TableBuilder.CreateHeader(
                new TableColumnDefinition("Bundle", 3f, RefreshDiffTables),
                new TableColumnDefinition("Old Size", 1f, RefreshDiffTables, numeric: true),
                new TableColumnDefinition("New Size", 1f, RefreshDiffTables, numeric: true),
                new TableColumnDefinition("Delta", 1f, RefreshDiffTables, numeric: true),
                new TableColumnDefinition("Status", 1f, RefreshDiffTables)));
            _bundleDiffEmptyState = TableBuilder.CreateEmptyState("Compare two reports to inspect bundle changes.");
            bundlePanel.Add(_bundleDiffEmptyState);
            _bundleDiffListView = CreateListView(_visibleBundleDiffs, MakeBundleDiffRow, BindBundleDiffRow);
            _bundleDiffListView.selectionType = SelectionType.Single;
            _bundleDiffListView.selectionChanged += OnBundleDiffSelectionChanged;
            bundlePanel.Add(_bundleDiffListView);

            var assetPanel = new VisualElement();
            assetPanel.AddToClassList("abi-diff-table-panel");
            assetPanel.Add(new Label("Asset Diff"));
            assetPanel.Add(TableBuilder.CreateHeader(
                new TableColumnDefinition("Asset", 3f, RefreshDiffTables),
                new TableColumnDefinition("Old Size", 1f, RefreshDiffTables, numeric: true),
                new TableColumnDefinition("New Size", 1f, RefreshDiffTables, numeric: true),
                new TableColumnDefinition("Delta", 1f, RefreshDiffTables, numeric: true),
                new TableColumnDefinition("Status", 1f, RefreshDiffTables)));
            _assetDiffEmptyState = TableBuilder.CreateEmptyState("Compare two reports to inspect asset changes.");
            assetPanel.Add(_assetDiffEmptyState);
            _assetDiffListView = CreateListView(_visibleAssetDiffs, MakeAssetDiffRow, BindAssetDiffRow);
            assetPanel.Add(_assetDiffListView);

            tableLayout.Add(bundlePanel);
            tableLayout.Add(assetPanel);
            _buildDiffContainer.Add(tableLayout);

            var growthScrollView = new ScrollView();
            growthScrollView.AddToClassList("abi-diff-growth-scroll");
            _diffGrowthDetailsContainer = new VisualElement();
            _diffGrowthDetailsContainer.AddToClassList("abi-diff-growth-details");
            growthScrollView.Add(_diffGrowthDetailsContainer);
            _buildDiffContainer.Add(growthScrollView);

            RenderDiffOverview();
            RefreshDiffTables();
        }

        private void BuildAiReportView()
        {
            _aiReportContainer.Clear();

            var header = new VisualElement();
            header.AddToClassList("abi-ai-header");

            var controls = new VisualElement();
            controls.AddToClassList("abi-ai-controls");

            _aiReportModeField = new PopupField<AiReportMode>(
                "Report Type",
                new List<AiReportMode>
                {
                    AiReportMode.QuickReview,
                    AiReportMode.OptimizationConsultant,
                    AiReportMode.BuildGrowthInvestigation,
                    AiReportMode.DuplicateInvestigation,
                    AiReportMode.TechnicalAudit
                },
                AiReportMode.OptimizationConsultant);
            _aiReportModeField.AddToClassList("abi-ai-mode-field");
            _aiReportModeField.RegisterValueChangedCallback(evt => UpdateAiReportModeDescription(evt.newValue));

            var generateButton = new Button(GenerateAiReport) { text = "Generate Report" };
            generateButton.AddToClassList("abi-ai-action-button");
            var copyButton = new Button(CopyAiReportToClipboard) { text = "Copy" };
            copyButton.AddToClassList("abi-ai-action-button");
            var exportTxtButton = new Button(() => ExportAiReport("txt")) { text = "Export TXT" };
            exportTxtButton.AddToClassList("abi-ai-action-button");
            var exportMarkdownButton = new Button(() => ExportAiReport("md")) { text = "Export Markdown" };
            exportMarkdownButton.AddToClassList("abi-ai-action-button-wide");

            controls.Add(_aiReportModeField);
            controls.Add(generateButton);
            controls.Add(copyButton);
            controls.Add(exportTxtButton);
            controls.Add(exportMarkdownButton);
            header.Add(controls);

            _aiReportModeDescriptionLabel = new Label();
            _aiReportModeDescriptionLabel.AddToClassList("abi-ai-description");
            header.Add(_aiReportModeDescriptionLabel);
            _aiReportContainer.Add(header);

            _aiReportPreviewField = new TextField
            {
                multiline = true,
                isReadOnly = true,
                value = "Load a Build Layout report, choose a report type, then click Generate Report."
            };
            _aiReportPreviewField.AddToClassList("abi-ai-preview");

            _aiReportOutputContainer = new VisualElement();
            _aiReportOutputContainer.AddToClassList("abi-ai-output");
            _aiReportScrollView = new ScrollView();
            _aiReportScrollView.AddToClassList("abi-ai-scroll");
            _aiReportScrollView.Add(_aiReportPreviewField);
            _aiReportOutputContainer.Add(_aiReportScrollView);
            _aiReportContainer.Add(_aiReportOutputContainer);
            UpdateAiReportModeDescription(_aiReportModeField.value);
        }

        private void UpdateAiReportModeDescription(AiReportMode mode)
        {
            if (_aiReportModeDescriptionLabel == null)
            {
                return;
            }

            AiPromptTemplate template = AiPromptTemplate.ForMode(mode);
            _aiReportModeDescriptionLabel.text = $"{template.Title}: {template.Description}";
        }

        private static ListView CreateListView(
            IList itemsSource,
            Func<VisualElement> makeItem,
            Action<VisualElement, int> bindItem)
        {
            var listView = new ListView
            {
                itemsSource = itemsSource,
                fixedItemHeight = 30,
                makeItem = makeItem,
                bindItem = bindItem,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };

            listView.AddToClassList("abi-list-view");
            return listView;
        }

        private VisualElement MakeBundleRow()
        {
            var row = TableBuilder.CreateRow();
            var cells = new BundleRowElements
            {
                Name = TableBuilder.CreateCell("bundle-name", 5f),
                Size = TableBuilder.CreateCell("bundle-size", 1.4f, true),
                AssetCount = TableBuilder.CreateCell("bundle-asset-count", 1.2f, true)
            };

            row.userData = cells;
            row.Add(cells.Name);
            row.Add(cells.Size);
            row.Add(cells.AssetCount);
            return row;
        }

        private void BindBundleRow(VisualElement row, int index)
        {
            var cells = (BundleRowElements)row.userData;
            BundleData bundle = _visibleBundles[index];

            cells.Name.text = bundle.Name;
            cells.Size.text = ByteFormatter.FormatBytes(bundle.SizeBytes);
            cells.AssetCount.text = bundle.AssetCount.ToString(CultureInfo.InvariantCulture);
        }

        private VisualElement MakeAssetRow()
        {
            var row = TableBuilder.CreateRow();
            var cells = new AssetRowElements
            {
                Name = TableBuilder.CreateCell("asset-name", 2f),
                Path = TableBuilder.CreateCell("asset-path", 5f),
                Size = TableBuilder.CreateCell("asset-size", 1.2f, true),
                BundleCount = TableBuilder.CreateCell("asset-bundle-count", 1f, true)
            };

            row.userData = cells;
            row.Add(cells.Name);
            row.Add(cells.Path);
            row.Add(cells.Size);
            row.Add(cells.BundleCount);
            return row;
        }

        private void BindAssetRow(VisualElement row, int index)
        {
            var cells = (AssetRowElements)row.userData;
            AssetData asset = _visibleAssets[index];

            cells.Name.text = asset.Name;
            cells.Path.text = asset.Path;
            cells.Size.text = ByteFormatter.FormatBytes(asset.SizeBytes);
            cells.BundleCount.text = asset.BundleCount.ToString(CultureInfo.InvariantCulture);
        }

        private VisualElement MakeDuplicateRow()
        {
            var row = TableBuilder.CreateRow();
            var cells = new DuplicateRowElements
            {
                Name = TableBuilder.CreateCell("duplicate-name", 2f),
                BundleCount = TableBuilder.CreateCell("duplicate-bundle-count", 1.2f, true),
                Size = TableBuilder.CreateCell("duplicate-size", 1.2f, true),
                Waste = TableBuilder.CreateCell("duplicate-waste", 1.4f, true)
            };

            cells.Waste.AddToClassList("abi-cell-danger");
            row.userData = cells;
            row.Add(cells.Name);
            row.Add(cells.BundleCount);
            row.Add(cells.Size);
            row.Add(cells.Waste);
            return row;
        }

        private void BindDuplicateRow(VisualElement row, int index)
        {
            var cells = (DuplicateRowElements)row.userData;
            DuplicateAssetData duplicate = _visibleDuplicates[index];

            row.EnableInClassList("abi-row-warning", index < 3);
            cells.Name.text = duplicate.Asset.Name;
            cells.BundleCount.text = duplicate.BundleCount.ToString(CultureInfo.InvariantCulture);
            cells.Size.text = ByteFormatter.FormatBytes(duplicate.Asset.SizeBytes);
            cells.Waste.text = ByteFormatter.FormatBytes(duplicate.EstimatedWasteBytes);
        }

        private VisualElement MakeOptimizationRow()
        {
            var row = TableBuilder.CreateRow();
            var cells = new OptimizationRowElements
            {
                Asset = TableBuilder.CreateCell("optimization-asset", 3f),
                BundleCount = TableBuilder.CreateCell("optimization-bundles", 0.9f, true),
                Waste = TableBuilder.CreateCell("optimization-waste", 1.1f, true),
                Savings = TableBuilder.CreateCell("optimization-savings", 1.1f, true),
                Severity = TableBuilder.CreateCell("optimization-severity", 1f),
                Recommendation = TableBuilder.CreateCell("optimization-recommendation", 1.6f)
            };

            cells.Waste.AddToClassList("abi-cell-danger");
            cells.Savings.AddToClassList("abi-cell-danger");
            cells.Severity.AddToClassList("abi-severity-pill");
            row.userData = cells;
            row.Add(cells.Asset);
            row.Add(cells.BundleCount);
            row.Add(cells.Waste);
            row.Add(cells.Savings);
            row.Add(cells.Severity);
            row.Add(cells.Recommendation);
            return row;
        }

        private void BindOptimizationRow(VisualElement row, int index)
        {
            var cells = (OptimizationRowElements)row.userData;
            OptimizationCandidate candidate = _visibleOptimizationCandidates[index];

            row.EnableInClassList("abi-row-warning", candidate.Severity == OptimizationSeverity.High || candidate.Severity == OptimizationSeverity.Critical);
            cells.Asset.text = candidate.Asset?.Name ?? "Unknown";
            cells.Asset.tooltip = candidate.Asset?.Path ?? string.Empty;
            cells.BundleCount.text = candidate.BundleCount.ToString(CultureInfo.InvariantCulture);
            cells.Waste.text = ByteFormatter.FormatBytes(candidate.DuplicateWasteBytes);
            cells.Savings.text = ByteFormatter.FormatBytes(candidate.Impact.PotentialSavingsBytes);
            SetSeverityPill(cells.Severity, candidate.Severity);
            cells.Recommendation.text = FormatRecommendation(candidate.Recommendation);
        }

        private void OnOptimizationSelectionChanged(IEnumerable<object> selectedItems)
        {
            OptimizationCandidate selectedCandidate = selectedItems.OfType<OptimizationCandidate>().FirstOrDefault();
            if (selectedCandidate != null)
            {
                RenderOptimizationDetails(selectedCandidate);
            }
        }

        private VisualElement MakeDependencySearchRow()
        {
            var row = TableBuilder.CreateRow();
            var cells = new DependencySearchRowElements
            {
                Name = TableBuilder.CreateCell("dependency-search-name", 2f),
                Path = TableBuilder.CreateCell("dependency-search-path", 4f),
                BundleCount = TableBuilder.CreateCell("dependency-search-bundles", 1f, true)
            };

            row.userData = cells;
            row.Add(cells.Name);
            row.Add(cells.Path);
            row.Add(cells.BundleCount);
            return row;
        }

        private void BindDependencySearchRow(VisualElement row, int index)
        {
            var cells = (DependencySearchRowElements)row.userData;
            DependencyNode node = _dependencySearchResults[index];

            cells.Name.text = node.AssetName;
            cells.Path.text = node.AssetPath;
            cells.BundleCount.text = node.BundleCount.ToString(CultureInfo.InvariantCulture);
        }

        private void OnDependencySearchSelectionChanged(IEnumerable<object> selectedItems)
        {
            DependencyNode selectedNode = selectedItems.OfType<DependencyNode>().FirstOrDefault();
            if (selectedNode != null)
            {
                ShowDependencyTree(selectedNode.AssetPath);
            }
        }

        private VisualElement MakeBundleDiffRow()
        {
            var row = TableBuilder.CreateRow();
            var cells = new BundleDiffRowElements
            {
                Name = TableBuilder.CreateCell("bundle-diff-name", 3f),
                OldSize = TableBuilder.CreateCell("bundle-diff-old", 1f, true),
                NewSize = TableBuilder.CreateCell("bundle-diff-new", 1f, true),
                Delta = TableBuilder.CreateCell("bundle-diff-delta", 1f, true),
                Status = TableBuilder.CreateCell("bundle-diff-status", 1f)
            };

            row.userData = cells;
            row.Add(cells.Name);
            row.Add(cells.OldSize);
            row.Add(cells.NewSize);
            row.Add(cells.Delta);
            row.Add(cells.Status);
            return row;
        }

        private void BindBundleDiffRow(VisualElement row, int index)
        {
            var cells = (BundleDiffRowElements)row.userData;
            BundleDiff diff = _visibleBundleDiffs[index];
            row.EnableInClassList("abi-diff-added", diff.Status == DiffStatus.Added);
            row.EnableInClassList("abi-diff-removed", diff.Status == DiffStatus.Removed);
            row.EnableInClassList("abi-diff-increased", diff.Delta > 0);
            row.EnableInClassList("abi-diff-decreased", diff.Delta < 0);
            cells.Name.text = diff.BundleName;
            cells.OldSize.text = ByteFormatter.FormatBytes(diff.OldSize);
            cells.NewSize.text = ByteFormatter.FormatBytes(diff.NewSize);
            cells.Delta.text = FormatSignedBytes(diff.Delta);
            cells.Status.text = diff.Status.ToString();
        }

        private VisualElement MakeAssetDiffRow()
        {
            var row = TableBuilder.CreateRow();
            var cells = new AssetDiffRowElements
            {
                Name = TableBuilder.CreateCell("asset-diff-name", 3f),
                OldSize = TableBuilder.CreateCell("asset-diff-old", 1f, true),
                NewSize = TableBuilder.CreateCell("asset-diff-new", 1f, true),
                Delta = TableBuilder.CreateCell("asset-diff-delta", 1f, true),
                Status = TableBuilder.CreateCell("asset-diff-status", 1f)
            };

            row.userData = cells;
            row.Add(cells.Name);
            row.Add(cells.OldSize);
            row.Add(cells.NewSize);
            row.Add(cells.Delta);
            row.Add(cells.Status);
            return row;
        }

        private void BindAssetDiffRow(VisualElement row, int index)
        {
            var cells = (AssetDiffRowElements)row.userData;
            AssetDiff diff = _visibleAssetDiffs[index];
            row.EnableInClassList("abi-diff-added", diff.Status == DiffStatus.Added);
            row.EnableInClassList("abi-diff-removed", diff.Status == DiffStatus.Removed);
            row.EnableInClassList("abi-diff-increased", diff.Delta > 0);
            row.EnableInClassList("abi-diff-decreased", diff.Delta < 0);
            cells.Name.text = diff.AssetPath;
            cells.OldSize.text = ByteFormatter.FormatBytes(diff.OldSize);
            cells.NewSize.text = ByteFormatter.FormatBytes(diff.NewSize);
            cells.Delta.text = FormatSignedBytes(diff.Delta);
            cells.Status.text = diff.Status.ToString();
        }

        private void OnBundleDiffSelectionChanged(IEnumerable<object> selectedItems)
        {
            BundleDiff selectedDiff = selectedItems.OfType<BundleDiff>().FirstOrDefault();
            if (selectedDiff != null)
            {
                RenderGrowthDetails(selectedDiff.BundleName);
            }
        }

        private void LoadReport()
        {
            BuildLayoutParseResult result = _loader.LoadFromUserSelection();
            if (!result.Success)
            {
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    ShowStatus(result.ErrorMessage, StatusKind.Error);
                }

                return;
            }

            SetReport(result.Report);
        }

        private void ClearReport()
        {
            _report = null;
            _diffReport = null;
            RebuildReportServices();

            _allBundles.Clear();
            _allAssets.Clear();
            _allDuplicates.Clear();
            _visibleBundles.Clear();
            _visibleAssets.Clear();
            _visibleDuplicates.Clear();
            _visibleOptimizationCandidates.Clear();
            _dependencySearchResults.Clear();
            _visibleBundleDiffs.Clear();
            _visibleAssetDiffs.Clear();

            ResetSortState();
            _searchQuery = string.Empty;
            _oldBuildPath = string.Empty;
            _newBuildPath = string.Empty;
            _generatedAiPrompt = string.Empty;

            _sourceLabel.text = "No report loaded";
            _searchField.SetValueWithoutNotify(string.Empty);
            _dependencySearchField.SetValueWithoutNotify(string.Empty);
            _oldBuildPathLabel.text = "No old build selected";
            _newBuildPathLabel.text = "No new build selected";
            _diffFilterField.SetValueWithoutNotify("All");
            RenderAiReportPreview("Load a Build Layout report, choose a report type, then click Generate Report.");

            EditorPrefs.DeleteKey(LastReportPathEditorPrefsKey);

            _optimizationDetailsContainer.Clear();
            _optimizationDetailsContainer.Add(TableBuilder.CreateEmptyState("Select a candidate to inspect cause, dependency paths, and simulated savings."));
            ClearDependencySelection("Select an asset to inspect its dependency tree.");

            RenderOverview();
            RenderOptimizationDashboard();
            RefreshOptimizationTable();
            RenderDiffOverview();
            RenderDiffInsights();
            RefreshAllTables();
            SetActiveTab(ActiveTab.Overview);
            ShowStatus("Load a Build Layout report to begin.", StatusKind.Neutral);
        }

        private void ResetSortState()
        {
            _bundleSortColumn = BundleSortColumn.Size;
            _assetSortColumn = AssetSortColumn.Size;
            _duplicateSortColumn = DuplicateSortColumn.Waste;
            _optimizationSortColumn = OptimizationSortColumn.Savings;
            _bundleSortAscending = false;
            _assetSortAscending = false;
            _duplicateSortAscending = false;
            _optimizationSortAscending = false;
        }

        private void RestoreLastReportIfAvailable()
        {
            if (_report != null || _loader == null)
            {
                return;
            }

            string lastReportPath = EditorPrefs.GetString(LastReportPathEditorPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(lastReportPath))
            {
                return;
            }

            if (!File.Exists(lastReportPath))
            {
                ShowStatus("The last loaded report no longer exists. Load a report to begin.", StatusKind.Neutral);
                return;
            }

            BuildLayoutParseResult result = _loader.LoadFromPath(lastReportPath);
            if (!result.Success)
            {
                ShowStatus($"Could not restore the last report: {result.ErrorMessage}", StatusKind.Error);
                return;
            }

            SetReport(result.Report, true);
        }

        private void SelectDiffBuildPath(bool oldBuild)
        {
            string path = EditorUtility.OpenFilePanel(oldBuild ? "Select Old Build Layout" : "Select New Build Layout", string.Empty, "txt,json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (oldBuild)
            {
                _oldBuildPath = path;
                _oldBuildPathLabel.text = Path.GetFileName(path);
            }
            else
            {
                _newBuildPath = path;
                _newBuildPathLabel.text = Path.GetFileName(path);
            }
        }

        private void CompareDiffBuilds()
        {
            if (string.IsNullOrEmpty(_oldBuildPath) || string.IsNullOrEmpty(_newBuildPath))
            {
                ShowStatus("Select both old and new Build Layout reports before comparing.", StatusKind.Error);
                return;
            }

            BuildDiffServiceResult result = _buildDiffService.CompareBuilds(_oldBuildPath, _newBuildPath);
            if (!result.Success)
            {
                ShowStatus(result.ErrorMessage, StatusKind.Error);
                return;
            }

            _diffReport = result.Report;
            RenderDiffOverview();
            RenderDiffInsights();
            RefreshDiffTables();
            ShowStatus("Build diff generated.", StatusKind.Success);
        }

        private void ExportDiff(string extension)
        {
            if (_diffReport == null)
            {
                ShowStatus("Generate a build diff before exporting.", StatusKind.Error);
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export Diff Report",
                string.Empty,
                $"AddressablesBuildDiff.{extension}",
                extension);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (string.Equals(extension, "json", StringComparison.OrdinalIgnoreCase))
                {
                    _diffExportService.ExportJson(_diffReport, path);
                }
                else
                {
                    _diffExportService.ExportCsv(_diffReport, path);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                ShowStatus($"Could not export diff report: {exception.Message}", StatusKind.Error);
                return;
            }

            ShowStatus($"Exported diff report to {Path.GetFileName(path)}.", StatusKind.Success);
        }

        private void ExportOptimization(string extension)
        {
            EnsureOptimizationReport();
            if (_optimizationReport == null)
            {
                ShowStatus("Load a report before exporting optimization recommendations.", StatusKind.Error);
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export Optimization Report",
                string.Empty,
                $"AddressablesOptimization.{extension}",
                extension);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (string.Equals(extension, "json", StringComparison.OrdinalIgnoreCase))
                {
                    _optimizationExportService.ExportJson(_optimizationReport, path);
                }
                else if (string.Equals(extension, "md", StringComparison.OrdinalIgnoreCase))
                {
                    _optimizationExportService.ExportMarkdown(_optimizationReport, path);
                }
                else
                {
                    _optimizationExportService.ExportCsv(_optimizationReport, path);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                ShowStatus($"Could not export optimization report: {exception.Message}", StatusKind.Error);
                return;
            }

            ShowStatus($"Exported optimization report to {Path.GetFileName(path)}.", StatusKind.Success);
        }

        private void GenerateFixPlanPrompt()
        {
            if (_report == null)
            {
                ShowStatus("Load a report before generating a fix plan prompt.", StatusKind.Error);
                return;
            }

            EnsureOptimizationReport();
            if (_optimizationReport == null)
            {
                ShowStatus("Optimization report could not be generated.", StatusKind.Error);
                return;
            }

            _generatedAiPrompt = _aiPromptGenerator.GenerateFixPlanPrompt(_report, _optimizationReport);
            RenderAiReportPreview(_generatedAiPrompt);

            _aiPromptExporter.CopyToClipboard(_generatedAiPrompt);
            ShowStatus("Fix plan prompt copied to clipboard.", StatusKind.Success);
        }

        private void GenerateAiReport()
        {
            if (_report == null && _diffReport == null)
            {
                ShowStatus("Load a report or generate a build diff before creating an AI report.", StatusKind.Error);
                return;
            }

            AiReportMode mode = _aiReportModeField?.value ?? AiReportMode.OptimizationConsultant;
            QueueAiReportGeneration(mode);
        }

        private void QueueAiReportGeneration(AiReportMode mode)
        {
            ShowAiReportLoadingState(mode);
            _queuedAiReportMode = mode;
            if (_report != null && RequiresHeavyAiContext(mode) && !_report.DependenciesLoaded)
            {
                _generateAiReportAfterDependencyLoad = true;
                QueueDependencyLoad();
                return;
            }

            if (_aiReportGenerationQueued || _aiReportGenerationInProgress)
            {
                return;
            }

            _aiReportGenerationQueued = true;
            EditorApplication.delayCall += GenerateAiReportDelayed;
        }

        private void GenerateAiReportDelayed()
        {
            _aiReportGenerationQueued = false;
            if (_report == null && _diffReport == null)
            {
                RenderAiReportPreview("Load a Build Layout report, choose a report type, then click Generate Report.");
                return;
            }

            _aiReportGenerationInProgress = true;
            BuildReportData report = _report;
            BuildDiffReport diffReport = _diffReport;
            OptimizationReport existingOptimization = _optimizationReport;
            AiReportMode mode = _queuedAiReportMode;
            _aiReportGenerationSourcePath = report?.SourcePath ?? string.Empty;
            _aiReportGenerationTask = Task.Run(() => GenerateAiReportInBackground(mode, report, diffReport, existingOptimization));
            EditorApplication.update += PollAiReportGenerationTask;
        }

        private void CompleteAiReportGeneration(AiReportMode mode)
        {
            AiPromptContext context = CreateAiPromptContext(mode);
            _generatedAiPrompt = _aiPromptGenerator.GenerateReport(context);
            RenderAiReportPreview(_generatedAiPrompt);
            ShowStatus("AI report generated offline.", StatusKind.Success);
        }

        private AiReportGenerationResult GenerateAiReportInBackground(
            AiReportMode mode,
            BuildReportData report,
            BuildDiffReport diffReport,
            OptimizationReport existingOptimization)
        {
            OptimizationReport optimization = existingOptimization;
            IReadOnlyList<DependencyNode> mostReferenced = null;
            IReadOnlyList<DependencyNode> sharedAssets = null;
            IReadOnlyList<DependencyChainInfo> hotspots = null;

            if (report != null && mode != AiReportMode.BuildGrowthInvestigation)
            {
                var graphService = new DependencyGraphService(
                    report,
                    new BuildLayoutDependencyProvider(report),
                    fallbackProvider: null);

                optimization = optimization ?? new DuplicateOptimizationService(graphService).Analyze(report);
                if (RequiresHeavyAiContext(mode) && report.DependenciesLoaded)
                {
                    mostReferenced = graphService.GetMostReferencedAssets(10);
                    sharedAssets = graphService.GetSharedAssets().Take(10).ToList();
                    hotspots = graphService.GetLargestDependencyChains(10);
                }
            }

            var context = new AiPromptContext
            {
                Report = report ?? diffReport?.NewBuild,
                OptimizationReport = optimization,
                BuildDiffReport = diffReport,
                Mode = mode,
                MostReferencedAssets = mostReferenced,
                SharedAssets = sharedAssets,
                DependencyHotspots = hotspots
            };

            return new AiReportGenerationResult
            {
                Prompt = new AiPromptGenerator().GenerateReport(context),
                OptimizationReport = optimization
            };
        }

        private void PollAiReportGenerationTask()
        {
            if (_aiReportGenerationTask == null || !_aiReportGenerationTask.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= PollAiReportGenerationTask;
            Task<AiReportGenerationResult> completedTask = _aiReportGenerationTask;
            string sourcePath = _aiReportGenerationSourcePath;
            _aiReportGenerationTask = null;
            _aiReportGenerationSourcePath = string.Empty;
            _aiReportGenerationInProgress = false;

            if (_report != null &&
                sourcePath.Length > 0 &&
                !string.Equals(_report.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                RenderAiReportPreview("Report changed while AI report was generating. Generate the report again.");
                ShowStatus("AI report generation was ignored because the loaded report changed.", StatusKind.Error);
                Repaint();
                return;
            }

            if (completedTask.IsFaulted)
            {
                string message = completedTask.Exception?.GetBaseException().Message ?? "Unknown error.";
                RenderAiReportPreview("AI report generation failed.");
                ShowStatus($"Could not generate AI report: {message}", StatusKind.Error);
                Repaint();
                return;
            }

            AiReportGenerationResult result = completedTask.Result;
            _generatedAiPrompt = result.Prompt;
            if (_optimizationReport == null && result.OptimizationReport != null)
            {
                _optimizationReport = result.OptimizationReport;
                RenderOptimizationDashboard();
                RefreshOptimizationTable();
            }

            RenderAiReportPreview(_generatedAiPrompt);
            ShowStatus("AI report generated offline.", StatusKind.Success);
            Repaint();
        }

        private static bool RequiresHeavyAiContext(AiReportMode mode)
        {
            return mode == AiReportMode.OptimizationConsultant ||
                   mode == AiReportMode.DuplicateInvestigation ||
                   mode == AiReportMode.TechnicalAudit;
        }

        private void ShowAiReportLoadingState(AiReportMode mode)
        {
            if (_aiReportOutputContainer == null)
            {
                return;
            }

            AiPromptTemplate template = AiPromptTemplate.ForMode(mode);
            _aiReportOutputContainer.Clear();
            _aiReportOutputContainer.Add(CreateLoadingState($"Generating {template.Title}..."));
            ShowStatus("Generating AI report context...", StatusKind.Neutral);
            Repaint();
        }

        private void RenderAiReportPreview(string text)
        {
            if (_aiReportPreviewField == null)
            {
                return;
            }

            _aiReportPreviewField.SetValueWithoutNotify(text);
            if (_aiReportOutputContainer == null)
            {
                return;
            }

            _aiReportOutputContainer.Clear();
            if (_aiReportScrollView == null)
            {
                _aiReportScrollView = new ScrollView();
                _aiReportScrollView.AddToClassList("abi-ai-scroll");
            }

            _aiReportScrollView.Clear();
            _aiReportScrollView.Add(_aiReportPreviewField);
            _aiReportOutputContainer.Add(_aiReportScrollView);
        }

        private void CopyAiReportToClipboard()
        {
            if (string.IsNullOrWhiteSpace(_generatedAiPrompt))
            {
                ShowStatus("Generate an AI report before copying.", StatusKind.Error);
                return;
            }

            _aiPromptExporter.CopyToClipboard(_generatedAiPrompt);
            ShowStatus("AI report copied to clipboard.", StatusKind.Success);
        }

        private void ExportAiReport(string extension)
        {
            if (string.IsNullOrWhiteSpace(_generatedAiPrompt))
            {
                ShowStatus("Generate an AI report before exporting.", StatusKind.Error);
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export AI Report",
                string.Empty,
                $"AddressablesAIReport.{extension}",
                extension);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (string.Equals(extension, "md", StringComparison.OrdinalIgnoreCase))
                {
                    _aiPromptExporter.ExportMarkdown(_generatedAiPrompt, path);
                }
                else
                {
                    _aiPromptExporter.ExportTxt(_generatedAiPrompt, path);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                ShowStatus($"Could not export AI report: {exception.Message}", StatusKind.Error);
                return;
            }

            ShowStatus($"Exported AI report to {Path.GetFileName(path)}.", StatusKind.Success);
        }

        private AiPromptContext CreateAiPromptContext(AiReportMode mode)
        {
            OptimizationReport optimization = null;
            if (_report != null && mode != AiReportMode.BuildGrowthInvestigation)
            {
                EnsureOptimizationReport();
                optimization = _optimizationReport;
            }
            else
            {
                optimization = _optimizationReport;
            }

            IReadOnlyList<DependencyNode> mostReferenced = null;
            IReadOnlyList<DependencyNode> sharedAssets = null;
            IReadOnlyList<DependencyChainInfo> hotspots = null;
            if (_report != null &&
                (mode == AiReportMode.OptimizationConsultant ||
                 mode == AiReportMode.DuplicateInvestigation ||
                 mode == AiReportMode.TechnicalAudit) &&
                _report.DependenciesLoaded)
            {
                mostReferenced = _dependencyGraphService.GetMostReferencedAssets(10);
                sharedAssets = _dependencyGraphService.GetSharedAssets().Take(10).ToList();
                hotspots = _dependencyGraphService.GetLargestDependencyChains(10);
            }

            return new AiPromptContext
            {
                Report = _report ?? _diffReport?.NewBuild,
                OptimizationReport = optimization,
                BuildDiffReport = _diffReport,
                Mode = mode,
                MostReferencedAssets = mostReferenced,
                SharedAssets = sharedAssets,
                DependencyHotspots = hotspots
            };
        }

        private void SetReport(BuildReportData report, bool restored = false)
        {
            _report = report;
            var analysis = new BuildAnalysisService(report);

            _allBundles.Clear();
            _allBundles.AddRange(analysis.GetLargestBundles());
            _allAssets.Clear();
            _allAssets.AddRange(analysis.GetLargestAssets());
            _allDuplicates.Clear();
            _allDuplicates.AddRange(analysis.GetDuplicateAssets());
            RebuildReportServices();

            _searchQuery = string.Empty;
            _generatedAiPrompt = string.Empty;
            _searchField.SetValueWithoutNotify(string.Empty);
            _dependencySearchField.SetValueWithoutNotify(string.Empty);
            RenderAiReportPreview("Choose a report type, then click Generate Report.");

            _sourceLabel.text = Path.GetFileName(report.SourcePath);
            if (!string.IsNullOrEmpty(report.SourcePath))
            {
                EditorPrefs.SetString(LastReportPathEditorPrefsKey, report.SourcePath);
            }

            RenderOverview();
            RenderOptimizationDashboard();
            _optimizationDetailsContainer.Clear();
            _optimizationDetailsContainer.Add(TableBuilder.CreateEmptyState("Select a candidate to inspect cause, dependency paths, and simulated savings."));
            ClearDependencySelection("Select an asset to inspect its dependency tree.");
            RefreshAllTables();
            SetActiveTab(ActiveTab.Overview);
            ShowStatus(
                $"{(restored ? "Restored" : "Loaded")} {report.Bundles.Count} bundles and {report.Assets.Count} assets from {Path.GetFileName(report.SourcePath)}.",
                StatusKind.Success);
        }

        private void RebuildReportServices()
        {
            if (_report == null)
            {
                ClearDependencyLoadState();
                _dependencyGraphService = null;
                _dependencySearchService = null;
                _dependencyTreeBuilder = null;
                _optimizationService = null;
                _optimizationReport = null;
                _optimizationGenerationQueued = false;
                _optimizationGenerationInProgress = false;
                _aiReportGenerationQueued = false;
                _aiReportGenerationInProgress = false;
                _generateAiReportAfterDependencyLoad = false;
                ClearAiReportGenerationState();
                return;
            }

            ClearDependencyLoadState();
            ClearAiReportGenerationState();
            _dependencyGraphService = new DependencyGraphService(
                _report,
                new BuildLayoutDependencyProvider(_report),
                new AssetDatabaseDependencyProvider(_report));
            _dependencySearchService = new DependencySearchService(_dependencyGraphService);
            _dependencyTreeBuilder = new DependencyTreeBuilder(_dependencyGraphService);
            _optimizationService = new DuplicateOptimizationService(_dependencyGraphService);
            _optimizationReport = null;
            _optimizationGenerationQueued = false;
            _optimizationGenerationInProgress = false;
            _generateAiReportAfterDependencyLoad = false;
        }

        private void ClearDependencyLoadState()
        {
            EditorApplication.update -= PollDependencyLoadTask;
            _dependencyLoadQueued = false;
            _dependencyLoadInProgress = false;
            _dependencyLoadTask = null;
            _dependencyLoadSourcePath = string.Empty;
        }

        private void ClearAiReportGenerationState()
        {
            EditorApplication.update -= PollAiReportGenerationTask;
            _aiReportGenerationQueued = false;
            _aiReportGenerationInProgress = false;
            _aiReportGenerationTask = null;
            _aiReportGenerationSourcePath = string.Empty;
        }

        private bool EnsureReportDependenciesLoaded()
        {
            if (_report == null)
            {
                return false;
            }

            if (_report.DependenciesLoaded)
            {
                return true;
            }

            if (_parser == null || string.IsNullOrWhiteSpace(_report.SourcePath))
            {
                ShowStatus("Could not load dependency graph for this report.", StatusKind.Error);
                return false;
            }

            ShowStatus("Loading dependency graph from JSON report...", StatusKind.Neutral);
            BuildLayoutParseResult result = _parser.ParseWithDependencies(_report.SourcePath);
            if (!result.Success)
            {
                ShowStatus($"Could not load dependency graph: {result.ErrorMessage}", StatusKind.Error);
                return false;
            }

            _report.Dependencies.Clear();
            _report.Dependencies.AddRange(result.Report.Dependencies);
            _report.DependenciesLoaded = true;
            RebuildReportServices();
            ShowStatus($"Loaded dependency graph with {_report.Dependencies.Count} edges.", StatusKind.Success);
            return true;
        }

        private void RenderOverview()
        {
            _overviewContainer.Clear();

            if (_report == null)
            {
                _overviewContainer.Add(TableBuilder.CreateEmptyState(
                    "Load a Build Layout report to see total size, bundle count, asset count, duplicates, and largest offenders."));
                return;
            }

            var analysis = new BuildAnalysisService(_report);
            BuildOverviewData overview = analysis.GetOverview();

            _overviewContainer.Add(CreateSectionTitle("Build Health Overview", "Primary metrics for build size, duplicate waste, savings, and risk."));

            var grid = new VisualElement();
            grid.AddToClassList("abi-overview-grid");
            grid.AddToClassList("abi-kpi-row");
            _overviewContainer.Add(grid);

            int healthScore = CalculateHealthScore(overview);
            AddHealthScoreCard(grid, healthScore);
            AddStatCard(grid, "Build Size", ByteFormatter.FormatBytes(overview.TotalBuildSizeBytes), null, null, FormatBuildGrowthMeta());
            AddStatCard(grid, "Bundle Count", overview.BundleCount.ToString(CultureInfo.InvariantCulture));
            AddStatCard(grid, "Asset Count", overview.AssetCount.ToString(CultureInfo.InvariantCulture));
            AddStatCard(grid, "Duplicate Waste", ByteFormatter.FormatBytes(overview.EstimatedDuplicateWasteBytes), GetWasteCardClass(overview.EstimatedDuplicateWasteBytes));
            AddStatCard(grid, "Potential Savings", ByteFormatter.FormatBytes(overview.EstimatedDuplicateWasteBytes), GetWasteCardClass(overview.EstimatedDuplicateWasteBytes), null, "From duplicate asset waste");

            if (_diffReport != null)
            {
                BundleDiff largestGrowthBundle = _diffReport.BundleDiffs
                    .Where(diff => diff.Delta > 0)
                    .OrderByDescending(diff => diff.Delta)
                    .FirstOrDefault();
                AssetDiff largestAddedAsset = _diffReport.AssetDiffs
                    .Where(diff => diff.Status == DiffStatus.Added)
                    .OrderByDescending(diff => diff.NewSize)
                    .FirstOrDefault();
                long duplicateRegressionWaste = _diffReport.DuplicateRegressions.Sum(regression => regression.WasteIntroducedBytes);

                AddStatCard(grid, "Build Growth", FormatSignedBytes(_diffReport.TotalSizeDelta));
                AddStatCard(grid, "Largest Growth Bundle", FormatBundleDiff(largestGrowthBundle));
                AddStatCard(grid, "Largest Added Asset", FormatAssetDiff(largestAddedAsset));
                AddStatCard(grid, "Duplicate Regression", ByteFormatter.FormatBytes(duplicateRegressionWaste));
            }

            AddOverviewInsightsPanel(overview);
            AddTopOffendersPanel(overview);
        }

        private void AddOverviewInsightsPanel(BuildOverviewData overview)
        {
            var panel = new VisualElement();
            panel.AddToClassList("abi-insight-grid");

            List<DuplicateAssetData> topDuplicates = _allDuplicates
                .OrderByDescending(duplicate => duplicate.EstimatedWasteBytes)
                .Take(5)
                .ToList();

            long topDuplicateWaste = topDuplicates.Sum(duplicate => duplicate.EstimatedWasteBytes);
            string topDuplicateShare = overview.EstimatedDuplicateWasteBytes > 0
                ? (topDuplicateWaste / (double)overview.EstimatedDuplicateWasteBytes).ToString("P0", CultureInfo.InvariantCulture)
                : "0%";

            panel.Add(CreateInsightCard(
                "!",
                "Top Issues",
                topDuplicates.Count == 0
                    ? "No duplicated asset waste was detected in this build."
                    : $"{topDuplicates.Count} assets account for {topDuplicateShare} of duplicate waste.",
                topDuplicates.Count == 0 ? OptimizationSeverity.Low : EstimateSeverity(topDuplicateWaste, overview.TotalBuildSizeBytes)));

            panel.Add(CreateInsightCard(
                "$",
                "Optimization ROI",
                overview.EstimatedDuplicateWasteBytes > 0
                    ? $"{ByteFormatter.FormatBytes(overview.EstimatedDuplicateWasteBytes)} can potentially be recovered from duplicate asset waste."
                    : "No duplicate waste savings are currently available.",
                EstimateSeverity(overview.EstimatedDuplicateWasteBytes, overview.TotalBuildSizeBytes)));

            string diffMessage = _diffReport == null
                ? "Compare two reports to reveal build growth and regression signals."
                : $"Build size changed by {FormatSignedBytes(_diffReport.TotalSizeDelta)} in the active diff.";
            panel.Add(CreateInsightCard(
                "^",
                "Build Growth",
                diffMessage,
                _diffReport != null && _diffReport.TotalSizeDelta > 0 ? OptimizationSeverity.Medium : OptimizationSeverity.Low));

            _overviewContainer.Add(CreateSectionTitle("Insights", "Actionable signals surfaced from the current report."));
            _overviewContainer.Add(panel);
        }

        private void AddTopOffendersPanel(BuildOverviewData overview)
        {
            var panel = new VisualElement();
            panel.AddToClassList("abi-top-offenders");
            panel.Add(CreatePaneHeading("Top Duplicate Assets"));

            IEnumerable<DuplicateAssetData> topDuplicates = _allDuplicates
                .OrderByDescending(duplicate => duplicate.EstimatedWasteBytes)
                .Take(5);

            if (!topDuplicates.Any())
            {
                panel.Add(TableBuilder.CreateEmptyState("No duplicate asset waste detected in this build."));
                _overviewContainer.Add(panel);
                return;
            }

            var header = new VisualElement();
            header.AddToClassList("abi-top-offenders-header");
            header.Add(CreateOffenderCell("Asset Name", 3f));
            header.Add(CreateOffenderCell("Waste", 1f, true));
            header.Add(CreateOffenderCell("Bundles", 0.8f, true));
            header.Add(CreateOffenderCell("Severity", 1f));
            panel.Add(header);

            foreach (DuplicateAssetData duplicate in topDuplicates)
            {
                OptimizationSeverity severity = EstimateSeverity(duplicate.EstimatedWasteBytes, overview.TotalBuildSizeBytes);
                var row = new VisualElement();
                row.AddToClassList("abi-top-offender-row");
                var name = CreateOffenderCell(duplicate.Asset.Name, 3f);
                name.tooltip = duplicate.Asset.Path;
                row.Add(name);
                row.Add(CreateOffenderCell(ByteFormatter.FormatBytes(duplicate.EstimatedWasteBytes), 1f, true));
                row.Add(CreateOffenderCell(duplicate.BundleCount.ToString(CultureInfo.InvariantCulture), 0.8f, true));
                var severityLabel = CreateOffenderCell(string.Empty, 1f);
                severityLabel.AddToClassList("abi-severity-pill");
                SetSeverityPill(severityLabel, severity);
                row.Add(severityLabel);
                panel.Add(row);
            }

            _overviewContainer.Add(panel);
        }

        private static Label CreateOffenderCell(string text, float flexGrow, bool numeric = false)
        {
            var label = new Label(text);
            label.AddToClassList("abi-top-offender-cell");
            if (numeric)
            {
                label.AddToClassList("abi-cell-number");
            }

            label.style.flexGrow = flexGrow;
            label.style.flexBasis = 0;
            label.style.minWidth = 0;
            return label;
        }

        private static VisualElement CreateSectionTitle(string title, string subtitle)
        {
            var container = new VisualElement();
            container.AddToClassList("abi-section-title");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("abi-section-title-label");
            container.Add(titleLabel);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var subtitleLabel = new Label(subtitle);
                subtitleLabel.AddToClassList("abi-section-title-subtitle");
                container.Add(subtitleLabel);
            }

            return container;
        }

        private static void AddHealthScoreCard(VisualElement parent, int healthScore)
        {
            var card = new VisualElement();
            card.AddToClassList("abi-stat-card");
            card.AddToClassList("abi-health-card");
            card.AddToClassList(GetHealthCardClass(healthScore));

            var label = new Label("Health Score");
            label.AddToClassList("abi-stat-label");

            var body = new VisualElement();
            body.AddToClassList("abi-health-body");

            var ring = new VisualElement();
            ring.AddToClassList("abi-health-ring");
            ring.AddToClassList(GetHealthRingClass(healthScore));
            var score = new Label(healthScore.ToString(CultureInfo.InvariantCulture));
            score.AddToClassList("abi-health-score");
            ring.Add(score);

            var copy = new VisualElement();
            copy.AddToClassList("abi-health-copy");
            var value = new Label(healthScore.ToString(CultureInfo.InvariantCulture) + " / 100");
            value.AddToClassList("abi-health-value");
            var state = new Label(FormatHealthState(healthScore));
            state.AddToClassList("abi-health-state");
            copy.Add(value);
            copy.Add(state);

            body.Add(ring);
            body.Add(copy);
            card.Add(label);
            card.Add(body);
            parent.Add(card);
        }

        private static void AddStatCard(VisualElement parent, string label, string value, string cardClass = null, string valueClass = null, string meta = null)
        {
            var card = new VisualElement();
            card.AddToClassList("abi-stat-card");
            if (!string.IsNullOrEmpty(cardClass))
            {
                foreach (string className in cardClass.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    card.AddToClassList(className);
                }
            }

            var labelElement = new Label(label);
            labelElement.AddToClassList("abi-stat-label");

            var valueElement = new Label(value);
            valueElement.AddToClassList("abi-stat-value");
            if (!string.IsNullOrEmpty(valueClass))
            {
                valueElement.AddToClassList(valueClass);
            }

            card.Add(labelElement);
            card.Add(valueElement);
            if (!string.IsNullOrEmpty(meta))
            {
                var metaElement = new Label(meta);
                metaElement.AddToClassList("abi-stat-meta");
                card.Add(metaElement);
            }

            parent.Add(card);
        }

        private static VisualElement CreateInsightCard(string icon, string title, string body, OptimizationSeverity severity)
        {
            var card = new VisualElement();
            card.AddToClassList("abi-insight-card");

            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("abi-insight-icon");
            card.Add(iconLabel);

            var content = new VisualElement();
            content.AddToClassList("abi-insight-content");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("abi-insight-title");
            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("abi-insight-body");
            var severityLabel = new Label();
            severityLabel.AddToClassList("abi-severity-pill");
            severityLabel.AddToClassList("abi-insight-severity");
            SetSeverityPill(severityLabel, severity);

            content.Add(titleLabel);
            content.Add(bodyLabel);
            content.Add(severityLabel);
            card.Add(content);
            return card;
        }

        private static void AddDiffStatCard(VisualElement parent, string label, string value)
        {
            AddStatCard(parent, label, value, "abi-diff-stat-card", "abi-diff-stat-value");
        }

        private string FormatBuildGrowthMeta()
        {
            if (_diffReport == null)
            {
                return null;
            }

            return "Trend " + FormatSignedBytes(_diffReport.TotalSizeDelta);
        }

        private static int CalculateHealthScore(BuildOverviewData overview)
        {
            if (overview == null || overview.TotalBuildSizeBytes <= 0)
            {
                return 100;
            }

            double duplicateRatio = (double)Math.Max(0, overview.EstimatedDuplicateWasteBytes) / overview.TotalBuildSizeBytes;
            int duplicatePenalty = (int)Math.Round(Math.Min(45, duplicateRatio * 220));
            int countPenalty = Math.Min(20, overview.DuplicateAssetCount / 10);
            return Math.Max(0, Math.Min(100, 100 - duplicatePenalty - countPenalty));
        }

        private static string FormatHealthState(int healthScore)
        {
            if (healthScore >= 90)
            {
                return "Excellent";
            }

            if (healthScore >= 70)
            {
                return "Needs attention";
            }

            return "High risk";
        }

        private static string GetHealthCardClass(int healthScore)
        {
            if (healthScore >= 90)
            {
                return "abi-card-success";
            }

            if (healthScore >= 70)
            {
                return "abi-card-warning";
            }

            return "abi-card-danger";
        }

        private static string GetHealthRingClass(int healthScore)
        {
            if (healthScore >= 90)
            {
                return "abi-health-ring-success";
            }

            if (healthScore >= 70)
            {
                return "abi-health-ring-warning";
            }

            return "abi-health-ring-danger";
        }

        private static string GetWasteCardClass(long bytes)
        {
            return bytes > 0 ? "abi-card-warning" : "abi-card-success";
        }

        private static OptimizationSeverity EstimateSeverity(long wasteBytes, long totalBuildSizeBytes)
        {
            if (totalBuildSizeBytes <= 0)
            {
                return wasteBytes > 0 ? OptimizationSeverity.Medium : OptimizationSeverity.Low;
            }

            double ratio = (double)wasteBytes / totalBuildSizeBytes;
            if (ratio >= 0.05)
            {
                return OptimizationSeverity.Critical;
            }

            if (ratio >= 0.02)
            {
                return OptimizationSeverity.High;
            }

            if (ratio > 0)
            {
                return OptimizationSeverity.Medium;
            }

            return OptimizationSeverity.Low;
        }

        private static void SetSeverityPill(Label label, OptimizationSeverity severity)
        {
            label.RemoveFromClassList("abi-severity-critical");
            label.RemoveFromClassList("abi-severity-high");
            label.RemoveFromClassList("abi-severity-medium");
            label.RemoveFromClassList("abi-severity-low");
            label.text = severity.ToString().ToUpperInvariant();

            switch (severity)
            {
                case OptimizationSeverity.Critical:
                    label.AddToClassList("abi-severity-critical");
                    break;
                case OptimizationSeverity.High:
                    label.AddToClassList("abi-severity-high");
                    break;
                case OptimizationSeverity.Medium:
                    label.AddToClassList("abi-severity-medium");
                    break;
                default:
                    label.AddToClassList("abi-severity-low");
                    break;
            }
        }

        private static Label CreatePaneHeading(string text)
        {
            var heading = new Label(text);
            heading.AddToClassList("abi-pane-heading");
            return heading;
        }

        private static string FormatLargestBundle(BundleData bundle)
        {
            return bundle == null
                ? "None"
                : $"{bundle.Name} ({ByteFormatter.FormatBytes(bundle.SizeBytes)})";
        }

        private static string FormatLargestAsset(AssetData asset)
        {
            return asset == null
                ? "None"
                : $"{asset.Name} ({ByteFormatter.FormatBytes(asset.SizeBytes)})";
        }

        private static string FormatDependencyNode(DependencyNode node)
        {
            return node == null
                ? "None"
                : $"{node.AssetName} ({node.ReferencedByCount} refs)";
        }

        private static string FormatDependencyChain(DependencyChainInfo chain)
        {
            return chain == null
                ? "None"
                : $"{chain.RootAssetName} ({chain.Depth} levels)";
        }

        private static string FormatBundleDiff(BundleDiff diff)
        {
            return diff == null ? "None" : $"{diff.BundleName} ({FormatSignedBytes(diff.Delta)})";
        }

        private static string FormatAssetDiff(AssetDiff diff)
        {
            return diff == null ? "None" : $"{diff.AssetName} ({ByteFormatter.FormatBytes(diff.NewSize)})";
        }

        private static string FormatSignedBytes(long bytes)
        {
            if (bytes > 0)
            {
                return "+" + ByteFormatter.FormatBytes(bytes);
            }

            if (bytes < 0)
            {
                return "-" + ByteFormatter.FormatBytes(-bytes);
            }

            return ByteFormatter.FormatBytes(0);
        }

        private static string FormatCause(DuplicateCause cause)
        {
            return SplitPascalCase(cause.ToString());
        }

        private static string FormatRecommendation(OptimizationRecommendation recommendation)
        {
            return SplitPascalCase(recommendation.ToString());
        }

        private static string SplitPascalCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(value.Length + 8);
            builder.Append(value[0]);
            for (int i = 1; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsUpper(character) && !char.IsWhiteSpace(value[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        private void SetActiveTab(ActiveTab tab)
        {
            _activeTab = tab;
            _overviewContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Overview);
            _bundlesContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Bundles);
            _assetsContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Assets);
            _duplicatesContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Duplicates);
            _optimizationContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Optimization);
            _dependenciesContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Dependencies);
            _buildDiffContainer.EnableInClassList("abi-hidden", tab != ActiveTab.BuildDiff);
            _aiReportContainer.EnableInClassList("abi-hidden", tab != ActiveTab.AIReport);

            SetTabButtonState(_overviewTabButton, tab == ActiveTab.Overview);
            SetTabButtonState(_bundlesTabButton, tab == ActiveTab.Bundles);
            SetTabButtonState(_assetsTabButton, tab == ActiveTab.Assets);
            SetTabButtonState(_duplicatesTabButton, tab == ActiveTab.Duplicates);
            SetTabButtonState(_optimizationTabButton, tab == ActiveTab.Optimization);
            SetTabButtonState(_dependenciesTabButton, tab == ActiveTab.Dependencies);
            SetTabButtonState(_buildDiffTabButton, tab == ActiveTab.BuildDiff);
            SetTabButtonState(_aiReportTabButton, tab == ActiveTab.AIReport);

            bool tableSearchEnabled = tab == ActiveTab.Bundles ||
                                      tab == ActiveTab.Assets ||
                                      tab == ActiveTab.Duplicates ||
                                      tab == ActiveTab.Optimization;
            _searchField.SetEnabled(tableSearchEnabled);
            RefreshActiveTable();
        }

        private static void SetTabButtonState(Button button, bool active)
        {
            button.EnableInClassList("abi-tab-button-active", active);
        }

        private void ToggleBundleSort(BundleSortColumn column)
        {
            if (_bundleSortColumn == column)
            {
                _bundleSortAscending = !_bundleSortAscending;
            }
            else
            {
                _bundleSortColumn = column;
                _bundleSortAscending = column == BundleSortColumn.Name;
            }

            RefreshBundles();
        }

        private void ToggleAssetSort(AssetSortColumn column)
        {
            if (_assetSortColumn == column)
            {
                _assetSortAscending = !_assetSortAscending;
            }
            else
            {
                _assetSortColumn = column;
                _assetSortAscending = column == AssetSortColumn.Name || column == AssetSortColumn.Path;
            }

            RefreshAssets();
        }

        private void ToggleDuplicateSort(DuplicateSortColumn column)
        {
            if (_duplicateSortColumn == column)
            {
                _duplicateSortAscending = !_duplicateSortAscending;
            }
            else
            {
                _duplicateSortColumn = column;
                _duplicateSortAscending = column == DuplicateSortColumn.Name;
            }

            RefreshDuplicates();
        }

        private void ToggleOptimizationSort(OptimizationSortColumn column)
        {
            if (_optimizationSortColumn == column)
            {
                _optimizationSortAscending = !_optimizationSortAscending;
            }
            else
            {
                _optimizationSortColumn = column;
                _optimizationSortAscending = column == OptimizationSortColumn.Asset || column == OptimizationSortColumn.Severity;
            }

            RefreshOptimizationTable();
        }

        private void RefreshActiveTable()
        {
            switch (_activeTab)
            {
                case ActiveTab.Bundles:
                    RefreshBundles();
                    break;
                case ActiveTab.Assets:
                    RefreshAssets();
                    break;
                case ActiveTab.Duplicates:
                    RefreshDuplicates();
                    break;
                case ActiveTab.Optimization:
                    RefreshOptimizationView();
                    break;
                case ActiveTab.Dependencies:
                    RefreshDependenciesView();
                    break;
                case ActiveTab.BuildDiff:
                    RefreshDiffTables();
                    break;
                case ActiveTab.AIReport:
                    break;
            }
        }

        private void RefreshAllTables()
        {
            RefreshBundles();
            RefreshAssets();
            RefreshDuplicates();
            RefreshDiffTables();
        }

        private void RefreshBundles()
        {
            IEnumerable<BundleData> rows = _allBundles
                .Where(bundle => SearchUtility.Matches(_searchQuery, bundle.Name));

            rows = SortBundles(rows);
            ReplaceList(_visibleBundles, rows);
            RefreshListView(_bundlesListView, _visibleBundles.Count, _bundlesEmptyState);
        }

        private IEnumerable<BundleData> SortBundles(IEnumerable<BundleData> rows)
        {
            switch (_bundleSortColumn)
            {
                case BundleSortColumn.Name:
                    return _bundleSortAscending
                        ? rows.OrderBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase);
                case BundleSortColumn.AssetCount:
                    return _bundleSortAscending
                        ? rows.OrderBy(bundle => bundle.AssetCount).ThenBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(bundle => bundle.AssetCount).ThenBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase);
                default:
                    return _bundleSortAscending
                        ? rows.OrderBy(bundle => bundle.SizeBytes).ThenBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(bundle => bundle.SizeBytes).ThenBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase);
            }
        }

        private void RefreshAssets()
        {
            IEnumerable<AssetData> rows = _allAssets
                .Where(asset => SearchUtility.Matches(_searchQuery, asset.Name, asset.Path));

            rows = SortAssets(rows);
            ReplaceList(_visibleAssets, rows);
            RefreshListView(_assetsListView, _visibleAssets.Count, _assetsEmptyState);
        }

        private IEnumerable<AssetData> SortAssets(IEnumerable<AssetData> rows)
        {
            switch (_assetSortColumn)
            {
                case AssetSortColumn.Name:
                    return _assetSortAscending
                        ? rows.OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(asset => asset.Name, StringComparer.OrdinalIgnoreCase);
                case AssetSortColumn.Path:
                    return _assetSortAscending
                        ? rows.OrderBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(asset => asset.Path, StringComparer.OrdinalIgnoreCase);
                case AssetSortColumn.BundleCount:
                    return _assetSortAscending
                        ? rows.OrderBy(asset => asset.BundleCount).ThenBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(asset => asset.BundleCount).ThenBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase);
                default:
                    return _assetSortAscending
                        ? rows.OrderBy(asset => asset.SizeBytes).ThenBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(asset => asset.SizeBytes).ThenBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase);
            }
        }

        private void RefreshDuplicates()
        {
            IEnumerable<DuplicateAssetData> rows = _allDuplicates
                .Where(duplicate => SearchUtility.Matches(_searchQuery, duplicate.Asset.Name, duplicate.Asset.Path));

            rows = SortDuplicates(rows);
            ReplaceList(_visibleDuplicates, rows);
            RefreshListView(_duplicatesListView, _visibleDuplicates.Count, _duplicatesEmptyState);
        }

        private void EnsureOptimizationReport()
        {
            if (_optimizationReport != null || _report == null || _optimizationService == null)
            {
                return;
            }

            _optimizationGenerationInProgress = true;
            try
            {
                if (!EnsureReportDependenciesLoaded())
                {
                    return;
                }

                _optimizationReport = _optimizationService.Analyze(_report);
            }
            finally
            {
                _optimizationGenerationInProgress = false;
            }
        }

        private void RefreshOptimizationView()
        {
            if (_report != null &&
                _optimizationReport == null &&
                !_optimizationGenerationInProgress)
            {
                QueueOptimizationGeneration();
                return;
            }

            RenderOptimizationDashboard();
            RefreshOptimizationTable();
        }

        private void RefreshDependenciesView()
        {
            if (_report != null &&
                !_report.DependenciesLoaded &&
                !_dependencyLoadInProgress)
            {
                QueueDependencyLoad();
                return;
            }

            RefreshDependencySearch();
        }

        private void QueueDependencyLoad()
        {
            ShowDependencyLoadingState();
            if (_dependencyLoadQueued || _dependencyLoadInProgress)
            {
                return;
            }

            _dependencyLoadQueued = true;
            EditorApplication.delayCall += StartDependencyLoadTask;
        }

        private void StartDependencyLoadTask()
        {
            _dependencyLoadQueued = false;
            if (_report == null || _report.DependenciesLoaded || string.IsNullOrWhiteSpace(_report.SourcePath))
            {
                RefreshDependencySearch();
                return;
            }

            _dependencyLoadInProgress = true;
            _dependencyLoadSourcePath = _report.SourcePath;
            string sourcePath = _dependencyLoadSourcePath;
            _dependencyLoadTask = Task.Run(() => _parser.ParseWithDependenciesForBackground(sourcePath));
            EditorApplication.update += PollDependencyLoadTask;
        }

        private void PollDependencyLoadTask()
        {
            if (_dependencyLoadTask == null || !_dependencyLoadTask.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= PollDependencyLoadTask;
            Task<BuildLayoutParseResult> completedTask = _dependencyLoadTask;
            string completedSourcePath = _dependencyLoadSourcePath;
            _dependencyLoadTask = null;
            _dependencyLoadSourcePath = string.Empty;
            _dependencyLoadInProgress = false;

            if (_report == null || !string.Equals(_report.SourcePath, completedSourcePath, StringComparison.OrdinalIgnoreCase))
            {
                Repaint();
                return;
            }

            if (completedTask.IsFaulted)
            {
                string message = completedTask.Exception?.GetBaseException().Message ?? "Unknown error.";
                ShowStatus($"Could not load dependency graph: {message}", StatusKind.Error);
                ClearDependencySelection("Dependency graph could not be loaded for this report.");
                Repaint();
                return;
            }

            ApplyDependencyLoadResult(completedTask.Result);
            Repaint();
        }

        private void ApplyDependencyLoadResult(BuildLayoutParseResult result)
        {
            if (!result.Success)
            {
                _generateAiReportAfterDependencyLoad = false;
                ShowStatus($"Could not load dependency graph: {result.ErrorMessage}", StatusKind.Error);
                ClearDependencySelection("Dependency graph could not be loaded for this report.");
                RefreshListView(_dependencySearchListView, 0, _dependencyEmptyState);
                return;
            }

            _report.Dependencies.Clear();
            _report.Dependencies.AddRange(result.Report.Dependencies);
            _report.DependenciesLoaded = true;
            RebuildReportServices();
            ShowStatus($"Loaded dependency graph with {_report.Dependencies.Count} edges.", StatusKind.Success);
            RefreshDependencySearch();
            if (_activeTab == ActiveTab.Optimization)
            {
                RefreshOptimizationView();
            }

            if (_generateAiReportAfterDependencyLoad)
            {
                _generateAiReportAfterDependencyLoad = false;
                QueueAiReportGeneration(_queuedAiReportMode);
            }
        }

        private void ShowDependencyLoadingState()
        {
            _dependencySearchResults.Clear();
            if (_dependencyEmptyState != null)
            {
                _dependencyEmptyState.text = "Loading dependency graph...";
            }

            RefreshListView(_dependencySearchListView, 0, _dependencyEmptyState);
            if (_dependencyTreeContainer != null)
            {
                _dependencyTreeContainer.Clear();
                _dependencyTreeContainer.Add(CreateLoadingState("Loading dependency graph..."));
            }

            if (_dependencyDetailsContainer != null)
            {
                _dependencyDetailsContainer.Clear();
                _dependencyDetailsContainer.Add(CreateLoadingState("Preparing dependency details..."));
            }

            ShowStatus("Loading dependency graph...", StatusKind.Neutral);
            Repaint();
        }

        private void QueueOptimizationGeneration()
        {
            ShowOptimizationLoadingState();
            if (_optimizationGenerationQueued)
            {
                return;
            }

            _optimizationGenerationQueued = true;
            EditorApplication.delayCall += GenerateOptimizationReportDelayed;
        }

        private void GenerateOptimizationReportDelayed()
        {
            _optimizationGenerationQueued = false;
            if (_report == null || _optimizationReport != null)
            {
                return;
            }

            EnsureOptimizationReport();
            RenderOptimizationDashboard();
            RefreshOptimizationTable();
            Repaint();
        }

        private void ShowOptimizationLoadingState()
        {
            _visibleOptimizationCandidates.Clear();
            _optimizationDashboardContainer.Clear();
            _optimizationInsightsContainer.Clear();
            _optimizationDashboardContainer.Add(CreateLoadingState("Loading optimization recommendations..."));
            if (_optimizationEmptyState != null)
            {
                _optimizationEmptyState.text = "Loading optimization recommendations...";
            }

            RefreshListView(_optimizationListView, 0, _optimizationEmptyState);
        }

        private void RenderOptimizationDashboard()
        {
            if (_optimizationDashboardContainer == null || _optimizationInsightsContainer == null)
            {
                return;
            }

            _optimizationDashboardContainer.Clear();
            _optimizationInsightsContainer.Clear();
            if (_optimizationReport == null)
            {
                _optimizationDashboardContainer.Add(TableBuilder.CreateEmptyState("Open this tab after loading a report to generate duplicate optimization recommendations."));
                return;
            }

            OptimizationCandidate topCandidate = _optimizationReport.Candidates
                .OrderByDescending(candidate => candidate.Impact?.PotentialSavingsBytes ?? candidate.DuplicateWasteBytes)
                .FirstOrDefault();
            AddStatCard(_optimizationDashboardContainer, "Potential Savings", ByteFormatter.FormatBytes(_optimizationReport.PotentialSavingsBytes), GetWasteCardClass(_optimizationReport.PotentialSavingsBytes));
            AddStatCard(_optimizationDashboardContainer, "Critical Issues", _optimizationReport.CriticalIssueCount.ToString(CultureInfo.InvariantCulture), _optimizationReport.CriticalIssueCount > 0 ? "abi-card-danger" : "abi-card-success");
            AddStatCard(_optimizationDashboardContainer, "Top Optimization Candidate", topCandidate?.Asset?.Name ?? "None", null, null, topCandidate == null ? null : ByteFormatter.FormatBytes(topCandidate.Impact.PotentialSavingsBytes));
            AddStatCard(_optimizationDashboardContainer, "ROI Score", FormatRoiScore(_optimizationReport), null, null, "Savings / duplicate waste");
            AddStatCard(_optimizationDashboardContainer, "Estimated Patch Reduction", ByteFormatter.FormatBytes(_optimizationReport.Simulation.EstimatedPatchReductionBytes));

            var heading = CreateSectionTitle("Optimization Insights", "Recommendation cards generated from duplicate waste and simulated savings.");
            _optimizationInsightsContainer.Add(heading);
            if (_optimizationReport.Insights.Count == 0)
            {
                _optimizationInsightsContainer.Add(CreateInsightCard(
                    "OK",
                    "No Duplicate Optimization Issues",
                    "The loaded report does not contain duplicate waste candidates that require action.",
                    OptimizationSeverity.Low));
                return;
            }

            foreach (OptimizationInsight insight in _optimizationReport.Insights)
            {
                _optimizationInsightsContainer.Add(CreateInsightCard(
                    insight.Rank.ToString(CultureInfo.InvariantCulture),
                    "Recommendation " + insight.Rank.ToString(CultureInfo.InvariantCulture),
                    insight.Message,
                    EstimateSeverity(_optimizationReport.PotentialSavingsBytes, _optimizationReport.TotalDuplicateWasteBytes)));
            }
        }

        private static string FormatRoiScore(OptimizationReport report)
        {
            if (report == null || report.TotalDuplicateWasteBytes <= 0)
            {
                return "0%";
            }

            double ratio = (double)report.PotentialSavingsBytes / report.TotalDuplicateWasteBytes;
            return Math.Min(100, Math.Round(ratio * 100)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private void RefreshOptimizationTable()
        {
            _visibleOptimizationCandidates.Clear();
            if (_optimizationReport == null)
            {
                if (_optimizationEmptyState != null)
                {
                    _optimizationEmptyState.text = _report == null
                        ? "Load a report to inspect duplicate optimization candidates."
                        : "Open the Optimization tab to generate recommendations.";
                }

                RefreshListView(_optimizationListView, 0, _optimizationEmptyState);
                return;
            }

            IEnumerable<OptimizationCandidate> rows = _optimizationReport.Candidates
                .Where(candidate => candidate.Asset != null)
                .Where(candidate => SearchUtility.Matches(_searchQuery, candidate.Asset.Name, candidate.Asset.Path));

            rows = SortOptimizationCandidates(rows);
            ReplaceList(_visibleOptimizationCandidates, rows);
            _optimizationEmptyState.text = "No optimization candidates match the current filters.";
            RefreshListView(_optimizationListView, _visibleOptimizationCandidates.Count, _optimizationEmptyState);
        }

        private IEnumerable<OptimizationCandidate> SortOptimizationCandidates(IEnumerable<OptimizationCandidate> rows)
        {
            switch (_optimizationSortColumn)
            {
                case OptimizationSortColumn.Asset:
                    return _optimizationSortAscending
                        ? rows.OrderBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase);
                case OptimizationSortColumn.BundleCount:
                    return _optimizationSortAscending
                        ? rows.OrderBy(candidate => candidate.BundleCount).ThenBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(candidate => candidate.BundleCount).ThenBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase);
                case OptimizationSortColumn.Waste:
                    return _optimizationSortAscending
                        ? rows.OrderBy(candidate => candidate.DuplicateWasteBytes).ThenBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(candidate => candidate.DuplicateWasteBytes).ThenBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase);
                case OptimizationSortColumn.Severity:
                    return _optimizationSortAscending
                        ? rows.OrderBy(candidate => candidate.Severity).ThenBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(candidate => candidate.Severity).ThenBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase);
                default:
                    return _optimizationSortAscending
                        ? rows.OrderBy(candidate => candidate.Impact.PotentialSavingsBytes).ThenBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(candidate => candidate.Impact.PotentialSavingsBytes).ThenBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase);
            }
        }

        private void RenderOptimizationDetails(OptimizationCandidate candidate)
        {
            _optimizationDetailsContainer.Clear();
            _optimizationDetailsContainer.Add(CreateSectionTitle("Candidate Details", "Why this issue is worth fixing and how risky the change is."));
            _optimizationDetailsContainer.Add(CreateOptimizationSummaryCard(candidate));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Asset", candidate.Asset?.Name ?? "Unknown"));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Path", candidate.Asset?.Path ?? string.Empty));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Bundle Count", candidate.BundleCount.ToString(CultureInfo.InvariantCulture)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Duplicate Waste", ByteFormatter.FormatBytes(candidate.DuplicateWasteBytes)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Potential Savings", ByteFormatter.FormatBytes(candidate.Impact.PotentialSavingsBytes)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Severity", candidate.Severity.ToString()));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Cause", FormatCause(candidate.Cause)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Recommendation", FormatRecommendation(candidate.Recommendation)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Confidence", candidate.Impact.Confidence.ToString("P0", CultureInfo.InvariantCulture)));

            _optimizationService?.PopulateDependencyPaths(candidate);

            var bundlesHeading = CreatePaneHeading("Affected Bundles");
            _optimizationDetailsContainer.Add(bundlesHeading);
            foreach (string bundleName in candidate.Asset.BundleNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                _optimizationDetailsContainer.Add(new Label(bundleName));
            }

            var pathsHeading = CreatePaneHeading("Dependency Paths");
            _optimizationDetailsContainer.Add(pathsHeading);
            if (candidate.DependencyPaths.Count == 0)
            {
                _optimizationDetailsContainer.Add(new Label("No dependency paths were available in the report."));
                return;
            }

            foreach (DependencyPath path in candidate.DependencyPaths.Take(8))
            {
                _optimizationDetailsContainer.Add(new Label(string.Join(" -> ", path.AssetPaths)));
            }
        }

        private static VisualElement CreateOptimizationSummaryCard(OptimizationCandidate candidate)
        {
            var card = new VisualElement();
            card.AddToClassList("abi-optimization-summary-card");

            var title = new Label(candidate.Asset?.Name ?? "Unknown Asset");
            title.AddToClassList("abi-optimization-summary-title");
            card.Add(title);

            var savings = new Label(ByteFormatter.FormatBytes(candidate.Impact.PotentialSavingsBytes));
            savings.AddToClassList("abi-optimization-summary-value");
            card.Add(savings);

            var meta = new Label("Potential savings | " + FormatRecommendation(candidate.Recommendation));
            meta.AddToClassList("abi-optimization-summary-meta");
            card.Add(meta);

            var severity = new Label();
            severity.AddToClassList("abi-severity-pill");
            SetSeverityPill(severity, candidate.Severity);
            card.Add(severity);
            return card;
        }

        private void RefreshDependencySearch()
        {
            _dependencySearchResults.Clear();
            if (_report != null && !_report.DependenciesLoaded)
            {
                QueueDependencyLoad();
                return;
            }

            if (_dependencySearchService == null)
            {
                _dependencyEmptyState.text = "Load a report to search dependency assets.";
                RefreshListView(_dependencySearchListView, _dependencySearchResults.Count, _dependencyEmptyState);
                ClearDependencySelection("Select an asset to inspect its dependency tree.");
                return;
            }

            string query = _dependencySearchField.value ?? string.Empty;
            _dependencySearchResults.AddRange(_dependencySearchService.Search(query));
            _dependencyEmptyState.text = _dependencySearchResults.Count == 0
                ? "No dependency assets match the current search."
                : string.Empty;
            RefreshListView(_dependencySearchListView, _dependencySearchResults.Count, _dependencyEmptyState);
            if (_dependencySearchResults.Count == 0)
            {
                ClearDependencySelection("Select an asset to inspect its dependency tree.");
            }
        }

        private void ShowDependencyTree(string assetPath)
        {
            if (_report != null && !_report.DependenciesLoaded)
            {
                QueueDependencyLoad();
                return;
            }

            if (_dependencyTreeBuilder == null)
            {
                ClearDependencySelection("Load a report before inspecting dependencies.");
                return;
            }

            DependencyNode root = _dependencyTreeBuilder.BuildTree(assetPath);
            _dependencyTreeContainer.Clear();
            _dependencyTreeContainer.Add(CreateDependencyFoldout(root, 0));
            ShowDependencyDetails(root);
        }

        private VisualElement CreateDependencyFoldout(DependencyNode node, int depth)
        {
            var foldout = new Foldout
            {
                text = FormatDependencyTreeLabel(node),
                value = depth < 2
            };
            foldout.AddToClassList("abi-dependency-foldout");
            foldout.RegisterCallback<ClickEvent>(_ => ShowDependencyDetails(node));

            AddDependencyIndicators(foldout, node);

            foreach (DependencyNode child in node.Dependencies)
            {
                foldout.Add(CreateDependencyFoldout(child, depth + 1));
            }

            return foldout;
        }

        private void AddDependencyIndicators(VisualElement element, DependencyNode node)
        {
            DependencyNodeIcon icon = DependencyIconUtility.GetIcon(node);
            string className = DependencyIconUtility.GetClass(icon);
            if (!string.IsNullOrEmpty(className))
            {
                element.AddToClassList(className);
                element.tooltip = DependencyIconUtility.GetMarker(icon);
            }

            if (node.IsCircularReference)
            {
                element.AddToClassList("abi-node-icon-circular");
            }
        }

        private static string FormatDependencyTreeLabel(DependencyNode node)
        {
            string marker = DependencyIconUtility.GetMarker(DependencyIconUtility.GetIcon(node));
            string markerPrefix = string.IsNullOrEmpty(marker) ? string.Empty : $"[{marker}] ";
            return $"{markerPrefix}{node.AssetName} | {ByteFormatter.FormatBytes(node.SizeBytes)} | deps {node.DependencyCount} | bundles {node.BundleCount}";
        }

        private void ShowDependencyDetails(DependencyNode node)
        {
            _dependencyDetailsContainer.Clear();
            _dependencyDetailsContainer.AddToClassList("abi-dependency-details");
            IReadOnlyList<DependencyNode> referencedBy = _dependencyGraphService?.GetReferencedBy(node.AssetPath) ?? Array.Empty<DependencyNode>();
            _dependencyDetailsContainer.Add(CreateDetailLabel("Asset Name", node.AssetName));
            _dependencyDetailsContainer.Add(CreateDetailLabel("Path", node.AssetPath));
            _dependencyDetailsContainer.Add(CreateDetailLabel("Size", ByteFormatter.FormatBytes(node.SizeBytes)));
            _dependencyDetailsContainer.Add(CreateDetailLabel("Bundle Count", node.BundleCount.ToString(CultureInfo.InvariantCulture)));
            _dependencyDetailsContainer.Add(CreateDetailLabel("Dependencies", node.DependencyCount.ToString(CultureInfo.InvariantCulture)));
            _dependencyDetailsContainer.Add(CreateDetailLabel("Referenced By", referencedBy.Count.ToString(CultureInfo.InvariantCulture)));
            _dependencyDetailsContainer.Add(CreateDetailLabel("Duplicate Waste", ByteFormatter.FormatBytes(node.EstimatedDuplicateWasteBytes)));
            _dependencyDetailsContainer.Add(CreateDetailLabel("Shared Status", node.IsSharedBetweenBundles ? "Shared between bundles" : "Not shared"));

            var referencesHeader = new Label("Referenced By");
            referencesHeader.AddToClassList("abi-detail-heading");
            _dependencyDetailsContainer.Add(referencesHeader);

            if (referencedBy.Count == 0)
            {
                _dependencyDetailsContainer.Add(CreateDependencyReferenceLabel("No direct references found."));
            }
            else
            {
                foreach (DependencyNode reference in referencedBy.Take(20))
                {
                    _dependencyDetailsContainer.Add(CreateDependencyReferenceLabel(reference.AssetPath));
                }
            }
        }

        private static Label CreateDependencyReferenceLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("abi-dependency-reference");
            label.tooltip = text;
            return label;
        }

        private void ClearDependencySelection(string message)
        {
            _dependencyTreeContainer.Clear();
            _dependencyDetailsContainer.Clear();
            _dependencyDetailsContainer.Add(TableBuilder.CreateEmptyState(message));
        }

        private static VisualElement CreateDetailLabel(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("abi-detail-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("abi-detail-label");

            var valueElement = new Label(value);
            valueElement.AddToClassList("abi-detail-value");

            row.Add(labelElement);
            row.Add(valueElement);
            return row;
        }

        private static VisualElement CreateLoadingState(string message)
        {
            var container = new VisualElement();
            container.AddToClassList("abi-loading-state");

            var label = new Label(message);
            label.AddToClassList("abi-loading-label");

            var track = new VisualElement();
            track.AddToClassList("abi-loading-track");

            var fill = new VisualElement();
            fill.AddToClassList("abi-loading-fill");

            track.Add(fill);
            container.Add(label);
            container.Add(track);
            StartLoadingAnimation(fill);
            return container;
        }

        private static void StartLoadingAnimation(VisualElement fill)
        {
            int frame = 0;
            fill.schedule.Execute(() =>
            {
                frame = (frame + 1) % 120;
                float normalized = frame / 119f;
                float leftPercent = -28f + normalized * 128f;
                fill.style.left = Length.Percent(leftPercent);
            }).Every(LoadingAnimationIntervalMs);
        }

        private void RenderDiffOverview()
        {
            if (_diffOverviewContainer == null)
            {
                return;
            }

            _diffOverviewContainer.Clear();
            if (_diffReport == null)
            {
                _diffOverviewContainer.Add(TableBuilder.CreateEmptyState("Select old and new Build Layout reports, then compare them."));
                return;
            }

            long oldSize = _diffReport.OldBuild.Bundles.Sum(bundle => bundle.SizeBytes);
            long newSize = _diffReport.NewBuild.Bundles.Sum(bundle => bundle.SizeBytes);
            long oldWaste = _diffReport.OldBuild.Assets.Sum(asset => Math.Max(0, asset.BundleCount - 1) * Math.Max(0, asset.SizeBytes));
            long newWaste = _diffReport.NewBuild.Assets.Sum(asset => Math.Max(0, asset.BundleCount - 1) * Math.Max(0, asset.SizeBytes));

            var grid = new VisualElement();
            grid.AddToClassList("abi-overview-grid");
            grid.AddToClassList("abi-diff-stat-grid");
            _diffOverviewContainer.Add(grid);
            AddDiffStatCard(grid, "Old Build Size", ByteFormatter.FormatBytes(oldSize));
            AddDiffStatCard(grid, "New Build Size", ByteFormatter.FormatBytes(newSize));
            AddDiffStatCard(grid, "Total Growth", FormatSignedBytes(_diffReport.TotalSizeDelta));
            AddDiffStatCard(grid, "Bundle Count Delta", FormatSignedCount(_diffReport.NewBuild.Bundles.Count - _diffReport.OldBuild.Bundles.Count));
            AddDiffStatCard(grid, "Asset Count Delta", FormatSignedCount(_diffReport.NewBuild.Assets.Count - _diffReport.OldBuild.Assets.Count));
            AddDiffStatCard(grid, "Duplicate Waste Delta", FormatSignedBytes(newWaste - oldWaste));
        }

        private void RenderDiffInsights()
        {
            _diffInsightsContainer.Clear();
            if (_diffReport == null)
            {
                return;
            }

            var heading = CreateSectionTitle("Smart Insights", "Build growth, duplicate regressions, and major contributors.");
            _diffInsightsContainer.Add(heading);
            foreach (DiffInsight insight in _diffReport.Insights)
            {
                _diffInsightsContainer.Add(CreateInsightCard(
                    insight.Rank.ToString(CultureInfo.InvariantCulture),
                    "Diff Insight " + insight.Rank.ToString(CultureInfo.InvariantCulture),
                    insight.Message,
                    _diffReport.TotalSizeDelta > 0 ? OptimizationSeverity.Medium : OptimizationSeverity.Low));
            }
        }

        private void RefreshDiffTables()
        {
            _visibleBundleDiffs.Clear();
            _visibleAssetDiffs.Clear();

            if (_diffReport == null)
            {
                RefreshListView(_bundleDiffListView, 0, _bundleDiffEmptyState);
                RefreshListView(_assetDiffListView, 0, _assetDiffEmptyState);
                if (_diffGrowthDetailsContainer != null)
                {
                    _diffGrowthDetailsContainer.Clear();
                }

                return;
            }

            string filter = _diffFilterField?.value ?? "All";
            _visibleBundleDiffs.AddRange(_diffReport.BundleDiffs.Where(diff => MatchesDiffFilter(diff.Status, filter))
                .OrderByDescending(diff => diff.Delta)
                .ThenBy(diff => diff.BundleName, StringComparer.OrdinalIgnoreCase));
            _visibleAssetDiffs.AddRange(_diffReport.AssetDiffs.Where(diff => MatchesDiffFilter(diff.Status, filter))
                .OrderByDescending(diff => diff.Delta)
                .ThenBy(diff => diff.AssetPath, StringComparer.OrdinalIgnoreCase));

            RefreshListView(_bundleDiffListView, _visibleBundleDiffs.Count, _bundleDiffEmptyState);
            RefreshListView(_assetDiffListView, _visibleAssetDiffs.Count, _assetDiffEmptyState);
            if (_visibleBundleDiffs.Count > 0 && _diffGrowthDetailsContainer.childCount == 0)
            {
                RenderGrowthDetails(_visibleBundleDiffs[0].BundleName);
            }
        }

        private static bool MatchesDiffFilter(DiffStatus status, string filter)
        {
            switch (filter)
            {
                case "Changed":
                    return status != DiffStatus.Unchanged;
                case "Added":
                    return status == DiffStatus.Added;
                case "Removed":
                    return status == DiffStatus.Removed;
                default:
                    return true;
            }
        }

        private void RenderGrowthDetails(string bundleName)
        {
            _diffGrowthDetailsContainer.Clear();
            if (_diffReport == null)
            {
                return;
            }

            BundleDiff bundleDiff = _diffReport.BundleDiffs.FirstOrDefault(diff => string.Equals(diff.BundleName, bundleName, StringComparison.OrdinalIgnoreCase));
            _diffGrowthDetailsContainer.Add(new Label(bundleName));
            if (bundleDiff != null)
            {
                _diffGrowthDetailsContainer.Add(CreateDetailLabel("Total Growth", FormatSignedBytes(bundleDiff.Delta)));
            }

            IReadOnlyList<GrowthReason> contributors = _diffReport.GrowthReasons
                .Where(reason => string.Equals(reason.BundleName, bundleName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(reason => reason.SizeDeltaBytes)
                .Take(10)
                .ToList();

            var heading = new Label("Largest Contributors");
            heading.AddToClassList("abi-detail-heading");
            _diffGrowthDetailsContainer.Add(heading);
            if (contributors.Count == 0)
            {
                _diffGrowthDetailsContainer.Add(new Label("No positive asset contributors found for this bundle."));
                return;
            }

            foreach (GrowthReason reason in contributors)
            {
                _diffGrowthDetailsContainer.Add(new Label($"{reason.AssetName}: +{ByteFormatter.FormatBytes(reason.SizeDeltaBytes)}"));
            }
        }

        private static string FormatSignedCount(int value)
        {
            return value > 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
        }

        private IEnumerable<DuplicateAssetData> SortDuplicates(IEnumerable<DuplicateAssetData> rows)
        {
            switch (_duplicateSortColumn)
            {
                case DuplicateSortColumn.Name:
                    return _duplicateSortAscending
                        ? rows.OrderBy(duplicate => duplicate.Asset.Name, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(duplicate => duplicate.Asset.Name, StringComparer.OrdinalIgnoreCase);
                case DuplicateSortColumn.BundleCount:
                    return _duplicateSortAscending
                        ? rows.OrderBy(duplicate => duplicate.BundleCount).ThenBy(duplicate => duplicate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(duplicate => duplicate.BundleCount).ThenBy(duplicate => duplicate.Asset.Path, StringComparer.OrdinalIgnoreCase);
                case DuplicateSortColumn.Size:
                    return _duplicateSortAscending
                        ? rows.OrderBy(duplicate => duplicate.Asset.SizeBytes).ThenBy(duplicate => duplicate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(duplicate => duplicate.Asset.SizeBytes).ThenBy(duplicate => duplicate.Asset.Path, StringComparer.OrdinalIgnoreCase);
                default:
                    return _duplicateSortAscending
                        ? rows.OrderBy(duplicate => duplicate.EstimatedWasteBytes).ThenBy(duplicate => duplicate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(duplicate => duplicate.EstimatedWasteBytes).ThenBy(duplicate => duplicate.Asset.Path, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void ReplaceList<T>(List<T> target, IEnumerable<T> source)
        {
            target.Clear();
            target.AddRange(source);
        }

        private static void RefreshListView(ListView listView, int rowCount, VisualElement emptyState)
        {
            bool showEmpty = rowCount == 0;
            emptyState.style.display = showEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            listView.style.display = showEmpty ? DisplayStyle.None : DisplayStyle.Flex;

            listView.Rebuild();
        }

        private void ShowStatus(string message, StatusKind kind)
        {
            _statusLabel.text = message;
            _statusLabel.EnableInClassList("abi-status-error", kind == StatusKind.Error);
            _statusLabel.EnableInClassList("abi-status-success", kind == StatusKind.Success);
        }

        private enum StatusKind
        {
            Neutral,
            Success,
            Error
        }

        private sealed class BundleRowElements
        {
            public Label Name;
            public Label Size;
            public Label AssetCount;
        }

        private sealed class AssetRowElements
        {
            public Label Name;
            public Label Path;
            public Label Size;
            public Label BundleCount;
        }

        private sealed class DuplicateRowElements
        {
            public Label Name;
            public Label BundleCount;
            public Label Size;
            public Label Waste;
        }

        private sealed class OptimizationRowElements
        {
            public Label Asset;
            public Label BundleCount;
            public Label Waste;
            public Label Savings;
            public Label Severity;
            public Label Recommendation;
        }

        private sealed class DependencySearchRowElements
        {
            public Label Name;
            public Label Path;
            public Label BundleCount;
        }

        private sealed class BundleDiffRowElements
        {
            public Label Name;
            public Label OldSize;
            public Label NewSize;
            public Label Delta;
            public Label Status;
        }

        private sealed class AssetDiffRowElements
        {
            public Label Name;
            public Label OldSize;
            public Label NewSize;
            public Label Delta;
            public Label Status;
        }

        private sealed class AiReportGenerationResult
        {
            public string Prompt;
            public OptimizationReport OptimizationReport;
        }
    }
}
