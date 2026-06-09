using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PSV.Installer.Common;

namespace PSV.Installer.Tests
{
    public sealed class GitGuardTests
    {
        private string _repo;

        private static void Git(string dir, string args)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = dir, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            };
            using (var p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(10000); }
        }

        [SetUp] public void SetUp()
        {
            _repo = Path.Combine(Path.GetTempPath(), "psvgit_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_repo);
            Git(_repo, "init");
            Git(_repo, "config user.email t@t.t");
            Git(_repo, "config user.name t");
        }

        [TearDown] public void TearDown()
        {
            // .git holds read-only files on Windows; best-effort cleanup.
            try { if (Directory.Exists(_repo)) { foreach (var f in Directory.GetFiles(_repo, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(_repo, true); } } catch { }
        }

        [Test]
        public void Tracked_and_clean_is_true()
        {
            var dir = Path.Combine(_repo, "Plugins"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "a.txt"), "x");
            Git(_repo, "add -A"); Git(_repo, "commit -m init");

            Assert.IsTrue(GitGuard.IsTrackedAndClean(dir, out var reason), reason);
        }

        [Test]
        public void Untracked_is_false()
        {
            var dir = Path.Combine(_repo, "New"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "b.txt"), "x");
            Assert.IsFalse(GitGuard.IsTrackedAndClean(dir, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Modified_is_false()
        {
            var dir = Path.Combine(_repo, "Mod"); Directory.CreateDirectory(dir);
            var f = Path.Combine(dir, "c.txt");
            File.WriteAllText(f, "x");
            Git(_repo, "add -A"); Git(_repo, "commit -m init");
            File.WriteAllText(f, "changed");
            Assert.IsFalse(GitGuard.IsTrackedAndClean(dir, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Non_git_directory_is_false()
        {
            var plain = Path.Combine(Path.GetTempPath(), "psvplain_" + Path.GetRandomFileName());
            Directory.CreateDirectory(plain);
            try { Assert.IsFalse(GitGuard.IsTrackedAndClean(plain, out var reason)); Assert.IsNotEmpty(reason); }
            finally { Directory.Delete(plain, true); }
        }

        [Test]
        public void Dir_with_untracked_sibling_is_false()
        {
            var dir = Path.Combine(_repo, "Mix"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "tracked.txt"), "x");
            Git(_repo, "add -A"); Git(_repo, "commit -m init");
            File.WriteAllText(Path.Combine(dir, "untracked.txt"), "y"); // now dir has a tracked + an untracked file
            Assert.IsFalse(GitGuard.IsTrackedAndClean(dir, out var reason));
            Assert.IsNotEmpty(reason);
        }
    }
}
