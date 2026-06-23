using System;
using System.IO;
using System.Text;
using AddressablesBuildInspector.Editor.Utilities;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Exports optimization reports to reviewable text formats.
    /// </summary>
    public sealed class OptimizationReportExportService
    {
        /// <summary>
        /// Writes a CSV optimization report.
        /// </summary>
        public void ExportCsv(OptimizationReport report, string path)
        {
            File.WriteAllText(path, ToCsv(report), Encoding.UTF8);
        }

        /// <summary>
        /// Writes a JSON optimization report.
        /// </summary>
        public void ExportJson(OptimizationReport report, string path)
        {
            File.WriteAllText(path, ToJson(report), Encoding.UTF8);
        }

        /// <summary>
        /// Writes a Markdown optimization report.
        /// </summary>
        public void ExportMarkdown(OptimizationReport report, string path)
        {
            File.WriteAllText(path, ToMarkdown(report), Encoding.UTF8);
        }

        /// <summary>
        /// Converts an optimization report to CSV.
        /// </summary>
        public string ToCsv(OptimizationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Asset,Path,Bundles,Waste,Potential Savings,Severity,Cause,Recommendation");
            foreach (OptimizationCandidate candidate in report.Candidates)
            {
                builder.AppendLine(string.Join(",",
                    Escape(candidate.Asset?.Name),
                    Escape(candidate.Asset?.Path),
                    candidate.BundleCount,
                    candidate.DuplicateWasteBytes,
                    candidate.Impact.PotentialSavingsBytes,
                    candidate.Severity,
                    candidate.Cause,
                    candidate.Recommendation));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Converts an optimization report to JSON.
        /// </summary>
        public string ToJson(OptimizationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine($"  \"totalDuplicateWasteBytes\": {report.TotalDuplicateWasteBytes},");
            builder.AppendLine($"  \"potentialSavingsBytes\": {report.PotentialSavingsBytes},");
            builder.AppendLine("  \"candidates\": [");
            for (int i = 0; i < report.Candidates.Count; i++)
            {
                OptimizationCandidate candidate = report.Candidates[i];
                builder.Append("    { ");
                builder.Append($"\"assetName\": \"{EscapeJson(candidate.Asset?.Name)}\", ");
                builder.Append($"\"assetPath\": \"{EscapeJson(candidate.Asset?.Path)}\", ");
                builder.Append($"\"bundleCount\": {candidate.BundleCount}, ");
                builder.Append($"\"duplicateWasteBytes\": {candidate.DuplicateWasteBytes}, ");
                builder.Append($"\"potentialSavingsBytes\": {candidate.Impact.PotentialSavingsBytes}, ");
                builder.Append($"\"severity\": \"{candidate.Severity}\", ");
                builder.Append($"\"cause\": \"{candidate.Cause}\", ");
                builder.Append($"\"recommendation\": \"{candidate.Recommendation}\"");
                builder.Append(" }");
                builder.AppendLine(i == report.Candidates.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Converts an optimization report to Markdown.
        /// </summary>
        public string ToMarkdown(OptimizationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("# Addressables Duplicate Optimization Report");
            builder.AppendLine();
            builder.AppendLine($"Total duplicate waste: {ByteFormatter.FormatBytes(report.TotalDuplicateWasteBytes)}");
            builder.AppendLine($"Potential savings: {ByteFormatter.FormatBytes(report.PotentialSavingsBytes)}");
            builder.AppendLine();
            builder.AppendLine("| Asset | Bundles | Waste | Recommendation |");
            builder.AppendLine("| --- | ---: | ---: | --- |");
            foreach (OptimizationCandidate candidate in report.Candidates)
            {
                builder.AppendLine($"| {EscapeMarkdown(candidate.Asset?.Path)} | {candidate.BundleCount} | {ByteFormatter.FormatBytes(candidate.DuplicateWasteBytes)} | {candidate.Recommendation} |");
            }

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

        private static string EscapeMarkdown(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|");
        }
    }
}
