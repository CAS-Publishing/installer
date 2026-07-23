using NUnit.Framework;
using Newtonsoft.Json;
using PSV.Installer.Catalog;

namespace PSV.Installer.Tests
{
    public class CatalogPackageRecordFieldsTests
    {
        [Test]
        public void PackageRecord_Deserializes_Scopes_Requires_DetectMarkers()
        {
            const string json = @"{
                ""id"": ""com.psvgamestudio.remoteconfig"",
                ""registry"": ""psv"",
                ""scopes"": [""com.psvgamestudio""],
                ""requires"": [""com.psv.core""],
                ""detectMarkers"": [""Firebase.RemoteConfig""]
            }";
            var rec = JsonConvert.DeserializeObject<PackageRecord>(json);
            Assert.AreEqual("com.psvgamestudio", rec.Scopes[0]);
            Assert.AreEqual("com.psv.core", rec.Requires[0]);
            Assert.AreEqual("Firebase.RemoteConfig", rec.DetectMarkers[0]);
        }

        [Test]
        public void PackageRecord_FieldsAbsent_AreNull()
        {
            var rec = JsonConvert.DeserializeObject<PackageRecord>(@"{ ""id"": ""x"" }");
            Assert.IsNull(rec.Scopes);
            Assert.IsNull(rec.Requires);
            Assert.IsNull(rec.DetectMarkers);
        }
    }
}
