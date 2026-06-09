using NUnit.Framework;
using PSV.Installer.Catalog;

namespace PSV.Installer.Tests
{
    public sealed class CatalogLoaderTests
    {
        [Test]
        public void Valid_schema1_parses_ok()
        {
            var json = "{ \"schemaVersion\": 1, \"catalogVersion\": \"1.0.0\", \"packages\": [] }";
            var status = CatalogLoader.ParseAndValidate(json, out var cat, out _);
            Assert.AreEqual(CatalogParseStatus.Ok, status);
            Assert.IsNotNull(cat);
            Assert.AreEqual("1.0.0", cat.CatalogVersion);
        }

        [Test]
        public void Missing_schema_defaults_to_zero_and_is_ok()
        {
            var status = CatalogLoader.ParseAndValidate("{ \"catalogVersion\": \"1\" }", out var cat, out _);
            Assert.AreEqual(CatalogParseStatus.Ok, status);
            Assert.IsNotNull(cat);
        }

        [Test]
        public void Malformed_json_is_malformed()
        {
            var status = CatalogLoader.ParseAndValidate("{ not json", out var cat, out var err);
            Assert.AreEqual(CatalogParseStatus.Malformed, status);
            Assert.IsNull(cat);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Empty_string_is_malformed()
        {
            Assert.AreEqual(CatalogParseStatus.Malformed, CatalogLoader.ParseAndValidate("   ", out _, out _));
        }

        [Test]
        public void Future_schema_is_unsupported()
        {
            var json = "{ \"schemaVersion\": 999, \"catalogVersion\": \"9.9.9\" }";
            var status = CatalogLoader.ParseAndValidate(json, out var cat, out var err);
            Assert.AreEqual(CatalogParseStatus.UnsupportedSchema, status);
            Assert.IsNull(cat);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Negative_schema_is_malformed()
        {
            var status = CatalogLoader.ParseAndValidate(
                "{ \"schemaVersion\": -1 }", out var cat, out var err);
            Assert.AreEqual(CatalogParseStatus.Malformed, status);
            Assert.IsNull(cat);
            Assert.IsNotEmpty(err);
        }
    }
}
