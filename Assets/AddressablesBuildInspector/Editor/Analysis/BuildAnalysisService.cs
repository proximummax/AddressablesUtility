using System;
using System.Collections.Generic;
using System.Linq;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Analysis
{
    /// <summary>
    /// Provides derived metrics and sorted views over parsed build report data.
    /// </summary>
    public sealed class BuildAnalysisService
    {
        private readonly BuildReportData _report;

        /// <summary>
        /// Creates an analysis service for a report.
        /// </summary>
        /// <param name="report">Parsed report data.</param>
        public BuildAnalysisService(BuildReportData report)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
        }

        /// <summary>
        /// Calculates all overview metrics used by the dashboard.
        /// </summary>
        /// <returns>Overview data for the report.</returns>
        public BuildOverviewData GetOverview()
        {
            IReadOnlyList<DuplicateAssetData> duplicates = GetDuplicateAssets();

            return new BuildOverviewData
            {
                TotalBuildSizeBytes = GetTotalBuildSize(),
                BundleCount = GetBundleCount(),
                AssetCount = GetAssetCount(),
                DuplicateAssetCount = duplicates.Count,
                EstimatedDuplicateWasteBytes = CalculateDuplicateWaste(),
                LargestBundle = GetLargestBundles(1).FirstOrDefault(),
                LargestAsset = GetLargestAssets(1).FirstOrDefault()
            };
        }

        /// <summary>
        /// Gets bundles sorted by size descending.
        /// </summary>
        /// <param name="maxCount">Maximum number of bundles to return.</param>
        /// <returns>Sorted bundle list.</returns>
        public IReadOnlyList<BundleData> GetLargestBundles(int maxCount = int.MaxValue)
        {
            return _report.Bundles
                .OrderByDescending(bundle => bundle.SizeBytes)
                .ThenBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, maxCount))
                .ToList();
        }

        /// <summary>
        /// Gets assets sorted by size descending.
        /// </summary>
        /// <param name="maxCount">Maximum number of assets to return.</param>
        /// <returns>Sorted asset list.</returns>
        public IReadOnlyList<AssetData> GetLargestAssets(int maxCount = int.MaxValue)
        {
            return GetAssets()
                .OrderByDescending(asset => asset.SizeBytes)
                .ThenBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, maxCount))
                .ToList();
        }

        /// <summary>
        /// Gets assets included in more than one bundle.
        /// </summary>
        /// <returns>Duplicate assets sorted by estimated waste descending.</returns>
        public IReadOnlyList<DuplicateAssetData> GetDuplicateAssets()
        {
            return GetAssets()
                .Select(CreateDuplicate)
                .Where(duplicate => duplicate.BundleCount > 1)
                .OrderByDescending(duplicate => duplicate.EstimatedWasteBytes)
                .ThenBy(duplicate => duplicate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Calculates total duplicate waste across all duplicated assets.
        /// </summary>
        /// <returns>Estimated waste in bytes.</returns>
        public long CalculateDuplicateWaste()
        {
            return GetDuplicateAssets().Sum(duplicate => duplicate.EstimatedWasteBytes);
        }

        /// <summary>
        /// Gets the total build size using bundle sizes.
        /// </summary>
        /// <returns>Total bundle size in bytes.</returns>
        public long GetTotalBuildSize()
        {
            return _report.Bundles.Sum(bundle => Math.Max(0, bundle.SizeBytes));
        }

        /// <summary>
        /// Gets the bundle count.
        /// </summary>
        /// <returns>Number of bundles.</returns>
        public int GetBundleCount()
        {
            return _report.Bundles.Count;
        }

        /// <summary>
        /// Gets the unique asset count.
        /// </summary>
        /// <returns>Number of unique assets.</returns>
        public int GetAssetCount()
        {
            return GetAssets().Count;
        }

        private IReadOnlyList<AssetData> GetAssets()
        {
            if (_report.Assets.Count > 0)
            {
                return _report.Assets;
            }

            return _report.Bundles
                .SelectMany(bundle => bundle.Assets)
                .GroupBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static DuplicateAssetData CreateDuplicate(AssetData asset)
        {
            int bundleCount = asset.BundleNames
                .Where(bundleName => !string.IsNullOrWhiteSpace(bundleName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            long waste = Math.Max(0, bundleCount - 1) * Math.Max(0, asset.SizeBytes);
            return new DuplicateAssetData(asset, bundleCount, waste);
        }
    }
}
