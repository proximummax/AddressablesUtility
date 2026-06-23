using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Parsing;

namespace AddressablesBuildInspector.Editor.Services
{
    /// <summary>
    /// Fills unknown bundle locations using the current Addressables project settings when available.
    /// </summary>
    public sealed class BundleLocationEnrichmentService
    {
        private const string AddressablesEditorAssemblyName = "Unity.Addressables.Editor";
        private const string SettingsDefaultObjectTypeName = "UnityEditor.AddressableAssets.Settings.AddressableAssetSettingsDefaultObject";
        private const string BundledAssetGroupSchemaTypeName = "UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema";

        /// <summary>
        /// Updates bundles that still have unknown locations.
        /// </summary>
        public void Enrich(BuildReportData report)
        {
            if (report == null)
            {
                return;
            }

            object settings = TryGetAddressableAssetSettings();
            if (settings == null)
            {
                return;
            }

            Dictionary<string, BundleLocation> locationsByGroupName = BuildGroupLocationMap(settings);
            Dictionary<string, BundleLocation> locationsByAssetPath = BuildAssetLocationMap(settings, locationsByGroupName);

            foreach (BundleData bundle in report.Bundles)
            {
                if (bundle.Location != BundleLocation.Unknown)
                {
                    continue;
                }

                if (TryResolveFromBundleAssets(bundle, locationsByAssetPath, out BundleLocation assetLocation))
                {
                    bundle.Location = assetLocation;
                    continue;
                }

                if (TryResolveFromBundleName(bundle.Name, locationsByGroupName, out BundleLocation groupLocation))
                {
                    bundle.Location = groupLocation;
                }
            }
        }

        private static object TryGetAddressableAssetSettings()
        {
            Type settingsDefaultObjectType = Type.GetType($"{SettingsDefaultObjectTypeName}, {AddressablesEditorAssemblyName}");
            if (settingsDefaultObjectType == null)
            {
                return null;
            }

            PropertyInfo settingsProperty = settingsDefaultObjectType.GetProperty(
                "Settings",
                BindingFlags.Public | BindingFlags.Static);
            return settingsProperty?.GetValue(null);
        }

        private static Dictionary<string, BundleLocation> BuildGroupLocationMap(object settings)
        {
            var locationsByGroupName = new Dictionary<string, BundleLocation>(StringComparer.OrdinalIgnoreCase);
            Type schemaType = Type.GetType($"{BundledAssetGroupSchemaTypeName}, {AddressablesEditorAssemblyName}");
            if (schemaType == null)
            {
                return locationsByGroupName;
            }

            MethodInfo getSchemaMethod = FindGetSchemaMethod(settings);
            if (getSchemaMethod == null)
            {
                return locationsByGroupName;
            }

            MethodInfo genericGetSchemaMethod = getSchemaMethod.MakeGenericMethod(schemaType);
            foreach (object group in ReflectionObjectReader.ReadEnumerable(settings, "groups"))
            {
                string groupName = ReflectionObjectReader.ReadString(group, "Name");
                if (groupName.Length == 0)
                {
                    continue;
                }

                object schema = genericGetSchemaMethod.Invoke(group, null);
                if (schema == null)
                {
                    continue;
                }

                string buildPath = ResolveProfilePath(settings, schema, "BuildPath");
                string loadPath = ResolveProfilePath(settings, schema, "LoadPath");
                BundleLocation location = BundleLocationResolver.ResolveFromBuildAndLoadPaths(buildPath, loadPath);
                if (location != BundleLocation.Unknown)
                {
                    locationsByGroupName[groupName] = location;
                }
            }

            return locationsByGroupName;
        }

