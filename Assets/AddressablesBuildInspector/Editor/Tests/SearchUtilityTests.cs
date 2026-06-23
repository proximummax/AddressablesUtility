using AddressablesBuildInspector.Editor.Utilities;
using NUnit.Framework;

namespace AddressablesBuildInspector.Editor.Tests
{
    public sealed class SearchUtilityTests
    {
        [Test]
        public void Matches_ReturnsTrueForEmptyQuery()
        {
            Assert.IsTrue(SearchUtility.Matches(string.Empty, "Assets/Characters/Hero.prefab"));
        }

        [Test]
        public void Matches_IsCaseInsensitive()
        {
            Assert.IsTrue(SearchUtility.Matches("hero", "Assets/Characters/HERO.prefab"));
        }

        [Test]
        public void Matches_SearchesAllProvidedFields()
        {
            Assert.IsTrue(SearchUtility.Matches("forest", "environment.bundle", "Assets/Scenes/Forest.unity"));
        }

        [Test]
        public void Matches_IgnoresNullFields()
        {
            Assert.IsFalse(SearchUtility.Matches("missing", null, "Assets/Scenes/Forest.unity"));
        }
    }
}
