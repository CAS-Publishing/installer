using NUnit.Framework;

namespace PSV.Installer.Tests
{
    public class InstallRetryPolicyTests
    {
        [Test]
        public void ExclusiveAccess_collision_is_transient()
        {
            // The canonical Package-Manager-busy message during initial project import. Must NOT
            // latch the once-per-session throttle — the next domain reload should retry.
            Assert.IsTrue(InstallRetryPolicy.IsTransient(
                "An operation that requires exclusive access to the project is currently running " +
                "and must finish before another can be started."));
        }

        [Test]
        public void Already_in_progress_is_transient()
        {
            Assert.IsTrue(InstallRetryPolicy.IsTransient(
                "An operation is already in progress."));
        }

        [Test]
        public void Auth_and_not_found_and_network_are_terminal()
        {
            // Terminal failures SHOULD throttle — retrying every reload only spams the console.
            Assert.IsFalse(InstallRetryPolicy.IsTransient("401 Unauthorized"));
            Assert.IsFalse(InstallRetryPolicy.IsTransient("404 Not Found: package does not exist"));
            Assert.IsFalse(InstallRetryPolicy.IsTransient(
                "Cannot connect to destination host npm.psvgamestudio.com"));
        }

        [Test]
        public void Unknown_or_empty_is_treated_as_terminal()
        {
            // Can't classify → conservative: throttle (matches the pre-existing behaviour, avoids
            // an unbounded retry on an unrecognised persistent failure).
            Assert.IsFalse(InstallRetryPolicy.IsTransient(null));
            Assert.IsFalse(InstallRetryPolicy.IsTransient(""));
            Assert.IsFalse(InstallRetryPolicy.IsTransient("something unexpected happened"));
        }

        [Test]
        public void Match_is_case_insensitive()
        {
            Assert.IsTrue(InstallRetryPolicy.IsTransient("EXCLUSIVE ACCESS to the project"));
            Assert.IsTrue(InstallRetryPolicy.IsTransient("Already In Progress"));
        }
    }
}
