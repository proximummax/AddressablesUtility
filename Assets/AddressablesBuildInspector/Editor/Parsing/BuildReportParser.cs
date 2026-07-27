using System;
using System.IO;

namespace AddressablesBuildInspector.Editor.Parsing
{
    /// <summary>
    /// Composite parser that selects a concrete parser for text or JSON build reports.
    /// </summary>
    public sealed class BuildReportParser : IBuildLayoutParser
    {
        private readonly BuildLayoutParser _textParser = new BuildLayoutParser();
        private readonly BuildReportJsonParser _customJsonParser = new BuildReportJsonParser();
        private readonly UnityNativeBuildLayoutJsonParser _unityNativeJsonParser = new UnityNativeBuildLayoutJsonParser(false);
        private readonly UnityNativeBuildLayoutJsonParser _unityNativeJsonDependencyParser = new UnityNativeBuildLayoutJsonParser(true);

        /// <inheritdoc />
        public BuildLayoutParseResult Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BuildLayoutParseResult.Failed("Select a Build Layout report file.");
            }

            if (IsJsonFile(filePath))
            {
                return ParseJson(filePath);
            }

            return _textParser.Parse(filePath);
        }

        /// <summary>
        /// Parses a report with dependency graph extraction enabled.
        /// </summary>
        /// <param name="filePath">Report path.</param>
        /// <returns>Parse result with dependency lines populated when available.</returns>
        public BuildLayoutParseResult ParseWithDependencies(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BuildLayoutParseResult.Failed("Select a Build Layout report file.");
            }

            if (IsJsonFile(filePath) &&
                UnityNativeBuildLayoutJsonParser.LooksLikeUnityNativeBuildLayout(filePath))
            {
                return _unityNativeJsonDependencyParser.Parse(filePath);
            }

            return Parse(filePath);
        }

        /// <summary>
        /// Parses dependency data without invoking Unity editor APIs. Intended for background loading.
        /// </summary>
        /// <param name="filePath">Report path.</param>
        /// <returns>Parse result with dependency lines populated when available.</returns>
        public BuildLayoutParseResult ParseWithDependenciesForBackground(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BuildLayoutParseResult.Failed("Select a Build Layout report file.");
            }

            if (IsJsonFile(filePath) &&
                UnityNativeBuildLayoutJsonParser.LooksLikeUnityNativeBuildLayout(filePath))
            {
                return UnityNativeBuildLayoutJsonParser.ParseViaTextExtractionOnly(filePath, true);
            }

            return Parse(filePath);
        }

        private BuildLayoutParseResult ParseJson(string filePath)
        {
            if (UnityNativeBuildLayoutJsonParser.LooksLikeUnityNativeBuildLayout(filePath))
            {
                BuildLayoutParseResult nativeResult = _unityNativeJsonParser.Parse(filePath);
                if (nativeResult.Success)
                {
                    return nativeResult;
                }
            }

            return _customJsonParser.Parse(filePath);
        }

        private static bool IsJsonFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                   LooksLikeJson(filePath);
        }

        private static bool LooksLikeJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        int value = reader.Read();
                        if (value < 0)
                        {
                            return false;
                        }

                        char character = (char)value;
                        if (char.IsWhiteSpace(character))
                        {
                            continue;
                        }

                        return character == '{' || character == '[';
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return false;
        }
    }
}
