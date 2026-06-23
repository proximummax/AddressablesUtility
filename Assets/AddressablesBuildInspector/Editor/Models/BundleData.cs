using System;
using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Models
{
    /// <summary>
    /// Represents one Addressables bundle entry from a build layout report.
    /// </summary>
    [Serializable]
    public sealed class BundleData
    {
        /// <summary>
        /// Creates an empty bundle entry for serializers.
        /// </summary>
        public BundleData()
            : this(string.Empty, 0)
        {
        }

        /// <summary>
        /// Creates a bundle entry.
        /// </summary>
        /// <param name="name">Bundle file or logical bundle name.</param>
        /// <param name="sizeBytes">Bundle size in bytes.</param>
        public BundleData(string name, long sizeBytes)
        {
            Name = name ?? string.Empty;
            SizeBytes = Math.Max(0, sizeBytes);
        }

        /// <summary>
        /// Bundle file or logical bundle name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Bundle size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Inferred bundle delivery location.
        /// </summary>
        public BundleLocation Location { get; set; } = BundleLocation.Unknown;

        /// <summary>
        /// Assets reported under this bundle.
        /// </summary>
        public List<AssetData> Assets { get; } = new List<AssetData>();

        /// <summary>
        /// Number of assets reported under this bundle.
        /// </summary>
        public int AssetCount => Assets.Count;
    }
}
