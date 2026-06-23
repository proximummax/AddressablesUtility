namespace AddressablesBuildInspector.Editor.Parsing
{
    /// <summary>
    /// Parses Addressables build layout report files.
    /// </summary>
    public interface IBuildLayoutParser
    {
        /// <summary>
        /// Parses a build layout report file.
        /// </summary>
        /// <param name="filePath">Report file path.</param>
        /// <returns>Parse result with either report data or a friendly error.</returns>
        BuildLayoutParseResult Parse(string filePath);
    }
}
