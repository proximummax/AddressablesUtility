using System;
using System.IO;
using System.Text;
using AddressablesBuildInspector.Editor.Utilities;

namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Exports build diff reports to text formats.
    /// </summary>
    public sealed class DiffExportService
    {
        /// <summary>
        /// Writes a CSV report to disk.
        /// </summary>
        public void ExportCsv(BuildDiffReport report, string filePath)
        {
            File.WriteAllText(filePath, ToCsv(report), Encoding.UTF8);
        }

        /// <summary>
        /// Writes a JSON report to disk.
        /// </summary>
        public void ExportJson(BuildDiffReport report, string filePath)
        {
            File.WriteAllText(filePath, ToJson(report), Encoding.UTF8);
        }

        /// <summary>
        /// Converts a diff report to CSV.
        /// </summary>
        public string ToCsv(BuildDiffReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Section,Name,Old Size,New Size,Delta,Status");
            foreach (BundleDiff diff in report.BundleDiffs)
            {
                builder.AppendLine($"Bundle,{Escape(diff.BundleName)},{diff.OldSize},{diff.NewSize},{diff.Delta},{diff.Status}");
            }

            foreach (AssetDiff diff in report.AssetDiffs)
            {
                builder.AppendLine($"Asset,{Escape(diff.AssetPath)},{diff.OldSize},{diff.NewSize},{diff.Delta},{diff.Status}");
            }

            builder.AppendLine();
            builder.AppendLine("Growth Reason,Bundle,Asset,Delta,Description");
            foreach (GrowthReason reason in report.GrowthReasons)
            {
                builder.AppendLine($"Growth,{Escape(reason.BundleName)},{Escape(reason.AssetPath)},{reason.SizeDeltaBytes},{Escape(reason.Description)}");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Converts a diff report to a compact JSON document.
        /// </summary>
        public string ToJson(BuildDiffReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine($"  \"totalSizeDelta\": {report.TotalSizeDelta},");
            builder.AppendLine($"  \"totalSizeDeltaFormatted\": \"{EscapeJson(ByteFormatter.FormatBytes(Math.Abs(report.TotalSizeDelta)))}\",");
            builder.AppendLine("  \"insights\": [");
            for (int i = 0; i < report.Insights.Count; i++)
            {
                DiffInsight insight = report.Insights[i];
                builder.Append($"    {{ \"rank\": {insight.Rank}, \"message\": \"{EscapeJson(insight.Message)}\" }}");
                builder.AppendLine(i == report.Insights.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"bundleDiffs\": [");
            for (int i = 0; i < report.BundleDiffs.Count; i++)
            {
                BundleDiff diff = report.BundleDiffs[i];
                builder.Append($"    {{ \"bundleName\": \"{EscapeJson(diff.BundleName)}\", \"oldSize\": {diff.OldSize}, \"newSize\": {diff.NewSize}, \"delta\": {diff.Delta}, \"status\": \"{diff.Status}\" }}");
                builder.AppendLine(i == report.BundleDiffs.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            string safe = value ?? string.Empty;
            return safe.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? $"\"{safe.Replace("\"", "\"\"")}\""
                : safe;
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
