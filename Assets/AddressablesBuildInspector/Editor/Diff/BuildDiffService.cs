using System;
using AddressablesBuildInspector.Editor.Parsing;

namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Loads two build layout reports and compares them.
    /// </summary>
    public sealed class BuildDiffService
    {
        private readonly IBuildLayoutParser _parser;
        private readonly BuildDiffAnalyzer _analyzer;

        /// <summary>
        /// Creates a build diff service.
        /// </summary>
        public BuildDiffService(IBuildLayoutParser parser, BuildDiffAnalyzer analyzer = null)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _analyzer = analyzer ?? new BuildDiffAnalyzer();
        }

        /// <summary>
        /// Compares two build layout report files.
        /// </summary>
        public BuildDiffServiceResult CompareBuilds(string oldBuildPath, string newBuildPath)
        {
            BuildLayoutParseResult oldResult = _parser.Parse(oldBuildPath);
            if (!oldResult.Success)
            {
                return BuildDiffServiceResult.Failed($"Old build: {oldResult.ErrorMessage}");
            }

            BuildLayoutParseResult newResult = _parser.Parse(newBuildPath);
            if (!newResult.Success)
            {
                return BuildDiffServiceResult.Failed($"New build: {newResult.ErrorMessage}");
            }

            return BuildDiffServiceResult.Succeeded(_analyzer.Analyze(oldResult.Report, newResult.Report));
        }
    }

    /// <summary>
    /// Result returned by <see cref="BuildDiffService"/>.
    /// </summary>
    public sealed class BuildDiffServiceResult
    {
        private BuildDiffServiceResult(bool success, BuildDiffReport report, string errorMessage)
        {
            Success = success;
            Report = report;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>True when comparison succeeded.</summary>
        public bool Success { get; }

        /// <summary>Diff report when successful.</summary>
        public BuildDiffReport Report { get; }

        /// <summary>Error message when comparison failed.</summary>
        public string ErrorMessage { get; }

        /// <summary>Creates a successful result.</summary>
        public static BuildDiffServiceResult Succeeded(BuildDiffReport report)
        {
            return new BuildDiffServiceResult(true, report, string.Empty);
        }

        /// <summary>Creates a failed result.</summary>
        public static BuildDiffServiceResult Failed(string errorMessage)
        {
            return new BuildDiffServiceResult(false, null, errorMessage);
        }
    }
}
