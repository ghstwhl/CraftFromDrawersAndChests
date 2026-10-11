using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The two decisions behind the per-storage switch that need no game: the hover suffix text and
    /// whether a toggle key is configured at all. Both live in <see cref="ToggleRules"/> so they run
    /// outside the game; the ZDO read/write and the Harmony patches around them cannot.
    /// </summary>
    [TestFixture]
    public class ToggleRulesTests
    {
        [Test]
        public void HoverSuffix_Off_SaysPullOff()
        {
            // The argument is the storage's state, taken straight from StorageToggle.IsOff: an off
            // storage must read "pull: off", not the inverse.
            Assert.That(ToggleRules.HoverSuffix(true), Is.EqualTo("<color=#FF5555> (pull: off)</color>"));
        }

        [Test]
        public void HoverSuffix_On_SaysPullOn()
        {
            Assert.That(ToggleRules.HoverSuffix(false), Is.EqualTo("<color=#55FF55> (pull: on)</color>"));
        }

        [Test]
        public void HoverSuffix_IsColouredRichText()
        {
            // The hover line is rendered by the game's TextMeshPro hover element, which understands
            // Unity rich text; the state must be visually distinct, so it is colour-wrapped.
            foreach (bool off in new[] { false, true })
            {
                string suffix = ToggleRules.HoverSuffix(off);
                Assert.That(suffix, Does.StartWith("<color="), "off=" + off);
                Assert.That(suffix, Does.EndWith("</color>"), "off=" + off);
            }
        }

        [Test]
        public void HoverSuffix_OffAndOn_Differ()
        {
            Assert.That(ToggleRules.HoverSuffix(false), Is.Not.EqualTo(ToggleRules.HoverSuffix(true)));
        }

        [Test]
        public void IsKeyConfigured_Null_IsFalse()
        {
            Assert.That(ToggleRules.IsKeyConfigured(null), Is.False);
        }

        [Test]
        public void IsKeyConfigured_Empty_IsFalse()
        {
            // The shipped default is empty, so out of the box the toggle is unbound.
            Assert.That(ToggleRules.IsKeyConfigured(string.Empty), Is.False);
        }

        [Test]
        public void IsKeyConfigured_Whitespace_IsFalse()
        {
            Assert.That(ToggleRules.IsKeyConfigured("   "), Is.False);
        }

        [Test]
        public void IsKeyConfigured_AKeyName_IsTrue()
        {
            Assert.That(ToggleRules.IsKeyConfigured("left ctrl"), Is.True);
        }

        [Test]
        public void ToggleHint_BoundKey_NamesTheKey()
        {
            Assert.That(ToggleRules.ToggleHint("left shift"), Is.EqualTo(" (press Left Shift to toggle)"));
        }

        [Test]
        public void ToggleHint_SingleLetterKey_IsCapitalised()
        {
            Assert.That(ToggleRules.ToggleHint("n"), Is.EqualTo(" (press N to toggle)"));
        }

        [Test]
        public void ToggleHint_RemainingLettersAreLowerCased()
        {
            // A setting written in an unexpected case must still read sensibly, not as "LeFt ShIfT".
            Assert.That(ToggleRules.ToggleHint("LeFt ShIfT"), Is.EqualTo(" (press Left Shift to toggle)"));
        }

        [Test]
        public void ToggleHint_Unbound_SaysNoKeyIsSet()
        {
            const string expected = " (no toggle key is set)";
            Assert.That(ToggleRules.ToggleHint(null), Is.EqualTo(expected));
            Assert.That(ToggleRules.ToggleHint(string.Empty), Is.EqualTo(expected));
            Assert.That(ToggleRules.ToggleHint("   "), Is.EqualTo(expected));
        }

        [Test]
        public void ToggleHint_UnboundAndBound_Differ()
        {
            Assert.That(ToggleRules.ToggleHint(null), Is.Not.EqualTo(ToggleRules.ToggleHint("n")));
        }

        [Test]
        public void ToggleHint_NeverRepeatsTheState()
        {
            // The hint is about the key only, so appending it after either state cannot read as though
            // the state itself were ambiguous.
            Assert.That(ToggleRules.ToggleHint("n"), Does.Not.Contain("pull:"));
            Assert.That(ToggleRules.ToggleHint(null), Does.Not.Contain("pull:"));
        }

        [Test]
        public void HoverSuffixAndToggleHint_AreBothPresentInAFullHoverLine()
        {
            // What a player actually sees, end to end for both halves of the addition.
            string line = "Wood chest" + ToggleRules.HoverSuffix(false) + ToggleRules.ToggleHint("n");
            Assert.That(line, Is.EqualTo("Wood chest<color=#55FF55> (pull: on)</color> (press N to toggle)"));
        }

        [Test]
        public void InputKeyName_Unbound_IsNull()
        {
            Assert.That(ToggleRules.InputKeyName(null), Is.Null);
            Assert.That(ToggleRules.InputKeyName(string.Empty), Is.Null);
            Assert.That(ToggleRules.InputKeyName("   "), Is.Null);
        }

        [Test]
        public void InputKeyName_IsTrimmedAndLowerCased()
        {
            // The input system matches key names case-sensitively, so this is what keeps a setting
            // written as "N" or "Left Shift" from being a silently dead key.
            Assert.That(ToggleRules.InputKeyName("N"), Is.EqualTo("n"));
            Assert.That(ToggleRules.InputKeyName("Left Shift"), Is.EqualTo("left shift"));
            Assert.That(ToggleRules.InputKeyName("  left ctrl  "), Is.EqualTo("left ctrl"));
        }

        [Test]
        public void InputKeyName_ClosedUpModifier_BecomesUnitysTwoWordName()
        {
            // Mod documentation usually writes "LeftShift"; Unity's own name is "left shift".
            Assert.That(ToggleRules.InputKeyName("LeftShift"), Is.EqualTo("left shift"));
            Assert.That(ToggleRules.InputKeyName("LeftControl"), Is.EqualTo("left ctrl"));
            Assert.That(ToggleRules.InputKeyName("RightAlt"), Is.EqualTo("right alt"));
        }

        [Test]
        public void InputKeyName_UnknownName_PassesThrough()
        {
            Assert.That(ToggleRules.InputKeyName("SomeKey"), Is.EqualTo("somekey"));
        }

        [Test]
        public void SplitKeys_SingleKey_IsOneToken()
        {
            Assert.That(ToggleRules.SplitKeys("n"), Is.EqualTo(new[] { "n" }));
        }

        [Test]
        public void SplitKeys_Combination_IsSplitAndNormalised()
        {
            Assert.That(ToggleRules.SplitKeys("left shift + e"), Is.EqualTo(new[] { "left shift", "e" }));
            Assert.That(ToggleRules.SplitKeys("  left shift+e  "), Is.EqualTo(new[] { "left shift", "e" }));
            Assert.That(ToggleRules.SplitKeys("LeftShift+E"), Is.EqualTo(new[] { "left shift", "e" }));
        }

        [Test]
        public void SplitKeys_UnboundOrEmptyParts_AreDropped()
        {
            Assert.That(ToggleRules.SplitKeys(null), Is.Empty);
            Assert.That(ToggleRules.SplitKeys("   "), Is.Empty);
            Assert.That(ToggleRules.SplitKeys("+"), Is.Empty);
            Assert.That(ToggleRules.SplitKeys("e+"), Is.EqualTo(new[] { "e" }));
            Assert.That(ToggleRules.SplitKeys("+e"), Is.EqualTo(new[] { "e" }));
        }

        [Test]
        public void ToggleHint_Combination_IsShownAsACombination()
        {
            Assert.That(ToggleRules.ToggleHint("left shift + e"),
                Is.EqualTo(" (press Left Shift + E to toggle)"));
            Assert.That(ToggleRules.ToggleHint("LeftShift+E"),
                Is.EqualTo(" (press Left Shift + E to toggle)"));
        }
    }
}
