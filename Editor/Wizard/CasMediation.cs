using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Activates a CAS mediation solution (OptimalAds / FamiliesAds) via reflection into CAS's editor
    /// <c>DependencyManager</c>. Best-effort: any missing CAS type/member logs a warning and returns
    /// false, so a CAS API change never throws — the installer's asset-field writes still apply.
    /// The reflected members (Create / solutions / Dependency.id / ActivateDependencies) must be
    /// confirmed in Unity by the owner; this code never assumes they exist.
    /// </summary>
    internal static class CasMediation
    {
        private const string OptimalId = "OptimalAds";
        private const string FamiliesId = "FamiliesAds";

        /// <summary>
        /// Activates or disables ONE CAS mediation solution (OptimalAds / FamiliesAds) independently,
        /// mirroring CAS's own per-solution checkboxes. Best-effort: a missing CAS type/member logs a
        /// warning and returns false, never throws. Does not touch the other solution.
        /// </summary>
        public static bool SetSolution(BuildTarget platform, bool families, bool enable)
        {
            try
            {
                var dmType = FindType("DependencyManager");
                if (dmType == null) return Warn("DependencyManager type not found");

                var audienceType = FindType("Audience");
                if (audienceType == null) return Warn("Audience type not found");
                var audienceVal = Enum.ToObject(audienceType, CasAudience.ForFamilies(families));

                var create = dmType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(BuildTarget), audienceType, typeof(bool) }, null);
                if (create == null) return Warn("DependencyManager.Create(BuildTarget,Audience,bool) not found");

                var manager = create.Invoke(null, new object[] { platform, audienceVal, false });
                if (manager == null) return Warn("DependencyManager.Create returned null");

                var solutionsMember = dmType.GetField("solutions") != null
                    ? (MemberInfo)dmType.GetField("solutions")
                    : dmType.GetProperty("solutions");
                var solutions = GetValue(solutionsMember, manager) as Array;
                if (solutions == null) return Warn("solutions not found");

                var wantName = families ? FamiliesId : OptimalId;
                object target = null;
                foreach (var sol in solutions)
                {
                    if (sol == null) continue;
                    var nameMember = (MemberInfo)sol.GetType().GetField("name") ?? sol.GetType().GetProperty("name");
                    if (GetValue(nameMember, sol) as string == wantName) { target = sol; break; }
                }
                if (target == null) return Warn($"solution '{wantName}' not present");

                var methodName = enable ? "ActivateDependencies" : "DisableDependencies";
                var method = target.GetType().GetMethod(methodName);
                if (method == null) return Warn("Dependency." + methodName + " not found");
                method.Invoke(target, new[] { (object)platform, manager });

                AssetDatabase.Refresh();
                RefreshInspector();
                return true;
            }
            catch (Exception e)
            {
                return Warn("reflection error: " + e.Message);
            }
        }

        /// <summary>
        /// Best-effort read: true when the given solution (Families or Optimal) is installed
        /// (IsInstalled() == installedVersion present). Returns false on any reflection failure.
        /// </summary>
        public static bool IsSolutionInstalled(BuildTarget platform, bool families)
        {
            try
            {
                var dmType = FindType("DependencyManager");
                var audienceType = FindType("Audience");
                if (dmType == null || audienceType == null) return false;

                var create = dmType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(BuildTarget), audienceType, typeof(bool) }, null);
                if (create == null) return false;

                var manager = create.Invoke(null, new object[] { platform, Enum.ToObject(audienceType, 0), false });
                if (manager == null) return false;

                var solutionsMember = dmType.GetField("solutions") != null
                    ? (MemberInfo)dmType.GetField("solutions")
                    : dmType.GetProperty("solutions");
                if (!(GetValue(solutionsMember, manager) is Array solutions)) return false;

                var wantName = families ? FamiliesId : OptimalId;
                foreach (var sol in solutions)
                {
                    if (sol == null) continue;
                    var nameMember = (MemberInfo)sol.GetType().GetField("name") ?? sol.GetType().GetProperty("name");
                    if (GetValue(nameMember, sol) as string != wantName) continue;
                    var isInstalled = sol.GetType().GetMethod("IsInstalled");
                    return isInstalled != null && isInstalled.Invoke(sol, null) is bool b && b;
                }
                return false;
            }
            catch { return false; }
        }

        private static Type FindType(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                    if (t.Name == simpleName && t.Namespace != null && t.Namespace.StartsWith("CAS"))
                        return t;
            }
            return null;
        }

        private static object GetValue(MemberInfo m, object target) =>
            m is FieldInfo f ? f.GetValue(target) : (m as PropertyInfo)?.GetValue(target);

        private static void RefreshInspector()
        {
            try { ActiveEditorTracker.sharedTracker.ForceRebuild(); }
            catch { /* refresh is cosmetic — the XML on disk is already correct */ }
        }

        private static bool Warn(string why)
        {
            Debug.LogWarning("[PSV Installer] CAS network-set not applied (" + why +
                "). Ad formats/audience were still written; pick the network set in CAS settings if needed.");
            return false;
        }
    }
}
