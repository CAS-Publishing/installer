using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    /// <summary>
    /// The SHIPPED catalog's Tenjin <c>legacyAssetFiles</c> must cover the SDK's RUNTIME scripts
    /// (BaseTenjin.cs, Tenjin.cs, AndroidTenjin.cs, …), not just its editor/build helpers — otherwise a
    /// manual Tenjin install moved to a non-standard path (e.g. Assets/Scripts/Core/Tenjin) is never
    /// removed on migrate, because the signature scan finds nothing to delete. Loads the real catalog so
    /// this fails the moment the data regresses.
    /// </summary>
    public class TenjinCatalogSignatureTests
    {
        private static List<LegacyAssetFile> TenjinSignatures()
        {
            var load = CatalogLoader.Load();
            Assert.AreEqual(CatalogLoadStatus.Ok, load.Status, "metadata catalog must be loadable in the dev project");
            ExternalRecord tenjin = null;
            foreach (var e in load.Catalog.External)
                if (e != null && e.Id == "com.tenjin.sdk") { tenjin = e; break; }
            Assert.IsNotNull(tenjin, "catalog must declare the com.tenjin.sdk external");
            Assert.IsNotNull(tenjin.LegacyAssetFiles, "Tenjin external must declare legacyAssetFiles");
            return tenjin.LegacyAssetFiles;
        }

        private static bool AnySignatureMatches(string fileName, string content, IReadOnlyList<LegacyAssetFile> sigs)
        {
            foreach (var s in sigs)
                if (AssetInstallProbe.MatchesSignature(fileName, content, s)) return true;
            return false;
        }

        [Test]
        public void Signatures_cover_core_runtime_scripts()
        {
            var sigs = TenjinSignatures();

            // Representative content taken from the real Tenjin Unity SDK files.
            Assert.IsTrue(AnySignatureMatches("BaseTenjin.cs",
                "// Copyright (c) 2022 Tenjin.\npublic abstract class BaseTenjin : MonoBehaviour { public string SdkVersion { get; } = \"1.16.0\"; }", sigs),
                "Tenjin signatures must cover BaseTenjin.cs");

            Assert.IsTrue(AnySignatureMatches("Tenjin.cs",
                "namespace TenjinSDK { public static class Tenjin { } }", sigs),
                "Tenjin signatures must cover Tenjin.cs");

            Assert.IsTrue(AnySignatureMatches("AndroidTenjin.cs",
                "public class AndroidTenjin : BaseTenjin { private const string c = \"com.tenjin.android.TenjinSDK\"; }", sigs),
                "Tenjin signatures must cover AndroidTenjin.cs");

            Assert.IsTrue(AnySignatureMatches("IosTenjin.cs",
                "public class IosTenjin : BaseTenjin { static extern void iosTenjinInit(string apiKey); }", sigs),
                "Tenjin signatures must cover IosTenjin.cs");
        }

        [Test]
        public void Signatures_reject_unrelated_user_file_of_same_name()
        {
            var sigs = TenjinSignatures();
            // A user's own Tenjin.cs that is NOT the SDK (no TenjinSDK namespace, no SDK class).
            const string userFile = "// my notes about a tenjin pricing helper\npublic class TenjinPricing { }";
            Assert.IsFalse(AnySignatureMatches("Tenjin.cs", userFile, sigs),
                "a user's unrelated same-named file must never match");
        }
    }
}
