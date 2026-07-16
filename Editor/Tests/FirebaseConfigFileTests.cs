using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class FirebaseConfigFileTests
    {
        [Test] public void WrongFileName_Rejected() =>
            Assert.IsNotNull(FirebaseConfigFile.Validate("C:/tmp/random.json"));

        [Test] public void GoogleServicesJson_Accepted() =>
            Assert.IsNull(FirebaseConfigFile.Validate("C:/tmp/google-services.json"));

        [Test] public void PlistAccepted() =>
            Assert.IsNull(FirebaseConfigFile.Validate("D:/x/GoogleService-Info.plist"));

        // ValidateAndCopy must never throw, even when the source path contains characters Path.GetFileName
        // may reject (its Validate call runs INSIDE the try, not before it). Whether or not this particular
        // runtime actually throws for an embedded control character, the call must come back with an error
        // string rather than propagating an exception.
        [Test] public void InvalidPathChars_ReturnsErrorString_DoesNotThrow()
        {
            var badPath = "C:/tmp/google-services.json" + '\0' + "x";
            string result = null;
            Assert.DoesNotThrow(() => result = FirebaseConfigFile.ValidateAndCopy(badPath, "Assets"));
            Assert.IsNotNull(result);
        }
    }
}
