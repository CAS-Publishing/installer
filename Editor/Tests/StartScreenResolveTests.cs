using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class StartScreenResolveTests
    {
        [TestCase(null, false, ExpectedResult = "ready")]
        [TestCase(null, true,  ExpectedResult = "components")]
        [TestCase("configure", true, ExpectedResult = "configure")]
        [TestCase("welcome", true, ExpectedResult = "components")] // stale id from a pre-Task-5 session
        [TestCase("progress", false, ExpectedResult = "progress")]
        public string Resolve(string saved, bool introDone) =>
            InstallerWizardWindow.ResolveStartScreenPure(saved, introDone);

        [TestCase("ready", ExpectedResult = 1)]
        [TestCase("progress", ExpectedResult = 1)]
        [TestCase("configure", ExpectedResult = 2)]
        [TestCase("done", ExpectedResult = 3)]
        [TestCase("components", ExpectedResult = null)]
        public int? Step(string id) => WizardStepper.StepFor(id);
    }
}
