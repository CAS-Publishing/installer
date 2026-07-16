using System.Collections.Generic;

namespace PSV.Installer.Wizard
{
    internal sealed class PlatformReadiness
    {
        public string Platform;
        public bool Used;   // платформа задіяна (є вимоги і компонент встановлений)
        public bool AllOk;  // всі активні вимоги виконані
    }

    internal static class ConfigureGate
    {
        // Правило «one platform is enough» (мокап Configuration complete №8).
        public static bool CanContinue(IReadOnlyList<PlatformReadiness> platforms)
        {
            foreach (var p in platforms)
                if (p != null && p.Used && p.AllOk) return true;
            return false;
        }
    }
}
