using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    /// <summary>
    /// Scattered legacy SDK files (e.g. Tenjin's BuildPostProcessor.cs dropped into Assets/Editor by
    /// the .unitypackage) must be matched by file NAME + a CONTENT signature — never name alone, so a
    /// user's unrelated file of the same name is left untouched.
    /// </summary>
    public class LegacyFileSignatureTests
    {
        private static LegacyAssetFile TenjinBuildPostProcessor() => new LegacyAssetFile
        {
            Name = "BuildPostProcessor.cs",
            Contains = new System.Collections.Generic.List<string> { "class BuildPostProcessor", "TenjinSDK" },
        };

        [Test]
        public void Matches_when_name_and_all_content_markers_present()
        {
            var sig = TenjinBuildPostProcessor();
            const string content = "public class BuildPostProcessor : MonoBehaviour { /* TenjinSDK */ }";

            Assert.IsTrue(AssetInstallProbe.MatchesSignature("BuildPostProcessor.cs", content, sig));
        }

        [Test]
        public void DoesNotMatch_user_file_same_name_without_signature()
        {
            var sig = TenjinBuildPostProcessor();
            const string content = "public class BuildPostProcessor { /* my own build hook, nothing to do with Tenjin */ }";

            Assert.IsFalse(AssetInstallProbe.MatchesSignature("BuildPostProcessor.cs", content, sig));
        }

        [Test]
        public void DoesNotMatch_when_name_differs()
        {
            var sig = TenjinBuildPostProcessor();
            const string content = "public class BuildPostProcessor : MonoBehaviour { /* TenjinSDK */ }";

            Assert.IsFalse(AssetInstallProbe.MatchesSignature("MyPostProcessor.cs", content, sig));
        }

        [Test]
        public void DoesNotMatch_when_only_some_content_markers_present()
        {
            var sig = TenjinBuildPostProcessor();
            const string content = "public class BuildPostProcessor : MonoBehaviour { }"; // no "TenjinSDK"

            Assert.IsFalse(AssetInstallProbe.MatchesSignature("BuildPostProcessor.cs", content, sig));
        }
    }
}
