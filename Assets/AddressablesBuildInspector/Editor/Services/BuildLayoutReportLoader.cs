using AddressablesBuildInspector.Editor.Parsing;
using UnityEditor;

namespace AddressablesBuildInspector.Editor.Services
{
    /// <summary>
    /// Coordinates editor file selection and build layout parsing.
    /// </summary>
    public sealed class BuildLayoutReportLoader
    {
        private readonly IBuildLayoutParser _parser;

        /// <summary>
        /// Creates a report loader.
        /// </summary>
        /// <param name="parser">Parser used for selected report files.</param>
        public BuildLayoutReportLoader(IBuildLayoutParser parser)
        {
            _parser = parser;
        }

        /// <summary>
        /// Opens a file picker and parses the selected report.
        /// </summary>
        /// <returns>Parse result. A canceled picker returns a failed result with an empty message.</returns>
        public BuildLayoutParseResult LoadFromUserSelection()
        {
            string path = EditorUtility.OpenFilePanel("Load Build Layout Report", string.Empty, "txt");
            if (string.IsNullOrEmpty(path))
            {
                return BuildLayoutParseResult.Failed(string.Empty);
            }

            return _parser.Parse(path);
        }
    }
}
