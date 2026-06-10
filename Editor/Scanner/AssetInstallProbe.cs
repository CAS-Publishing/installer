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
    /// Presence is decided by REFLECTION over loaded types' namespaces (<see cref="CollectLoadedNamespaces"/>
    /// + <see cref="IsPresentInNamespaces"/>): this catches every install shape — asmdef, precompiled
    /// DLL, AND raw .cs scripts with no asmdef (they compile into Assembly-CSharp, so their namespace
    /// is still a loaded type). UPM packages also load types, but the classifier only consults this
    /// when the package is ABSENT from the manifest, so a positive means a non-UPM copy in the project.
    ///
    /// The folder(s) to delete for a migration are found on demand by <see cref="FindRootsForMigration"/>
    /// (a one-off Assets/ walk matching asmdef name/rootNamespace, DLL file name, and .cs namespace) —
    /// not during every scan, since reflection alone can't map a namespace back to a folder.
    /// Read-only; never throws.
    /// </summary>
    internal static class AssetInstallProbe
    {
        // ── Presence (reflection, per scan) ──────────────────────────────────

        /// <summary>Collects the namespaces of all loaded types. Tolerant of partially-loaded assemblies.</summary>
        public static HashSet<string> CollectLoadedNamespaces()
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
                    var ns = t?.Namespace;
                    if (!string.IsNullOrEmpty(ns)) set.Add(ns);
                }
            }
            return set;
        }

        /// <summary>True when any loaded namespace matches a marker (case-insensitive substring).</summary>
        public static bool IsPresentInNamespaces(IEnumerable<string> namespaces, IReadOnlyList<string> markers)
        {
            if (namespaces == null || markers == null || markers.Count == 0) return false;
            foreach (var ns in namespaces)
                if (MatchesAny(ns, markers)) return true;
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
                        hit = MatchesAny(FirstNamespace(file), markers);
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
