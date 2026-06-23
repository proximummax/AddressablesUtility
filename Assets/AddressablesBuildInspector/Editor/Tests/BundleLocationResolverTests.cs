using AddressablesBuildInspector.Editor.Models;
using AddressablesBuildInspector.Editor.Parsing;
using NUnit.Framework;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class BundleLocationResolverTests
    {
        [Test]
        public void ResolveFromLoadPath_ReturnsRemoteForHttpUrls()
        {
            Assert.AreEqual(BundleLocation.Remote, BundleLocationResolver.ResolveFromLoadPath("https://cdn.example.com/Android/group.bundle"));
        }

        [Test]
        public void ResolveFromLoadPath_ReturnsLocalForRuntimePathMarker()
        {
            Assert.AreEqual(
                BundleLocation.Local,
                BundleLocationResolver.ResolveFromLoadPath("{UnityEngine.AddressableAssets.Addressables.RuntimePath}/Android/group.bundle"));
        }

        [Test]
        public void ResolveFromBuildAndLoadPaths_ReturnsRemoteForRemoteProfilePaths()
        {
            Assert.AreEqual(
                BundleLocation.Remote,
                BundleLocationResolver.ResolveFromBuildAndLoadPaths(
                    "ServerData/Android",
                    "https://cdn.example.com/Android/group.bundle"));
        }

        [Test]
        public void ResolveFromBuildAndLoadPaths_ReturnsLocalForDefaultLocalProfilePaths()
        {
            Assert.AreEqual(
                BundleLocation.Local,
                BundleLocationResolver.ResolveFromBuildAndLoadPaths(
                    "[UnityEngine.AddressableAssets.Addressables.BuildPath]/Android",
                    "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/Android/group.bundle"));
        }
    }
}
