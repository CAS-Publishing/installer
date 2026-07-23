using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class ComponentsViewMapTests
    {
        private static ComponentStatus S(string status, bool installed, bool outsideUpm = false, bool git = false) =>
            new ComponentStatus { StatusText = status, Installed = installed, OutsideUpm = outsideUpm, GitInstalled = git };

        [Test] public void Installed_MapsToUpToDate()
        {
            var vm = ComponentsViewMap.Map(S("Installed", true), "1.12.0");
            Assert.AreEqual("Up to date", vm.StatusText);
            Assert.AreEqual(RowAction.None, vm.Action);
            Assert.IsTrue(vm.RemoveEnabled);
        }

        [Test] public void UpdateAvailable_MapsToUpdateRequired_WithHint()
        {
            var vm = ComponentsViewMap.Map(S("Update available", true), "1.12.0");
            Assert.AreEqual("Update required", vm.StatusText);
            Assert.AreEqual(RowAction.Update, vm.Action);
            Assert.AreEqual("to v1.12.0", vm.ActionHint);
        }

        [Test] public void TooOld_MapsToUpdateRequired_WithHint()
        {
            var vm = ComponentsViewMap.Map(S("Too old", true), "2.0.0");
            Assert.AreEqual("Update required", vm.StatusText);
            Assert.AreEqual(RowAction.Update, vm.Action);
            Assert.AreEqual("to v2.0.0", vm.ActionHint);
        }

        [Test] public void UpdateAvailable_NullRecommended_HintIsNull()
        {
            var vm = ComponentsViewMap.Map(S("Update available", true), null);
            Assert.IsNull(vm.ActionHint);
        }

        [Test] public void ManualInstall_MapsToConnectToHub()
        {
            var vm = ComponentsViewMap.Map(S("Installed (manual)", true, outsideUpm: true), null);
            Assert.AreEqual("Manual install", vm.StatusText);
            Assert.AreEqual(RowAction.ConnectToHub, vm.Action);
            Assert.AreEqual("Connect to Hub", vm.ActionText);
        }

        // Deviation lock-in: a manual (out-of-UPM) copy has no manifest.json entry to remove — Remove
        // must stay disabled even though the SDK is "Installed", or clicking it silently does nothing
        // (the RemovePackage-not-in-manifest no-op, ManifestWriter.ApplyRemovePackage) while looking
        // like a working button.
        [Test] public void ManualInstall_RemoveDisabled()
        {
            var vm = ComponentsViewMap.Map(S("Installed (manual)", true, outsideUpm: true), null);
            Assert.IsFalse(vm.RemoveEnabled);
        }

        // Fix 1 (2026-07-23 firebase-migration-and-registry-fix): "Needs migration" is its own case
        // now (split out from "Installed (manual)"/"Installed (git)") — its producers always set
        // ComponentStatus.ActionText = "Migrate" (PackageState.LegacyUpm/LegacyAssets and the
        // promoted split-group case), so the button label is "Migrate", not the generic
        // "Connect to Hub". Dispatch (RowAction.ConnectToHub) is unchanged.
        [Test] public void NeedsMigration_MapsToConnectToHub_WithMigrateLabel()
        {
            var status = new ComponentStatus { StatusText = "Needs migration", Installed = true, ActionText = "Migrate" };
            var vm = ComponentsViewMap.Map(status, null);
            Assert.AreEqual("Manual install", vm.StatusText);
            Assert.AreEqual(RowAction.ConnectToHub, vm.Action);
            Assert.AreEqual("Migrate", vm.ActionText);
        }

        [Test] public void NeedsMigration_MissingActionText_FallsBackToMigrateLabel()
        {
            var vm = ComponentsViewMap.Map(S("Needs migration", true), null);
            Assert.AreEqual("Migrate", vm.ActionText);
        }

        [Test] public void InstalledGit_MapsToConnectToHub()
        {
            var vm = ComponentsViewMap.Map(S("Installed (git)", true, git: true), null);
            Assert.AreEqual("Manual install", vm.StatusText);
            Assert.AreEqual(RowAction.ConnectToHub, vm.Action);
            Assert.AreEqual("Connect to Hub", vm.ActionText);
            Assert.IsTrue(vm.RemoveEnabled); // a git dependency IS a real manifest entry
        }

        // Deviation (documented in the task report): a naive "manual → Connect to Hub" mapping would
        // let the user "connect" a legacy-wrapper install, installing the canonical id ALONGSIDE the
        // legacy one — a duplicate SDK. Legacy gets no action; the detected legacy id (carried in
        // ComponentStatus.ActionText by ComponentStatusProvider.FromExternal) surfaces as a hint
        // instead, and Remove is forced back on because the legacy id IS a real manifest entry.
        [Test] public void InstalledLegacy_MapsToManualInstall_NoAction_HintIsLegacyId()
        {
            var status = new ComponentStatus
            {
                StatusText = "Installed (legacy)",
                Installed = true,
                ActionText = "com.psv.tenjin", // FromExternal stashes the detected legacy id here
            };

            var vm = ComponentsViewMap.Map(status, null);

            Assert.AreEqual("Manual install", vm.StatusText);
            Assert.AreEqual(RowAction.None, vm.Action);
            Assert.AreEqual("com.psv.tenjin", vm.ActionHint);
            Assert.IsTrue(vm.RemoveEnabled);
        }

        [Test] public void MixedInstall_MapsToFix()
        {
            var vm = ComponentsViewMap.Map(S("Mixed install", true), null);
            Assert.AreEqual("Mixed install", vm.StatusText);
            Assert.AreEqual(RowAction.Fix, vm.Action);
            Assert.AreEqual("Fix", vm.ActionText);
        }

        [Test] public void NeedsRegistry_MapsToFix()
        {
            var vm = ComponentsViewMap.Map(S("Needs registry", true), null);
            Assert.AreEqual("Needs registry", vm.StatusText);
            Assert.AreEqual(RowAction.Fix, vm.Action);
            Assert.AreEqual("Fix", vm.ActionText);
        }

        [Test] public void NotInstalled_MapsToInstall()
        {
            var vm = ComponentsViewMap.Map(S("Not Installed", false), null);
            Assert.AreEqual(RowAction.Install, vm.Action);
            Assert.IsFalse(vm.RemoveEnabled);
        }

        [Test] public void NotInCatalog_MapsToNoAction_RemoveDisabled()
        {
            var vm = ComponentsViewMap.Map(S("Not in catalog", false), null);
            Assert.AreEqual("Not in catalog", vm.StatusText);
            Assert.AreEqual(RowAction.None, vm.Action);
            Assert.IsFalse(vm.RemoveEnabled);
        }
    }
}
