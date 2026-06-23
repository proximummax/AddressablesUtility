using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AddressablesBuildInspector.Editor.Analysis;
using AddressablesBuildInspector.Editor.Models;
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

        private readonly List<BundleData> _allBundles = new List<BundleData>();
        private readonly List<BundleData> _visibleBundles = new List<BundleData>();
        private readonly List<AssetData> _allAssets = new List<AssetData>();
        private readonly List<AssetData> _visibleAssets = new List<AssetData>();
        private readonly List<DuplicateAssetData> _allDuplicates = new List<DuplicateAssetData>();
        private readonly List<DuplicateAssetData> _visibleDuplicates = new List<DuplicateAssetData>();

        private BuildLayoutReportLoader _loader;
        private BuildReportData _report;
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
        private Button _overviewTabButton;
        private Button _bundlesTabButton;
        private Button _assetsTabButton;
        private Button _duplicatesTabButton;
        private Label _bundlesEmptyState;
        private Label _assetsEmptyState;
        private Label _duplicatesEmptyState;
        private ListView _bundlesListView;
        private ListView _assetsListView;
        private ListView _duplicatesListView;

        private BundleSortColumn _bundleSortColumn = BundleSortColumn.Size;
        private AssetSortColumn _assetSortColumn = AssetSortColumn.Size;
        private DuplicateSortColumn _duplicateSortColumn = DuplicateSortColumn.Waste;
        private bool _bundleSortAscending;
        private bool _assetSortAscending;
        private bool _duplicateSortAscending;

        private enum ActiveTab
        {
            Overview,
            Bundles,
            Assets,
            Duplicates
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
            _loader = new BuildLayoutReportLoader(new BuildLayoutParser());

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
        }

        private void BuildToolbar()
        {
            VisualElement toolbar = rootVisualElement.Q<VisualElement>("abi-toolbar");
            toolbar.Clear();
            toolbar.AddToClassList("abi-toolbar");

            var loadButton = new Button(LoadReport)
            {
                text = "Load Build Layout",
                tooltip = "Select an Addressables Build Layout text report."
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

        private void BuildTabs()
        {
            _tabBar.Clear();
            _tabBar.AddToClassList("abi-tab-bar");

            _overviewTabButton = CreateTabButton("Overview", ActiveTab.Overview);
            _bundlesTabButton = CreateTabButton("Bundles", ActiveTab.Bundles);
            _assetsTabButton = CreateTabButton("Assets", ActiveTab.Assets);
            _duplicatesTabButton = CreateTabButton("Duplicates", ActiveTab.Duplicates);

            _tabBar.Add(_overviewTabButton);
            _tabBar.Add(_bundlesTabButton);
            _tabBar.Add(_assetsTabButton);
            _tabBar.Add(_duplicatesTabButton);
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
        }

        private void BuildBundlesTable()
        {
            _bundlesContainer.Clear();
            _bundlesContainer.Add(TableBuilder.CreateHeader(
                new TableColumnDefinition("Bundle Name", 5f, () => ToggleBundleSort(BundleSortColumn.Name), "Sort by bundle name"),
                new TableColumnDefinition("Size", 1.4f, () => ToggleBundleSort(BundleSortColumn.Size), "Sort by bundle size"),
                new TableColumnDefinition("Asset Count", 1.2f, () => ToggleBundleSort(BundleSortColumn.AssetCount), "Sort by asset count")));

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
                new TableColumnDefinition("Size", 1.2f, () => ToggleAssetSort(AssetSortColumn.Size), "Sort by asset size"),
                new TableColumnDefinition("Bundles", 1f, () => ToggleAssetSort(AssetSortColumn.BundleCount), "Sort by bundle count")));

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
                new TableColumnDefinition("Bundle Count", 1.2f, () => ToggleDuplicateSort(DuplicateSortColumn.BundleCount), "Sort by bundle count"),
                new TableColumnDefinition("Size", 1.2f, () => ToggleDuplicateSort(DuplicateSortColumn.Size), "Sort by asset size"),
                new TableColumnDefinition("Estimated Waste", 1.4f, () => ToggleDuplicateSort(DuplicateSortColumn.Waste), "Sort by estimated duplicate waste")));

            _duplicatesEmptyState = TableBuilder.CreateEmptyState("No duplicate assets found in the loaded report.");
            _duplicatesContainer.Add(_duplicatesEmptyState);

            _duplicatesListView = CreateListView(_visibleDuplicates, MakeDuplicateRow, BindDuplicateRow);
            _duplicatesContainer.Add(_duplicatesListView);
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

        private void SetReport(BuildReportData report)
        {
            _report = report;
            var analysis = new BuildAnalysisService(report);

            _allBundles.Clear();
            _allBundles.AddRange(analysis.GetLargestBundles());
            _allAssets.Clear();
            _allAssets.AddRange(analysis.GetLargestAssets());
            _allDuplicates.Clear();
            _allDuplicates.AddRange(analysis.GetDuplicateAssets());

            _searchQuery = string.Empty;
            _searchField.SetValueWithoutNotify(string.Empty);
            _sourceLabel.text = Path.GetFileName(report.SourcePath);

            RenderOverview();
            RefreshAllTables();
            SetActiveTab(ActiveTab.Overview);
            ShowStatus(
                $"Loaded {report.Bundles.Count} bundles and {report.Assets.Count} assets from {Path.GetFileName(report.SourcePath)}.",
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

        private void SetActiveTab(ActiveTab tab)
        {
            _activeTab = tab;
            _overviewContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Overview);
            _bundlesContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Bundles);
            _assetsContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Assets);
            _duplicatesContainer.EnableInClassList("abi-hidden", tab != ActiveTab.Duplicates);

            SetTabButtonState(_overviewTabButton, tab == ActiveTab.Overview);
            SetTabButtonState(_bundlesTabButton, tab == ActiveTab.Bundles);
            SetTabButtonState(_assetsTabButton, tab == ActiveTab.Assets);
            SetTabButtonState(_duplicatesTabButton, tab == ActiveTab.Duplicates);

            _searchField.SetEnabled(tab != ActiveTab.Overview);
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
            }
        }

        private void RefreshAllTables()
        {
            RefreshBundles();
            RefreshAssets();
            RefreshDuplicates();
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
    }
}