        private static Dictionary<string, BundleLocation> BuildAssetLocationMap(
            object settings,
            IReadOnlyDictionary<string, BundleLocation> locationsByGroupName)
        {
            var locationsByAssetPath = new Dictionary<string, BundleLocation>(StringComparer.OrdinalIgnoreCase);
            foreach (object group in ReflectionObjectReader.ReadEnumerable(settings, "groups"))
            {
                string groupName = ReflectionObjectReader.ReadString(group, "Name");
                if (groupName.Length == 0 ||
                    !locationsByGroupName.TryGetValue(groupName, out BundleLocation location))
                {
                    continue;
                }

                foreach (object entry in ReflectionObjectReader.ReadEnumerable(group, "entries"))
                {
                    string assetPath = ReflectionObjectReader.ReadString(entry, "AssetPath");
                    if (assetPath.Length == 0)
                    {
                        continue;
                    }

                    locationsByAssetPath[assetPath.Replace('\\', '/')] = location;
                }
            }

            return locationsByAssetPath;
        }

        private static MethodInfo FindGetSchemaMethod(object settings)
        {
            foreach (object group in ReflectionObjectReader.ReadEnumerable(settings, "groups"))
            {
                MethodInfo getSchemaMethod = group.GetType().GetMethod(
                    "GetSchema",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getSchemaMethod != null && getSchemaMethod.IsGenericMethodDefinition)
                {
                    return getSchemaMethod;
                }
            }

            Type groupType = Type.GetType(
                "UnityEditor.AddressableAssets.Settings.AddressableAssetGroup, Unity.Addressables.Editor");
            return groupType?.GetMethod(
                "GetSchema",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static string ResolveProfilePath(object settings, object schema, string memberName)
        {
            object profileReference = ReflectionObjectReader.ReadMember(schema, memberName);
            if (profileReference == null)
            {
                return string.Empty;
            }

            MethodInfo getValueMethod = profileReference.GetType().GetMethod(
                "GetValue",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { settings.GetType() },
                null);
            return getValueMethod?.Invoke(profileReference, new[] { settings }) as string ?? string.Empty;
        }

        private static bool TryResolveFromBundleAssets(
            BundleData bundle,
            IReadOnlyDictionary<string, BundleLocation> locationsByAssetPath,
            out BundleLocation location)
        {
            location = BundleLocation.Unknown;
            if (bundle.Assets.Count == 0)
            {
                return false;
            }

            int remoteVotes = 0;
            int localVotes = 0;
            foreach (AssetData asset in bundle.Assets)
            {
                string normalizedPath = asset.Path.Replace('\\', '/');
                if (!locationsByAssetPath.TryGetValue(normalizedPath, out BundleLocation assetLocation))
                {
                    continue;
                }

                if (assetLocation == BundleLocation.Remote)
                {
                    remoteVotes++;
                }
                else if (assetLocation == BundleLocation.Local)
                {
                    localVotes++;
                }
            }

            if (remoteVotes == 0 && localVotes == 0)
            {
                return false;
            }

            location = remoteVotes >= localVotes ? BundleLocation.Remote : BundleLocation.Local;
            return true;
        }

        private static bool TryResolveFromBundleName(
            string bundleName,
            IReadOnlyDictionary<string, BundleLocation> locationsByGroupName,
            out BundleLocation location)
        {
            location = BundleLocation.Unknown;
            if (string.IsNullOrWhiteSpace(bundleName))
            {
                return false;
            }

            string normalizedBundleName = NormalizeBundleToken(bundleName);
            int bestMatchLength = 0;
            foreach (KeyValuePair<string, BundleLocation> pair in locationsByGroupName)
            {
                string normalizedGroupName = NormalizeBundleToken(pair.Key);
                if (normalizedGroupName.Length == 0)
                {
                    continue;
                }

                if (!normalizedBundleName.StartsWith(normalizedGroupName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (normalizedGroupName.Length <= bestMatchLength)
                {
                    continue;
                }

                bestMatchLength = normalizedGroupName.Length;
                location = pair.Value;
            }

            return location != BundleLocation.Unknown;
        }

        private static string NormalizeBundleToken(string value)
        {
            return (value ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .ToLowerInvariant()
                .Replace(' ', '_');
        }
    }
}
