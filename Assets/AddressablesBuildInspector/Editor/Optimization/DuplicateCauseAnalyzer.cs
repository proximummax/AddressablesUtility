using System;
using System.Collections.Generic;
using System.IO;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Classifies likely causes for duplicate Addressables asset inclusion.
    /// </summary>
    public sealed class DuplicateCauseAnalyzer
    {
        private static readonly HashSet<string> TextureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr"
        };

        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav", ".mp3", ".ogg", ".aiff"
        };

        /// <summary>
        /// Classifies a duplicated asset from its type and bundle usage.
        /// </summary>
        public DuplicateCause Analyze(AssetData asset)
        {
            if (asset == null)
            {
                return DuplicateCause.Unknown;
            }

            string extension = Path.GetExtension(asset.Path);
            if (TextureExtensions.Contains(extension))
            {
                return DuplicateCause.SharedTextureAcrossGroups;
            }

            if (string.Equals(extension, ".mat", StringComparison.OrdinalIgnoreCase))
            {
                return DuplicateCause.SharedMaterialAcrossGroups;
            }

            if (string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return DuplicateCause.SharedPrefabReferences;
            }

            if (AudioExtensions.Contains(extension))
            {
                return DuplicateCause.SharedAudioAssets;
            }

            if (string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase))
            {
                return DuplicateCause.SharedScriptableObjects;
            }

            return asset.BundleNames.Count > 1
                ? DuplicateCause.CrossGroupReferences
                : DuplicateCause.AddressablesGroupMisconfiguration;
        }
    }
}
