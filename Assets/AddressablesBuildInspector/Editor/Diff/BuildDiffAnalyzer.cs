using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Utilities;

namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Compares parsed Addressables build reports and explains growth.
    /// </summary>
    public sealed class BuildDiffAnalyzer
    {
        private const long LargeAssetThresholdBytes = 10L * 1024L * 1024L;

        /// <summary>
        /// Generates a complete diff report.
        /// </summary>
        public BuildDiffReport Analyze(BuildReportData oldBuild, BuildReportData newBuild)
        {
            if (oldBuild == null)
            {
                throw new ArgumentNullException(nameof(oldBuild));
            }

            if (newBuild == null)
            {
                throw new ArgumentNullException(nameof(newBuild));
            }

            var report = new BuildDiffReport
            {
                OldBuild = oldBuild,
                NewBuild = newBuild,
                TotalSizeDelta = GetTotalBuildSize(newBuild) - GetTotalBuildSize(oldBuild)
            };

            report.BundleDiffs.AddRange(CompareBundles(oldBuild, newBuild));
            report.AssetDiffs.AddRange(CompareAssets(oldBuild, newBuild));
            report.GrowthReasons.AddRange(AnalyzeGrowthReasons(oldBuild, newBuild));
            report.DuplicateRegressions.AddRange(AnalyzeDuplicateRegression(oldBuild, newBuild));
            report.Insights.AddRange(GenerateInsights(report));
            return report;
        }

        /// <summary>
        /// Compares bundles by name.
        /// </summary>
        public IReadOnlyList<BundleDiff> CompareBundles(BuildReportData oldBuild, BuildReportData newBuild)
        {
            Dictionary<string, BundleData> oldBundles = oldBuild.Bundles
                .GroupBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, BundleData> newBundles = newBuild.Bundles
                .GroupBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            return oldBundles.Keys
                .Union(newBundles.Keys, StringComparer.OrdinalIgnoreCase)
                .Select(name =>
                {
                    oldBundles.TryGetValue(name, out BundleData oldBundle);
                    newBundles.TryGetValue(name, out BundleData newBundle);
                    long oldSize = oldBundle?.SizeBytes ?? 0;
                    long newSize = newBundle?.SizeBytes ?? 0;
                    return new BundleDiff
                    {
                        BundleName = name,
                        OldSize = oldSize,
                        NewSize = newSize,
                        Delta = newSize - oldSize,
                        Status = GetStatus(oldBundle != null, newBundle != null, oldSize, newSize)
                    };
                })
                .OrderByDescending(diff => Math.Abs(diff.Delta))
                .ThenBy(diff => diff.BundleName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Compares assets by path.
        /// </summary>
        public IReadOnlyList<AssetDiff> CompareAssets(BuildReportData oldBuild, BuildReportData newBuild)
        {
            Dictionary<string, AssetData> oldAssets = IndexAssets(oldBuild);
            Dictionary<string, AssetData> newAssets = IndexAssets(newBuild);

            return oldAssets.Keys
                .Union(newAssets.Keys, StringComparer.OrdinalIgnoreCase)
                .Select(path =>
                {
                    oldAssets.TryGetValue(path, out AssetData oldAsset);
                    newAssets.TryGetValue(path, out AssetData newAsset);
                    long oldSize = oldAsset?.SizeBytes ?? 0;
                    long newSize = newAsset?.SizeBytes ?? 0;
                    AssetData displayAsset = newAsset ?? oldAsset;
                    return new AssetDiff
                    {
                        AssetName = displayAsset?.Name ?? GetFileName(path),
                        AssetPath = path,
                        OldSize = oldSize,
                        NewSize = newSize,
                        Delta = newSize - oldSize,
                        Status = GetStatus(oldAsset != null, newAsset != null, oldSize, newSize)
                    };
                })
                .OrderByDescending(diff => Math.Abs(diff.Delta))
                .ThenBy(diff => diff.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Finds largest positive asset-level contributors inside growing bundles.
        /// </summary>
        public IReadOnlyList<GrowthReason> AnalyzeGrowthReasons(BuildReportData oldBuild, BuildReportData newBuild)
        {
            Dictionary<string, BundleData> oldBundles = oldBuild.Bundles
                .GroupBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var reasons = new List<GrowthReason>();
            foreach (BundleData newBundle in newBuild.Bundles)
            {
                oldBundles.TryGetValue(newBundle.Name, out BundleData oldBundle);
                Dictionary<string, AssetData> oldAssets = oldBundle == null
                    ? new Dictionary<string, AssetData>(StringComparer.OrdinalIgnoreCase)
                    : oldBundle.Assets.GroupBy(asset => NormalizePath(asset.Path), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

                foreach (AssetData newAsset in newBundle.Assets)
                {
                    string path = NormalizePath(newAsset.Path);
                    oldAssets.TryGetValue(path, out AssetData oldAsset);
                    long delta = newAsset.SizeBytes - (oldAsset?.SizeBytes ?? 0);
                    if (delta <= 0)
                    {
                        continue;
                    }

                    reasons.Add(new GrowthReason
                    {
                        BundleName = newBundle.Name,
                        AssetName = newAsset.Name,
                        AssetPath = path,
                        SizeDeltaBytes = delta,
                        Description = $"{newAsset.Name} added {ByteFormatter.FormatBytes(delta)} to {newBundle.Name}."
                    });
                }
            }

            return reasons
                .OrderByDescending(reason => reason.SizeDeltaBytes)
                .ThenBy(reason => reason.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Finds assets duplicated in the new build that were not duplicated in the old build.
        /// </summary>
        public IReadOnlyList<DuplicateRegression> AnalyzeDuplicateRegression(BuildReportData oldBuild, BuildReportData newBuild)
        {
            Dictionary<string, AssetData> oldAssets = IndexAssets(oldBuild);
            Dictionary<string, AssetData> newAssets = IndexAssets(newBuild);

            return newAssets.Values
                .Select(newAsset =>
                {
                    oldAssets.TryGetValue(NormalizePath(newAsset.Path), out AssetData oldAsset);
                    int oldBundleCount = GetBundleCount(oldAsset);
                    int newBundleCount = GetBundleCount(newAsset);
                    long oldWaste = CalculateDuplicateWaste(oldAsset);
                    long newWaste = CalculateDuplicateWaste(newAsset);
                    return new DuplicateRegression
                    {
                        AssetName = newAsset.Name,
                        AssetPath = newAsset.Path,
                        OldBundleCount = oldBundleCount,
                        NewBundleCount = newBundleCount,
                        WasteIntroducedBytes = Math.Max(0, newWaste - oldWaste)
                    };
                })
                .Where(regression => regression.OldBundleCount <= 1 && regression.NewBundleCount > 1 && regression.WasteIntroducedBytes > 0)
                .OrderByDescending(regression => regression.WasteIntroducedBytes)
                .ThenBy(regression => regression.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Generates ranked insights from a diff report.
        /// </summary>
        public IReadOnlyList<DiffInsight> GenerateInsights(BuildDiffReport report)
        {
            var insights = new List<DiffInsight>();
            int rank = 1;

            if (report.TotalSizeDelta > 0)
            {
                insights.Add(new DiffInsight { Rank = rank++, Message = $"Build size increased by {ByteFormatter.FormatBytes(report.TotalSizeDelta)}." });
            }
            else if (report.TotalSizeDelta < 0)
            {
                insights.Add(new DiffInsight { Rank = rank++, Message = $"Build size decreased by {ByteFormatter.FormatBytes(-report.TotalSizeDelta)}." });
            }
            else
            {
                insights.Add(new DiffInsight { Rank = rank++, Message = "Build size is unchanged." });
            }

            long duplicateWasteDelta = CalculateTotalDuplicateWaste(report.NewBuild) - CalculateTotalDuplicateWaste(report.OldBuild);
            if (duplicateWasteDelta > 0)
            {
                insights.Add(new DiffInsight { Rank = rank++, Message = $"Duplicate waste increased by {ByteFormatter.FormatBytes(duplicateWasteDelta)}." });
            }

            long positiveGrowth = report.GrowthReasons.Sum(reason => reason.SizeDeltaBytes);
            long topThreeGrowth = report.GrowthReasons.Take(3).Sum(reason => reason.SizeDeltaBytes);
            if (positiveGrowth > 0 && topThreeGrowth > 0)
            {
                long percentage = (long)Math.Round((double)topThreeGrowth / positiveGrowth * 100d);
                insights.Add(new DiffInsight { Rank = rank++, Message = $"{percentage}% of growth comes from {Math.Min(3, report.GrowthReasons.Count)} assets." });
            }

            BundleDiff largestGrowingBundle = report.BundleDiffs
                .Where(diff => diff.Delta > 0)
                .OrderByDescending(diff => diff.Delta)
                .FirstOrDefault();
            if (largestGrowingBundle != null && report.TotalSizeDelta > 0)
            {
                long percentage = (long)Math.Round((double)largestGrowingBundle.Delta / report.TotalSizeDelta * 100d);
                insights.Add(new DiffInsight { Rank = rank++, Message = $"{largestGrowingBundle.BundleName} accounts for {percentage}% of total growth." });
            }

            int largeAddedAssets = report.AssetDiffs.Count(diff => diff.Status == DiffStatus.Added && diff.NewSize >= LargeAssetThresholdBytes);
            if (largeAddedAssets > 0)
            {
                insights.Add(new DiffInsight { Rank = rank, Message = $"{largeAddedAssets.ToString(CultureInfo.InvariantCulture)} new assets exceed {ByteFormatter.FormatBytes(LargeAssetThresholdBytes)}." });
            }

            return insights;
        }

        private static DiffStatus GetStatus(bool hasOld, bool hasNew, long oldSize, long newSize)
        {
            if (!hasOld)
            {
                return DiffStatus.Added;
            }

            if (!hasNew)
            {
                return DiffStatus.Removed;
            }

            return oldSize == newSize ? DiffStatus.Unchanged : DiffStatus.Modified;
        }

        private static Dictionary<string, AssetData> IndexAssets(BuildReportData report)
        {
            return report.Assets
                .GroupBy(asset => NormalizePath(asset.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        private static long GetTotalBuildSize(BuildReportData report)
        {
            return report.Bundles.Sum(bundle => Math.Max(0, bundle.SizeBytes));
        }

        private static int GetBundleCount(AssetData asset)
        {
            return asset?.BundleNames
                .Where(bundleName => !string.IsNullOrWhiteSpace(bundleName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() ?? 0;
        }

        private static long CalculateDuplicateWaste(AssetData asset)
        {
            int bundleCount = GetBundleCount(asset);
            return Math.Max(0, bundleCount - 1) * Math.Max(0, asset?.SizeBytes ?? 0);
        }

        private static long CalculateTotalDuplicateWaste(BuildReportData report)
        {
            return report.Assets.Sum(CalculateDuplicateWaste);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string GetFileName(string path)
        {
            int slashIndex = path.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex < path.Length - 1 ? path.Substring(slashIndex + 1) : path;
        }
    }
}
