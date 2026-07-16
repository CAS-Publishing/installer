using System.Collections.Generic;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// One row on the "Ready to install" screen — a default component's display copy plus the
    /// right-column status text (target version, or an "already installed" confirmation).
    /// </summary>
    internal sealed class ReadyRow
    {
        public string Name;
        public string Sub;
        public string RightText;      // "4.x.x" (target) or "✓ Already installed (v)"
        public bool   AlreadyInstalled;
    }

    /// <summary>
    /// Pure view-model for the "Ready to install" screen (step 1 of the install flow):
    /// derives per-row display state from the live <see cref="ComponentStatus"/> scan, and
    /// decides whether the primary button reads "Install" or "Continue".
    /// </summary>
    internal sealed class ReadyModel
    {
        public readonly List<ReadyRow> Rows = new List<ReadyRow>();
        public bool AllInstalled = true;
        public string PrimaryButtonText => AllInstalled ? "Continue" : "Install";

        public static ReadyModel Build(List<ComponentStatus> statuses)
        {
            var m = new ReadyModel();
            foreach (var s in statuses)
            {
                var installed = s.Installed;
                m.Rows.Add(new ReadyRow
                {
                    Name = s.DisplayName,
                    Sub = s.Sub,
                    AlreadyInstalled = installed,
                    RightText = installed
                        ? $"✓ Already installed ({s.Version ?? "?"})"
                        : (s.Version ?? "latest"),
                });
                if (!installed) m.AllInstalled = false;
            }
            return m;
        }
    }
}
