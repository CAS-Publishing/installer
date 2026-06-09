using System.Collections.Generic;
using PSV.Installer.Catalog;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Builds the per-component, per-platform readiness rows for the Setup screen by combining
    /// the catalog's declarative <c>config</c> requirements with live evaluation via
    /// <see cref="SetupChecker"/>. Config is only evaluated for INSTALLED components — you can't
    /// configure a package that isn't installed. Read-only.
    /// </summary>
    internal static class SetupModel
    {
        internal sealed class Cell
        {
            public ConfigRequirement Req;
            public ReqResult Result;
            public string Label;
        }

        internal sealed class Row
        {
            public string Name;
            public string Logo;
            public bool Installed;
            public readonly List<Cell> Android = new List<Cell>();
            public readonly List<Cell> IOS = new List<Cell>();
        }

        public static bool TryBuild(out List<Row> rows, out string error)
        {
            rows = new List<Row>();
            error = null;

            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok)
            {
                error = load.Status == CatalogLoadStatus.NotInstalled
                    ? "Catalog metadata package is not installed yet — configuration status is unavailable."
                    : $"Catalog could not be read: {load.Error}";
                return false;
            }

            var cat = load.Catalog;
            var configById = new Dictionary<string, List<ConfigRequirement>>();
            if (cat.Packages != null)
                foreach (var p in cat.Packages)
                    if (p?.Id != null && p.Config != null) configById[p.Id] = p.Config;
            if (cat.External != null)
                foreach (var e in cat.External)
                    if (e?.Id != null && e.Config != null) configById[e.Id] = e.Config;

            // Install state per default component (so we only check config for installed ones).
            var installedById = new Dictionary<string, bool>();
            if (ComponentStatusProvider.TryGetStatuses(out var statuses, out _))
                foreach (var s in statuses) installedById[s.Id] = s.Installed;

            foreach (var id in ComponentStatusProvider.DefaultIds)
            {
                ComponentStatusProvider.TryGetDefaultDisplay(id, out var name, out var logo);
                var row = new Row { Name = name, Logo = logo };
                installedById.TryGetValue(id, out row.Installed);

                // Only INSTALLED components have their per-platform config evaluated. Each cell
                // carries its own requirement (and thus its own per-platform action target).
                if (row.Installed && configById.TryGetValue(id, out var reqs) && reqs != null)
                {
                    foreach (var req in reqs)
                    {
                        if (req == null) continue;
                        var cell = new Cell { Req = req, Result = SetupChecker.Evaluate(req), Label = req.Label };
                        if (req.Platform == "iOS") row.IOS.Add(cell);
                        else if (req.Platform == "Android") row.Android.Add(cell);
                        else { row.Android.Add(cell); row.IOS.Add(cell); }
                    }
                }

                rows.Add(row);
            }

            return true;
        }
    }
}
