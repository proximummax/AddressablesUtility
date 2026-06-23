using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Parsing
{
    /// <summary>
    /// Result returned by a build layout parser.
    /// </summary>
    public sealed class BuildLayoutParseResult
    {
        private BuildLayoutParseResult(bool success, BuildReportData report, string errorMessage)
        {
            Success = success;
            Report = report;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>
        /// True when parsing completed successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Parsed report. This is null when <see cref="Success"/> is false.
        /// </summary>
        public BuildReportData Report { get; }

        /// <summary>
        /// Friendly error message. This is empty when <see cref="Success"/> is true.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Creates a successful parse result.
        /// </summary>
        /// <param name="report">Parsed report data.</param>
        /// <returns>Successful parse result.</returns>
        public static BuildLayoutParseResult Succeeded(BuildReportData report)
        {
            return new BuildLayoutParseResult(true, report, string.Empty);
        }

        /// <summary>
        /// Creates a failed parse result.
        /// </summary>
        /// <param name="errorMessage">Friendly error message.</param>
        /// <returns>Failed parse result.</returns>
        public static BuildLayoutParseResult Failed(string errorMessage)
        {
            return new BuildLayoutParseResult(false, null, errorMessage);
        }
    }
}
