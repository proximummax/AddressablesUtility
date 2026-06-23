using System;
using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Models
{
    /// <summary>
    /// Represents one asset entry discovered in an Addressables build layout report.
    /// </summary>
    [Serializable]
    public sealed class AssetData
    {
        /// <summary>
        /// Creates an empty asset entry for serializers.
        /// </summary>
        public AssetData()
            : this(string.Empty, string.Empty, 0)
        {
        }

        /// <summary>
        /// Creates an asset entry with the supplied identity and size.
        /// </summary>
        /// <param name="name">Display name for the asset.</param>
        /// <param name="path">Project-relative asset path.</param>
        /// <param name="sizeBytes">Asset size in bytes.</param>
        public AssetData(string name, string path, long sizeBytes)
        {
            Name = name ?? string.Empty;
            Path = path ?? string.Empty;
            SizeBytes = Math.Max(0, sizeBytes);
        }

        /// <summary>
        /// Display name, usually the final segment of <see cref="Path"/>.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Project-relative asset path such as <c>Assets/Textures/Icon.png</c>.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Reported asset size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Names of bundles that include this asset.
        /// </summary>
        public List<string> BundleNames { get; } = new List<string>();

        /// <summary>
        /// Number of unique bundles that include this asset.
        /// </summary>
        public int BundleCount => BundleNames.Count;
    }
}
