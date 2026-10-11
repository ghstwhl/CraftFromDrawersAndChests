using System.Collections.Generic;
using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The pure decisions behind the ctrl-click pull key: the prevent-key gate (ported from
    /// CraftFromContainers 4.0.7's <c>AllowByKey()</c>), the click-to-pull test, the shortfall-name
    /// de-duplication and the two feedback messages. They live in <see cref="PullKeyRules"/> so they
    /// run outside the game; the Harmony patch that reads the live key state and the crafting panel
    /// cannot.
    /// </summary>
    [TestFixture]
    public class PullKeyRulesTests
    {
        [Test]
        public void AllowByKey_SwitchOff_KeyHeldSuppresses()
        {
            // The shipped default: holding the prevent key suppresses the mod.
            Assert.That(PullKeyRules.AllowByKey(false, true), Is.False);
        }

        [Test]
        public void AllowByKey_SwitchOff_KeyNotHeldAllows()
        {
            Assert.That(PullKeyRules.AllowByKey(false, false), Is.True);
        }

        [Test]
        public void AllowByKey_SwitchOn_KeyHeldAllows()
        {
            // Switched: the mod only acts while the prevent key is held.
            Assert.That(PullKeyRules.AllowByKey(true, true), Is.True);
        }

        [Test]
        public void AllowByKey_SwitchOn_KeyNotHeldSuppresses()
        {
            Assert.That(PullKeyRules.AllowByKey(true, false), Is.False);
        }

        [Test]
        public void AllowByKey_SwitchPrevent_IsExactlyTheInverseOfItself()
        {
            // The switch flips which key state means "go": the two settings disagree on both states.
            foreach (bool held in new[] { false, true })
                Assert.That(PullKeyRules.AllowByKey(true, held), Is.Not.EqualTo(PullKeyRules.AllowByKey(false, held)), "held=" + held);
        }

        [Test]
        public void ShouldPull_RequiresThePullKey()
        {
            Assert.That(PullKeyRules.ShouldPull(false, false, false), Is.False);
            Assert.That(PullKeyRules.ShouldPull(false, false, true), Is.False);
        }

        [Test]
        public void ShouldPull_PullKeyHeldAndAllowed_IsTrue()
        {
            Assert.That(PullKeyRules.ShouldPull(true, false, false), Is.True);
        }

        [Test]
        public void ShouldPull_PullKeyHeldButSuppressedByPreventKey_IsFalse()
        {
            // Pull key held, but the prevent key (switch off) suppresses the mod: vanilla wins.
            Assert.That(PullKeyRules.ShouldPull(true, true, false), Is.False);
        }

        [Test]
        public void ShouldPull_PullKeyHeldWithSwitchOnAndPreventKeyHeld_IsTrue()
        {
            Assert.That(PullKeyRules.ShouldPull(true, true, true), Is.True);
        }

        [Test]
        public void DistinctNames_Null_IsEmpty()
        {
            Assert.That(PullKeyRules.DistinctNames(null), Is.Empty);
        }

        [Test]
        public void DistinctNames_DropsDuplicateNames()
        {
            var names = new List<string> { "Wood", "Resin", "Wood" };
            Assert.That(PullKeyRules.DistinctNames(names), Is.EqualTo(new[] { "Wood", "Resin" }));
        }

        [Test]
        public void DistinctNames_PreservesFirstSeenOrder()
        {
            var names = new List<string> { "Resin", "Wood", "Resin", "Stone" };
            Assert.That(PullKeyRules.DistinctNames(names), Is.EqualTo(new[] { "Resin", "Wood", "Stone" }));
        }

        [Test]
        public void DistinctNames_DropsNullAndEmptyEntries()
        {
            var names = new List<string> { "Wood", null, string.Empty, "Resin" };
            Assert.That(PullKeyRules.DistinctNames(names), Is.EqualTo(new[] { "Wood", "Resin" }));
        }

        [Test]
        public void MissingMessage_NamesEachMissingItemOnce()
        {
            var missing = new List<string> { "Wood", "Wood", "Resin" };
            string message = PullKeyRules.MissingMessage(missing);

            Assert.That(message, Does.Contain("Wood"));
            Assert.That(message, Does.Contain("Resin"));
            // "Wood" appears exactly once, not once per repeated request.
            Assert.That(CountOccurrences(message, "Wood"), Is.EqualTo(1));
        }

        [Test]
        public void MissingMessage_EmptyList_IsAGenericReason()
        {
            Assert.That(PullKeyRules.MissingMessage(new List<string>()), Is.Not.Empty);
            Assert.That(PullKeyRules.MissingMessage(null), Is.Not.Empty);
        }

        [Test]
        public void NoRoomMessage_IsNotEmpty()
        {
            Assert.That(PullKeyRules.NoRoomMessage, Is.Not.Empty);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int index = 0;
            while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }
    }
}
