using System;
using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Models
{
    /// <summary>
    /// Parsed Addressables build layout report data.
    /// </summary>
    [Serializable]
    public sealed class BuildReportData
    {
        /// <summary>
        /// File path used to load this report.
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// UTC time when this report was parsed.
        /// </summary>
        public DateTime LoadedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bundles discovered in the report.
        /// </summary>
        public List<BundleData> Bundles { get; } = new List<BundleData>();

        /// <summary>
        /// Unique assets discovered in the report.
        /// </summary>
        public List<AssetData> Assets { get; } = new List<AssetData>();

        /// <summary>
        /// Dependency text lines captured for future analysis.
        /// </summary>
        public List<string> Dependencies { get; } = new List<string>();
    }
}
