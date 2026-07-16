namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Maps a screen id to its 1-based position in the 3-step intro flow (Install → Configure →
    /// Done) for the stepper header. Null means "not a flow screen" — the caller (InstallerWizardWindow.
    /// UpdateTabBar) falls back to the tab bar for those ids. Pure/testable.
    /// </summary>
    internal static class WizardStepper
    {
        internal static int? StepFor(string screenId)
        {
            switch (screenId)
            {
                case "ready":
                case "progress":
                    return 1;
                case "configure":
                    return 2;
                case "done":
                    return 3;
                default:
                    return null;
            }
        }
    }
}
