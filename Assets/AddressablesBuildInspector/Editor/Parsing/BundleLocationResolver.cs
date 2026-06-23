using System;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Parsing
{
    /// <summary>
    /// Resolves Addressables bundle delivery location from profile paths.
    /// </summary>
    public static class BundleLocationResolver
    {
        private const string LocalBuildPathMarker = "[UnityEngine.AddressableAssets.Addressables.BuildPath]";
        private const string LocalRuntimePathMarker = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}";

        /// <summary>
        /// Resolves bundle location from an optional location label and load path.
        /// </summary>
        public static BundleLocation Resolve(string location, string loadPath, bool isRemote)
        {
            if (isRemote)
            {
                return BundleLocation.Remote;
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                BundleLocation fromLocation = ResolveFromPathText(location);
                if (fromLocation != BundleLocation.Unknown)
                {
                    return fromLocation;
                }
            }

            return ResolveFromLoadPath(loadPath);
        }

        /// <summary>
        /// Resolves bundle location from a resolved or profile load path.
        /// </summary>
        public static BundleLocation ResolveFromLoadPath(string loadPath)
        {
            return ResolveFromPathText(loadPath);
        }

        /// <summary>
        /// Resolves bundle location from resolved build and load profile paths.
        /// </summary>
        public static BundleLocation ResolveFromBuildAndLoadPaths(string buildPath, string loadPath)
        {
            BundleLocation fromLoadPath = ResolveFromLoadPath(loadPath);
            if (fromLoadPath != BundleLocation.Unknown)
            {
                return fromLoadPath;
            }

            bool buildLocal = ContainsMarker(buildPath, LocalBuildPathMarker);
            bool loadLocal = ContainsMarker(loadPath, LocalRuntimePathMarker);
            if (buildLocal && loadLocal)
            {
                return BundleLocation.Local;
            }

            if (!buildLocal && !loadLocal &&
                (!string.IsNullOrWhiteSpace(buildPath) || !string.IsNullOrWhiteSpace(loadPath)))
            {
                return BundleLocation.Remote;
            }

            return BundleLocation.Unknown;
        }

        private static BundleLocation ResolveFromPathText(string pathText)
        {
            if (string.IsNullOrWhiteSpace(pathText))
            {
                return BundleLocation.Unknown;
            }

            if (pathText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                pathText.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return BundleLocation.Remote;
            }

            if (pathText.IndexOf("remote", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pathText.IndexOf("ServerData", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BundleLocation.Remote;
            }

            if (ContainsMarker(pathText, LocalRuntimePathMarker) ||
                ContainsMarker(pathText, LocalBuildPathMarker) ||
                pathText.IndexOf("StreamingAssets", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pathText.IndexOf("local", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BundleLocation.Local;
            }

            return BundleLocation.Unknown;
        }

        private static bool ContainsMarker(string value, string marker)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
