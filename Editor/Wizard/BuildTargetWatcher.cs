using UnityEditor;
using UnityEditor.Build;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// When the active build target switches to Android or iOS and CAS is installed but that
    /// platform's CAS id isn't configured yet, auto-opens the wizard at Welcome (preselecting the
    /// new platform) so the user can configure it. Other targets are ignored. Implements Unity's
    /// build-target-changed callback (invoked by the Editor on a successful target switch).
    /// </summary>
    internal sealed class BuildTargetWatcher : IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;

        // Coalesce rapid multi-switches (Android→iOS→Android within one tick) into a single deferred
        // check for the LATEST platform: store the pending platform and enqueue delayCall only once,
        // instead of += a fresh closure per switch (which would run the scan + open N times in a tick).
        private static string _pendingPlatform;
        private static bool _queued;

        public void OnActiveBuildTargetChanged(BuildTarget previous, BuildTarget newTarget)
        {
            if (newTarget != BuildTarget.Android && newTarget != BuildTarget.iOS) return;

            _pendingPlatform = PlatformDetect.FromBuildTarget(newTarget); // "Android" | "iOS"
            if (_queued) return;
            _queued = true;

            // Defer the scan + window open to the next editor tick. Doing them synchronously inside the
            // build-target-change callback runs while the editor is mid-switch and triggers a flood of
            // off-main-thread UI Toolkit text errors (UITKTextJobSystem/GetFontAsset). delayCall fires
            // once, on the main thread, after the switch settles.
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            _queued = false;
            var platform = _pendingPlatform;
            _pendingPlatform = null;
            if (string.IsNullOrEmpty(platform)) return;

            if (!BuildSwitchPolicy.ShouldOpenOnSwitch(
                    CasPresence.IsInstalled(),
                    CasIdApplier.ReadExisting(platform)))
                return;

            InstallerWizardWindow.OpenAtWelcome(platform);
        }
    }
}
