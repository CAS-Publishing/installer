using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasIdValidationTests
    {
        [Test] public void Android_bundle_is_valid()
            => Assert.IsTrue(CasIdValidation.IsValid("com.company.game", CasIdValidation.AndroidRegex));

        [Test] public void Android_trims_whitespace()
            => Assert.IsTrue(CasIdValidation.IsValid("  com.company.game  ", CasIdValidation.AndroidRegex));

        [Test] public void Android_single_segment_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("com", CasIdValidation.AndroidRegex));

        [Test] public void Android_demo_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("demo", CasIdValidation.AndroidRegex));

        [Test] public void Android_numeric_segment_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("1.2", CasIdValidation.AndroidRegex));

        [Test] public void iOS_numeric_is_valid()
            => Assert.IsTrue(CasIdValidation.IsValid("1234567890", CasIdValidation.IosRegex));

        [Test] public void iOS_demo_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("demo", CasIdValidation.IosRegex));

        [Test] public void iOS_alphanumeric_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("12a", CasIdValidation.IosRegex));

        [Test] public void Empty_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("", CasIdValidation.AndroidRegex));

        [Test] public void Null_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid(null, CasIdValidation.IosRegex));

        [Test] public void Resolve_uses_catalog_when_present()
        {
            var (regex, hint) = CasIdValidation.Resolve("Android", "^x$", "myhint");
            Assert.AreEqual("^x$", regex);
            Assert.AreEqual("myhint", hint);
        }

        [Test] public void Resolve_falls_back_to_android_defaults()
        {
            var (regex, hint) = CasIdValidation.Resolve("Android", null, "");
            Assert.AreEqual(CasIdValidation.AndroidRegex, regex);
            Assert.AreEqual(CasIdValidation.AndroidHint, hint);
        }

        [Test] public void Resolve_falls_back_to_ios_defaults()
        {
            var (regex, hint) = CasIdValidation.Resolve("iOS", null, null);
            Assert.AreEqual(CasIdValidation.IosRegex, regex);
            Assert.AreEqual(CasIdValidation.IosHint, hint);
        }
    }
}
