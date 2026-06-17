using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
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
    /// The folder(s) to delete for a migration are found on demand by <see cref="FindRootsForMigration"/>
    /// (a one-off Assets/ walk matching asmdef name/rootNamespace, DLL file name, and .cs namespace) —
    /// not during every scan, since reflection alone can't map a namespace back to a folder.
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

        // ── Migration roots (file walk, on demand) ───────────────────────────

        /// <summary>
        /// Finds the top-level <c>Assets/</c> folders (relative to Assets/) that hold this SDK, by
        /// matching asmdef name/rootNamespace, DLL file name, and .cs file namespace against the
        /// markers. Called only when the user clicks Migrate. Never null.
        /// </summary>
        public static List<string> FindRootsForMigration(IReadOnlyList<string> markers)
        {
            var roots = new List<string>();
            if (markers == null || markers.Count == 0) return roots;

            var assetsRoot = Application.dataPath;
            try
            {
                foreach (var file in Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file);
                    bool hit;
                    if (string.Equals(ext, ".asmdef", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadAsmdef(file, out var name, out var rootNs);
                        hit = MatchesAny(name, markers) || MatchesAny(rootNs, markers);
                    }
                    else if (string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        hit = MatchesAny(Path.GetFileNameWithoutExtension(file), markers);
                    }
                    else if (string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        // Namespace first; fall back to the file name for global-namespace SDKs
                        // (e.g. Tenjin's BaseTenjin.cs / Tenjin.cs declare no namespace), mirroring
                        // the DLL filename match above so those roots are still located.
                        hit = MatchesAny(FirstNamespace(file), markers)
                              || MatchesAny(Path.GetFileNameWithoutExtension(file), markers);
                    }
                    else continue;

                    if (!hit) continue;
                    var root = TopRoot(assetsRoot, file);
                    if (!string.IsNullOrEmpty(root) && !roots.Contains(root)) roots.Add(root);
                }
            }
            catch
            {
                // Read-only probe: any IO failure → return what we found so far.
            }
            return roots;
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

        private static readonly Regex NamespaceRx =
            new Regex(@"\bnamespace\s+([A-Za-z_][\w.]*)", RegexOptions.Compiled);

        // First declared namespace in a .cs file. Reads only the head of the file (namespaces sit
        // near the top, after usings) so the on-demand migration walk stays cheap.
        private static string FirstNamespace(string path)
        {
            try
            {
                string head;
                using (var sr = new StreamReader(path))
                {
                    var buf = new char[4096];
                    var n = sr.Read(buf, 0, buf.Length);
                    head = new string(buf, 0, n);
                }
                var m = NamespaceRx.Match(head);
                return m.Success ? m.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        private static void ReadAsmdef(string path, out string name, out string rootNamespace)
        {
            name = null;
            rootNamespace = null;
            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                name = root.Value<string>("name");
                rootNamespace = root.Value<string>("rootNamespace");
            }
            catch
            {
                // unreadable / non-JSON asmdef → leave nulls
            }
        }

        // Top-level Assets/ folder containing the file (relative to Assets/), or null when the file
        // sits directly under Assets/ — we never offer to delete loose root-level files as a folder.
        private static string TopRoot(string assetsRoot, string filePath)
        {
            var full = Path.GetFullPath(filePath);
            if (full.Length <= assetsRoot.Length) return null;
            var rel = full.Substring(assetsRoot.Length).Replace('\\', '/').TrimStart('/');
            var slash = rel.IndexOf('/');
            return slash <= 0 ? null : rel.Substring(0, slash);
        }
    }
}
