using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class AutoInstallProgressTests
    {
        private static HashSet<string> Set(params string[] ids) => new HashSet<string>(ids);

        [Test]
        public void FirstUnresolved_returns_first_missing_id_index()
        {
            var targets = new List<string> { "a", "b", "c" };
            Assert.AreEqual(0, AutoInstallProgress.FirstUnresolved(targets, Set()));
            Assert.AreEqual(1, AutoInstallProgress.FirstUnresolved(targets, Set("a")));
            Assert.AreEqual(2, AutoInstallProgress.FirstUnresolved(targets, Set("a", "b")));
        }

        [Test]
        public void FirstUnresolved_returns_minus_one_when_all_resolved()
        {
            var targets = new List<string> { "a", "b" };
            Assert.AreEqual(-1, AutoInstallProgress.FirstUnresolved(targets, Set("a", "b")));
        }

        [Test]
        public void FirstUnresolved_skips_already_resolved_earlier_ids()
        {
            // 'a' resolved but 'b' not → current step is 'b' (index 1), not blocked on 'a'.
            var targets = new List<string> { "a", "b", "c" };
            Assert.AreEqual(1, AutoInstallProgress.FirstUnresolved(targets, Set("a", "c")));
        }

        [Test]
        public void FirstUnresolved_handles_null_inputs()
        {
            Assert.AreEqual(-1, AutoInstallProgress.FirstUnresolved(null, Set("a")));
            Assert.AreEqual(0, AutoInstallProgress.FirstUnresolved(new List<string> { "a" }, null));
        }

        [Test]
        public void IsStepOverdue_false_when_no_deadline_armed()
        {
            // Deadline 0 (unarmed) must never trip, even at a large "now".
            Assert.IsFalse(AutoInstallProgress.IsStepOverdue(now: 9999, deadline: 0));
            Assert.IsFalse(AutoInstallProgress.IsStepOverdue(now: 9999, deadline: -1));
        }

        [Test]
        public void IsStepOverdue_true_only_at_or_past_deadline()
        {
            Assert.IsFalse(AutoInstallProgress.IsStepOverdue(now: 99, deadline: 100));
            Assert.IsTrue(AutoInstallProgress.IsStepOverdue(now: 100, deadline: 100));
            Assert.IsTrue(AutoInstallProgress.IsStepOverdue(now: 101, deadline: 100));
        }
    }
}
