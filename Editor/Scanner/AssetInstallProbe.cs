using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PSV.Installer.Scanner
{
    /// <summary>
    /// Detects an SDK installed OUTSIDE UPM (e.g. via a .unitypackage or manual drop).
    ///
    /// Presence is decided by REFLECTION over loaded types (<see cref="CollectLoadedIdentifiers"/>
    /// + <see cref="IsPresentInIdentifiers"/>): this catches every install shape — asmdef, precompiled
    /// DLL, AND raw .cs scripts with no asmdef (they compile into Assembly-CSharp). The matched
    /// identifier is a type's namespace, or — for types in the GLOBAL namespace (e.g. Tenjin's
    /// <c>Tenjin</c>/<c>BaseTenjin</c> classes) — its simple name, so namespace-less SDKs are still
    /// seen. UPM packages also load types, but the classifier only consults this when the package is
    /// ABSENT from the manifest, so a positive means a non-UPM copy in the project.
    ///
    /// Read-only; never throws.
    /// </summary>
    internal static class AssetInstallProbe
    {
        // ── Presence (reflection, per scan) ──────────────────────────────────

        /// <summary>
        /// The marker-matching identifier for a loaded type: its namespace when it has one, otherwise
        /// its simple name. SDKs whose public types live in the GLOBAL namespace (e.g. Tenjin's
        /// <c>Tenjin</c> / <c>BaseTenjin</c> classes) have no namespace to match against — using the
        /// simple name there lets a "Tenjin" marker still detect them. Namespaced types keep using the
        /// namespace, so detection of CAS / Firebase (already namespaced) is unchanged.
        /// </summary>
        internal static string TypeIdentifier(string ns, string simpleName)
            => string.IsNullOrEmpty(ns) ? simpleName : ns;

        /// <summary>
        /// Collects marker-matching identifiers for all loaded types — namespaces for namespaced types,
        /// simple names for global-namespace types (see <see cref="TypeIdentifier"/>). Tolerant of
        /// partially-loaded assemblies.
        /// </summary>
        public static HashSet<string> CollectLoadedIdentifiers()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    var id = TypeIdentifier(t.Namespace, t.Name);
                    if (!string.IsNullOrEmpty(id)) set.Add(id);
                }
            }
            return set;
        }

        /// <summary>True when any loaded identifier matches a marker (case-insensitive substring).</summary>
        public static bool IsPresentInIdentifiers(IEnumerable<string> identifiers, IReadOnlyList<string> markers)
        {
            if (identifiers == null || markers == null || markers.Count == 0) return false;
            foreach (var id in identifiers)
                if (MatchesAny(id, markers)) return true;
            return false;
        }

        /// <summary>
        /// Reads a static string version member <c>typeFullName.memberName</c> (field, property, or const)
        /// from any loaded assembly via reflection — i.e. the version a manually-installed SDK reports at
        /// runtime. Used to detect a downgrade before migrating that manual copy to a pinned UPM version.
        /// Returns null when not configured, the type/member is absent, or the value isn't a non-empty
        /// string. Tolerant of partially-loaded assemblies; never throws.
        /// </summary>
        public static string ReadStaticVersion(string typeFullName, string memberName)
        {
            if (string.IsNullOrEmpty(typeFullName) || string.IsNullOrEmpty(memberName)) return null;

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Static | BindingFlags.FlattenHierarchy;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(typeFullName, throwOnError: false); }
                catch { continue; }
                if (t == null) continue;

                try
                {
                    var fi = t.GetField(memberName, Flags);     // covers const + static field
                    if (fi != null && fi.GetValue(null) is string fv && !string.IsNullOrEmpty(fv)) return fv;

                    var pi = t.GetProperty(memberName, Flags);
                    if (pi != null && pi.GetValue(null) is string pv && !string.IsNullOrEmpty(pv)) return pv;
                }
                catch { /* keep scanning other assemblies */ }
            }
            return null;
        }

        /// <summary>
        /// Read-only: relative-to-Assets paths of files under <paramref name="relativeDir"/> (e.g.
        /// "Plugins") whose file name matches a marker. Used to SURFACE this SDK's files left in a
        /// SHARED folder that the installer must NOT auto-delete (other SDKs may share it), so the
        /// user can prune them by hand. <c>.meta</c> files are skipped; capped at <paramref name="max"/>.
        /// Never throws.
        /// </summary>
        public static List<string> FindLooseFiles(IReadOnlyList<string> markers, string relativeDir, int max = 25)
        {
            var hits = new List<string>();
            if (markers == null || markers.Count == 0 || string.IsNullOrEmpty(relativeDir)) return hits;

            var assetsRoot = Application.dataPath;
            var dir = Path.GetFullPath(Path.Combine(assetsRoot, relativeDir));
            if (!Directory.Exists(dir)) return hits;

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetExtension(file), ".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!MatchesAny(Path.GetFileNameWithoutExtension(file), markers)) continue;

                    var full = Path.GetFullPath(file);
                    if (full.Length <= assetsRoot.Length) continue;
                    var rel = full.Substring(assetsRoot.Length).Replace('\\', '/').TrimStart('/');
                    hits.Add(rel);
                    if (hits.Count >= max) break;
                }
            }
            catch
            {
                // Read-only probe: any IO failure → return what we found so far.
            }
            return hits;
        }

        // ── Shared helpers ───────────────────────────────────────────────────

        /// <summary>Case-insensitive substring match of <paramref name="value"/> against any marker.</summary>
        internal static bool MatchesAny(string value, IReadOnlyList<string> markers)
        {
            if (string.IsNullOrEmpty(value) || markers == null) return false;
            foreach (var m in markers)
            {
                if (string.IsNullOrEmpty(m)) continue;
                if (value.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

    }
}
