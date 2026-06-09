using System.Collections.Generic;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Placeholder data for the wizard preview. Iteration 1 only — iteration 2 replaces these
    /// with live Scanner/Catalog data. Values mirror docs/design/screens.jsx so the preview
    /// matches the mockup exactly.
    /// </summary>
    internal static class StubData
    {
        public sealed class ProgressStep
        {
            public string Label;
            public string State;   // "done" | "active" | "wait"
            public string Right;
        }

        // Mirrors docs/design/screens.jsx lines 500–510.
        public static readonly List<ProgressStep> ProgressSteps = new List<ProgressStep>
        {
            new ProgressStep { Label = "Checking system requirements",         State = "done" },
            new ProgressStep { Label = "Resolving dependencies",               State = "done" },
            new ProgressStep { Label = "Installing CAS SDK",                    State = "done" },
            new ProgressStep { Label = "Installing Tenjin SDK",                 State = "done" },
            new ProgressStep { Label = "Installing Unity Ads",                  State = "active", Right = "Installing…" },
            new ProgressStep { Label = "Installing Google Mobile Ads",          State = "wait" },
            new ProgressStep { Label = "Installing Facebook Audience Network",  State = "wait" },
            new ProgressStep { Label = "Configuring settings",                  State = "wait" },
            new ProgressStep { Label = "Initializing components",               State = "wait" },
        };

        public const string InstallerVersion = "1.2.3";
        public const string LatestVersion    = "1.2.5";
        public const int    ProgressPercent  = 56;
    }
}
