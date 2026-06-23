using System;
using System.IO;
using AddressablesBuildInspector.Editor.Services;

namespace AddressablesBuildInspector.Editor.Parsing
{
    /// <summary>
    /// Composite parser that selects a concrete parser for text or JSON build reports.
    /// </summary>
    public sealed class BuildReportParser : IBuildLayoutParser
    {
        private readonly BuildLayoutParser _textParser = new BuildLayoutParser();
        private readonly BuildReportJsonParser _customJsonParser = new BuildReportJsonParser();
        private readonly UnityNativeBuildLayoutJsonParser _unityNativeJsonParser = new UnityNativeBuildLayoutJsonParser();
        private readonly BundleLocationEnrichmentService _locationEnrichmentService = new BundleLocationEnrichmentService();

        /// <inheritdoc />
        public BuildLayoutParseResult Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BuildLayoutParseResult.Failed("Select a Build Layout report file.");
            }

            BuildLayoutParseResult result;
            if (IsJsonFile(filePath))
            {
                result = ParseJson(filePath);
            }
            else
            {
                result = _textParser.Parse(filePath);
            }

            if (result.Success)
            {
                _locationEnrichmentService.Enrich(result.Report);
            }

            return result;
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
