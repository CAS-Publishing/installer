using PSV.Installer;
using UnityEditor;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Registers the wizard as the installer UI that <see cref="Bootstrap"/> opens, replacing
    /// the legacy IMGUI window for the auto-popup. The legacy window remains available via its
    /// own menu item. Runs at editor init (InitializeOnLoad), before Bootstrap's delayCall fires,
    /// so the hook is set in time.
    /// </summary>
    [InitializeOnLoad]
    internal static class WizardBootstrap
    {
        static WizardBootstrap()
        {
            Bootstrap.ShowInstaller = InstallerWizardWindow.ShowIfReportChanged;
        }
    }
}
