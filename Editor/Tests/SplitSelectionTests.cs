using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Common;

namespace PSV.Installer.Tests
{
    public sealed class SplitSelectionTests
    {
        [Test]
        public void Select_adds_all_members_without_duplicates()
        {
            var selected = new List<string> { "com.a" };
            SplitSelection.SetGroup(selected, new[] { "com.a", "com.b", "com.c" }, true);
            CollectionAssert.AreEquivalent(new[] { "com.a", "com.b", "com.c" }, selected);
        }

        [Test]
        public void Deselect_removes_all_members()
        {
            var selected = new List<string> { "com.a", "com.b", "com.c", "other" };
            SplitSelection.SetGroup(selected, new[] { "com.a", "com.b", "com.c" }, false);
            CollectionAssert.AreEquivalent(new[] { "other" }, selected);
        }

        [Test]
        public void Null_inputs_are_safe()
        {
            Assert.DoesNotThrow(() => SplitSelection.SetGroup(null, new[] { "x" }, true));
            var s = new List<string>();
            Assert.DoesNotThrow(() => SplitSelection.SetGroup(s, null, true));
        }
    }
}
