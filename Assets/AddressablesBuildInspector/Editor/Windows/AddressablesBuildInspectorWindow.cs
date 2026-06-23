using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        private const string LocationFilterAll = "All";
        private const string LocationFilterRemote = "Remote";
        private const string LocationFilterLocal = "Local";

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
        private readonly Dictionary<string, BundleLocation> _bundleLocationsByName = new Dictionary<string, BundleLocation>(StringComparer.OrdinalIgnoreCase);

        private BuildLayoutReportLoader _loader;
        private BuildDiffService _buildDiffService;
        private DiffExportService _diffExportService;
        private DuplicateOptimizationService _optimizationService;
        private OptimizationReportExportService _optimizationExportService;
        private BuildReportData _report;
        private BuildDiffReport _diffReport;
        private OptimizationReport _optimizationReport;
        private DependencyGraphService _dependencyGraphService;
        private DependencySearchService _dependencySearchService;
        private DependencyTreeBuilder _dependencyTreeBuilder;
        private ActiveTab _activeTab = ActiveTab.Overview;
        private string _searchQuery = string.Empty;
        private string _locationFilter = LocationFilterAll;

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
        private Button _overviewTabButton;
        private Button _bundlesTabButton;
        private Button _assetsTabButton;
        private Button _duplicatesTabButton;
        private Button _optimizationTabButton;
        private Button _dependenciesTabButton;
        private Button _buildDiffTabButton;
        private Label _bundlesEmptyState;
        private Label _assetsEmptyState;
        private Label _duplicatesEmptyState;
        private ListView _bundlesListView;
        private ListView _assetsListView;
        private ListView _duplicatesListView;
        private VisualElement _locationFilterBar;
        private Button _locationFilterAllButton;
        private Button _locationFilterRemoteButton;
        private Button _locationFilterLocalButton;
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

        private enum ActiveTab
        {
            Overview,
            Bundles,
            Assets,
            Duplicates,
            Optimization,
            Dependencies,
            BuildDiff
        }

        private enum BundleSortColumn
        {
            Name,
            Location,
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
            var parser = new BuildReportParser();
            _loader = new BuildLayoutReportLoader(parser);
            _buildDiffService = new BuildDiffService(parser);
            _diffExportService = new DiffExportService();
            _optimizationExportService = new OptimizationReportExportService();

            rootVisualElement.Clear();
            LoadVisualTree();
            CacheElements();
            BuildToolbar();
            BuildTabs();
            BuildLocationFilterBar();
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

            var filterBar = new VisualElement { name = "abi-filter-bar" };
            filterBar.AddToClassList("abi-filter-bar");
            filterBar.Add(new Label { text = "Location" });
            filterBar.Add(new VisualElement { name = "abi-location-filter-group" });
            root.Add(filterBar);

            var content = new VisualElement { name = "abi-content" };
            content.Add(new VisualElement { name = "abi-overview-tab" });
            content.Add(new VisualElement { name = "abi-bundles-tab" });
            content.Add(new VisualElement { name = "abi-assets-tab" });
            content.Add(new VisualElement { name = "abi-duplicates-tab" });
            content.Add(new VisualElement { name = "abi-optimization-tab" });
            content.Add(new VisualElement { name = "abi-dependencies-tab" });
            content.Add(new VisualElement { name = "abi-build-diff-tab" });
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
            toolbar.Add(_sourceLabel);
            toolbar.Add(_searchField);
        }

        private void BuildLocationFilterBar()
        {
            _locationFilterBar = rootVisualElement.Q<VisualElement>("abi-filter-bar");
            VisualElement group = rootVisualElement.Q<VisualElement>("abi-location-filter-group");
            group.Clear();

            _locationFilterAllButton = CreateLocationFilterButton(LocationFilterAll);
            _locationFilterRemoteButton = CreateLocationFilterButton(LocationFilterRemote);
            _locationFilterLocalButton = CreateLocationFilterButton(LocationFilterLocal);

            group.Add(_locationFilterAllButton);
            group.Add(_locationFilterRemoteButton);
            group.Add(_locationFilterLocalButton);

            SetLocationFilter(LocationFilterAll, refreshTable: false);
        }

        private Button CreateLocationFilterButton(string filter)
        {
            var button = new Button(() => SetLocationFilter(filter))
            {
                text = filter,
                tooltip = "Filter table rows by local or remote Addressables bundles."
            };
            button.AddToClassList("abi-location-filter-button");
            return button;
        }

        private void SetLocationFilter(string filter, bool refreshTable = true)
        {
            _locationFilter = filter ?? LocationFilterAll;
            SetLocationFilterButtonState(_locationFilterAllButton, string.Equals(_locationFilter, LocationFilterAll, StringComparison.OrdinalIgnoreCase));
            SetLocationFilterButtonState(_locationFilterRemoteButton, string.Equals(_locationFilter, LocationFilterRemote, StringComparison.OrdinalIgnoreCase));
            SetLocationFilterButtonState(_locationFilterLocalButton, string.Equals(_locationFilter, LocationFilterLocal, StringComparison.OrdinalIgnoreCase));

            if (refreshTable)
            {
                RefreshActiveTable();
            }
        }

        private static void SetLocationFilterButtonState(Button button, bool active)
        {
            button.EnableInClassList("abi-location-filter-button-active", active);
        }

        private void BuildTabs()
        {
            _tabBar.Clear();
            _tabBar.AddToClassList("abi-tab-bar");

            _overviewTabButton = CreateTabButton("Overview", ActiveTab.Overview);
            _bundlesTabButton = CreateTabButton("Bundles", ActiveTab.Bundles);
            _assetsTabButton = CreateTabButton("Assets", ActiveTab.Assets);
            _duplicatesTabButton = CreateTabButton("Duplicates", ActiveTab.Duplicates);
            _optimizationTabButton = CreateTabButton("Optimization", ActiveTab.Optimization);
            _dependenciesTabButton = CreateTabButton("Dependencies", ActiveTab.Dependencies);
            _buildDiffTabButton = CreateTabButton("Build Diff", ActiveTab.BuildDiff);

            _tabBar.Add(_overviewTabButton);
            _tabBar.Add(_bundlesTabButton);
            _tabBar.Add(_assetsTabButton);
            _tabBar.Add(_duplicatesTabButton);
            _tabBar.Add(_optimizationTabButton);
            _tabBar.Add(_dependenciesTabButton);
            _tabBar.Add(_buildDiffTabButton);
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
        }

        private void BuildBundlesTable()
        {
            _bundlesContainer.Clear();
            _bundlesContainer.Add(TableBuilder.CreateHeader(
                new TableColumnDefinition("Bundle Name", 5f, () => ToggleBundleSort(BundleSortColumn.Name), "Sort by bundle name"),
                new TableColumnDefinition("Location", 1.1f, () => ToggleBundleSort(BundleSortColumn.Location), "Sort by local or remote location"),
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
            controls.Add(exportCsvButton);
            controls.Add(exportJsonButton);
            controls.Add(exportMarkdownButton);
            _optimizationContainer.Add(controls);

            _optimizationDashboardContainer = new VisualElement();
            _optimizationDashboardContainer.AddToClassList("abi-overview-grid");
            _optimizationContainer.Add(_optimizationDashboardContainer);

            _optimizationInsightsContainer = new VisualElement();
            _optimizationInsightsContainer.AddToClassList("abi-optimization-insights");
            _optimizationContainer.Add(_optimizationInsightsContainer);

            var tableLayout = new VisualElement();
            tableLayout.AddToClassList("abi-optimization-layout");

            var candidatePanel = new VisualElement();
            candidatePanel.AddToClassList("abi-optimization-table-panel");
            candidatePanel.Add(CreatePaneHeading("Optimization Candidates"));
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

            _optimizationDetailsContainer = new VisualElement();
            _optimizationDetailsContainer.AddToClassList("abi-optimization-details");
            _optimizationDetailsContainer.Add(TableBuilder.CreateEmptyState("Select a candidate to inspect cause, dependency paths, and simulated savings."));

            tableLayout.Add(candidatePanel);
            tableLayout.Add(_optimizationDetailsContainer);
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

            _diffInsightsContainer = new VisualElement();
            _diffInsightsContainer.AddToClassList("abi-diff-insights");
            _buildDiffContainer.Add(_diffInsightsContainer);

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

            _diffGrowthDetailsContainer = new VisualElement();
            _diffGrowthDetailsContainer.AddToClassList("abi-diff-growth-details");
            _buildDiffContainer.Add(_diffGrowthDetailsContainer);

            RenderDiffOverview();
            RefreshDiffTables();
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
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None
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
                Location = TableBuilder.CreateCell("bundle-location", 1.1f),
                Size = TableBuilder.CreateCell("bundle-size", 1.4f, true),
                AssetCount = TableBuilder.CreateCell("bundle-asset-count", 1.2f, true)
            };

            row.userData = cells;
            row.Add(cells.Name);
            row.Add(cells.Location);
            row.Add(cells.Size);
            row.Add(cells.AssetCount);
            return row;
        }

        private void BindBundleRow(VisualElement row, int index)
        {
            var cells = (BundleRowElements)row.userData;
            BundleData bundle = _visibleBundles[index];

            cells.Name.text = bundle.Name;
            cells.Location.text = FormatBundleLocation(bundle.Location);
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
            cells.Severity.text = candidate.Severity.ToString();
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
            _bundleLocationsByName.Clear();
            foreach (BundleData bundle in report.Bundles)
            {
                _bundleLocationsByName[bundle.Name] = bundle.Location;
            }

            _dependencyGraphService = new DependencyGraphService(
                report,
                new BuildLayoutDependencyProvider(report),
                new AssetDatabaseDependencyProvider(report));
            _dependencySearchService = new DependencySearchService(_dependencyGraphService);
            _dependencyTreeBuilder = new DependencyTreeBuilder(_dependencyGraphService);
            _optimizationService = new DuplicateOptimizationService(_dependencyGraphService);
            _optimizationReport = null;

            _searchQuery = string.Empty;
            _searchField.SetValueWithoutNotify(string.Empty);
            SetLocationFilter(LocationFilterAll, refreshTable: false);
            _dependencySearchField.SetValueWithoutNotify(string.Empty);
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

            var grid = new VisualElement();
            grid.AddToClassList("abi-overview-grid");
            _overviewContainer.Add(grid);

            AddStatCard(grid, "Total Build Size", ByteFormatter.FormatBytes(overview.TotalBuildSizeBytes));
            AddStatCard(grid, "Bundle Count", overview.BundleCount.ToString(CultureInfo.InvariantCulture));
            AddStatCard(grid, "Asset Count", overview.AssetCount.ToString(CultureInfo.InvariantCulture));
            AddStatCard(grid, "Duplicate Assets", overview.DuplicateAssetCount.ToString(CultureInfo.InvariantCulture));
            AddStatCard(grid, "Duplicate Waste", ByteFormatter.FormatBytes(overview.EstimatedDuplicateWasteBytes));
            AddStatCard(grid, "Largest Bundle", FormatLargestBundle(overview.LargestBundle));
            AddStatCard(grid, "Largest Asset", FormatLargestAsset(overview.LargestAsset));

            int remoteBundleCount = _report.Bundles.Count(bundle => bundle.Location == BundleLocation.Remote);
            int localBundleCount = _report.Bundles.Count(bundle => bundle.Location == BundleLocation.Local);
            int unknownBundleCount = _report.Bundles.Count(bundle => bundle.Location == BundleLocation.Unknown);
            AddStatCard(grid, "Remote Bundles", remoteBundleCount.ToString(CultureInfo.InvariantCulture));
            AddStatCard(grid, "Local Bundles", localBundleCount.ToString(CultureInfo.InvariantCulture));
            AddStatCard(grid, "Unknown Location", unknownBundleCount.ToString(CultureInfo.InvariantCulture));

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
        }

        private static void AddStatCard(VisualElement parent, string label, string value)
        {
            var card = new VisualElement();
            card.AddToClassList("abi-stat-card");

            var labelElement = new Label(label);
            labelElement.AddToClassList("abi-stat-label");

            var valueElement = new Label(value);
            valueElement.AddToClassList("abi-stat-value");

            card.Add(labelElement);
            card.Add(valueElement);
            parent.Add(card);
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

        private bool MatchesLocationFilter(BundleData bundle)
        {
            if (bundle == null || string.Equals(_locationFilter, LocationFilterAll, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(FormatBundleLocation(bundle.Location), _locationFilter, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesLocationFilter(AssetData asset)
        {
            if (asset == null || string.Equals(_locationFilter, LocationFilterAll, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            BundleLocation requestedLocation = string.Equals(_locationFilter, LocationFilterRemote, StringComparison.OrdinalIgnoreCase)
                ? BundleLocation.Remote
                : BundleLocation.Local;

            return asset.BundleNames
                .Select(ResolveBundleLocation)
                .Any(location => location == requestedLocation);
        }

        private BundleLocation ResolveBundleLocation(string bundleName)
        {
            return _bundleLocationsByName.TryGetValue(bundleName ?? string.Empty, out BundleLocation location)
                ? location
                : BundleLocation.Unknown;
        }

        private static string FormatBundleLocation(BundleLocation location)
        {
            switch (location)
            {
                case BundleLocation.Local:
                    return LocationFilterLocal;
                case BundleLocation.Remote:
                    return LocationFilterRemote;
                default:
                    return "Unknown";
            }
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

            SetTabButtonState(_overviewTabButton, tab == ActiveTab.Overview);
            SetTabButtonState(_bundlesTabButton, tab == ActiveTab.Bundles);
            SetTabButtonState(_assetsTabButton, tab == ActiveTab.Assets);
            SetTabButtonState(_duplicatesTabButton, tab == ActiveTab.Duplicates);
            SetTabButtonState(_optimizationTabButton, tab == ActiveTab.Optimization);
            SetTabButtonState(_dependenciesTabButton, tab == ActiveTab.Dependencies);
            SetTabButtonState(_buildDiffTabButton, tab == ActiveTab.BuildDiff);

            bool tableSearchEnabled = tab == ActiveTab.Bundles ||
                                      tab == ActiveTab.Assets ||
                                      tab == ActiveTab.Duplicates ||
                                      tab == ActiveTab.Optimization;
            _searchField.SetEnabled(tableSearchEnabled);
            _locationFilterBar.SetEnabled(tableSearchEnabled);
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
                _bundleSortAscending = column == BundleSortColumn.Name || column == BundleSortColumn.Location;
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
                    EnsureOptimizationReport();
                    RenderOptimizationDashboard();
                    RefreshOptimizationTable();
                    break;
                case ActiveTab.Dependencies:
                    RefreshDependencySearch();
                    break;
                case ActiveTab.BuildDiff:
                    RefreshDiffTables();
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
                .Where(bundle => SearchUtility.Matches(_searchQuery, bundle.Name))
                .Where(MatchesLocationFilter);

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
                case BundleSortColumn.Location:
                    return _bundleSortAscending
                        ? rows.OrderBy(bundle => bundle.Location).ThenBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(bundle => bundle.Location).ThenBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase);
                default:
                    return _bundleSortAscending
                        ? rows.OrderBy(bundle => bundle.SizeBytes).ThenBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
                        : rows.OrderByDescending(bundle => bundle.SizeBytes).ThenBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase);
            }
        }

        private void RefreshAssets()
        {
            IEnumerable<AssetData> rows = _allAssets
                .Where(asset => SearchUtility.Matches(_searchQuery, asset.Name, asset.Path))
                .Where(MatchesLocationFilter);

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
                .Where(duplicate => SearchUtility.Matches(_searchQuery, duplicate.Asset.Name, duplicate.Asset.Path))
                .Where(duplicate => MatchesLocationFilter(duplicate.Asset));

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

            _optimizationReport = _optimizationService.Analyze(_report);
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

            AddStatCard(_optimizationDashboardContainer, "Duplicate Waste", ByteFormatter.FormatBytes(_optimizationReport.TotalDuplicateWasteBytes));
            AddStatCard(_optimizationDashboardContainer, "Potential Savings", ByteFormatter.FormatBytes(_optimizationReport.PotentialSavingsBytes));
            AddStatCard(_optimizationDashboardContainer, "Candidates", _optimizationReport.Candidates.Count.ToString(CultureInfo.InvariantCulture));
            AddStatCard(_optimizationDashboardContainer, "Critical Issues", _optimizationReport.CriticalIssueCount.ToString(CultureInfo.InvariantCulture));
            AddStatCard(_optimizationDashboardContainer, "Predicted Waste", ByteFormatter.FormatBytes(_optimizationReport.Simulation.PredictedDuplicateWasteBytes));

            var heading = CreatePaneHeading("Optimization Insights");
            _optimizationInsightsContainer.Add(heading);
            if (_optimizationReport.Insights.Count == 0)
            {
                _optimizationInsightsContainer.Add(new Label("No duplicate optimization issues found."));
                return;
            }

            foreach (OptimizationInsight insight in _optimizationReport.Insights)
            {
                _optimizationInsightsContainer.Add(new Label($"{insight.Rank}. {insight.Message}"));
            }
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
                .Where(candidate => SearchUtility.Matches(_searchQuery, candidate.Asset.Name, candidate.Asset.Path))
                .Where(candidate => MatchesLocationFilter(candidate.Asset));

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
            _optimizationDetailsContainer.Add(CreatePaneHeading("Candidate Details"));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Asset", candidate.Asset?.Name ?? "Unknown"));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Path", candidate.Asset?.Path ?? string.Empty));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Bundle Count", candidate.BundleCount.ToString(CultureInfo.InvariantCulture)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Duplicate Waste", ByteFormatter.FormatBytes(candidate.DuplicateWasteBytes)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Potential Savings", ByteFormatter.FormatBytes(candidate.Impact.PotentialSavingsBytes)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Severity", candidate.Severity.ToString()));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Cause", FormatCause(candidate.Cause)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Recommendation", FormatRecommendation(candidate.Recommendation)));
            _optimizationDetailsContainer.Add(CreateDetailLabel("Confidence", candidate.Impact.Confidence.ToString("P0", CultureInfo.InvariantCulture)));

            var bundlesHeading = CreatePaneHeading("Affected Bundles");
            _optimizationDetailsContainer.Add(bundlesHeading);
            foreach (string bundleName in candidate.Asset.BundleNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                _optimizationDetailsContainer.Add(new Label($"{bundleName} ({FormatBundleLocation(ResolveBundleLocation(bundleName))})"));
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

        private void RefreshDependencySearch()
        {
            _dependencySearchResults.Clear();
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
                _dependencyDetailsContainer.Add(new Label("No direct references found."));
            }
            else
            {
                foreach (DependencyNode reference in referencedBy.Take(20))
                {
                    _dependencyDetailsContainer.Add(new Label(reference.AssetPath));
                }
            }
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
            _diffOverviewContainer.Add(grid);
            AddStatCard(grid, "Old Build Size", ByteFormatter.FormatBytes(oldSize));
            AddStatCard(grid, "New Build Size", ByteFormatter.FormatBytes(newSize));
            AddStatCard(grid, "Total Growth", FormatSignedBytes(_diffReport.TotalSizeDelta));
            AddStatCard(grid, "Bundle Count Delta", FormatSignedCount(_diffReport.NewBuild.Bundles.Count - _diffReport.OldBuild.Bundles.Count));
            AddStatCard(grid, "Asset Count Delta", FormatSignedCount(_diffReport.NewBuild.Assets.Count - _diffReport.OldBuild.Assets.Count));
            AddStatCard(grid, "Duplicate Waste Delta", FormatSignedBytes(newWaste - oldWaste));
        }

        private void RenderDiffInsights()
        {
            _diffInsightsContainer.Clear();
            if (_diffReport == null)
            {
                return;
            }

            var heading = new Label("Smart Insights");
            heading.AddToClassList("abi-detail-heading");
            _diffInsightsContainer.Add(heading);
            foreach (DiffInsight insight in _diffReport.Insights)
            {
                _diffInsightsContainer.Add(new Label($"{insight.Rank}. {insight.Message}"));
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
            public Label Location;
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
    }
}
