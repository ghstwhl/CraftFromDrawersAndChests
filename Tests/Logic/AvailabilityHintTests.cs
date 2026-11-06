using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The "nearby but blocked" hint. These tests pin the three decisions the crafting readout makes
    /// from pure values: whether to hint at all, what marker to append, and how to phrase the reason —
    /// so the game-facing readout has nothing left to decide and cannot drift from the rule.
    /// </summary>
    [TestFixture]
    public class AvailabilityHintTests
    {
        // --- ShouldHint: only an *uncovered* requirement with blocked stock behind it ---

        [Test]
        public void CoveredRequirement_IsNotHinted()
        {
            // Storage covers it: that is the flash state, not the blocked state.
            Assert.That(AvailabilityHint.ShouldHint(required: 2, available: 2, blockedStock: 5), Is.False);
            Assert.That(AvailabilityHint.ShouldHint(required: 2, available: 9, blockedStock: 5), Is.False);
        }

        [Test]
        public void UncoveredRequirementWithNothingBlocked_IsNotHinted()
        {
            // The ordinary "you have none" case must stay silent.
            Assert.That(AvailabilityHint.ShouldHint(required: 2, available: 0, blockedStock: 0), Is.False);
            Assert.That(AvailabilityHint.ShouldHint(required: 2, available: 1, blockedStock: 0), Is.False);
        }

        [Test]
        public void UncoveredRequirementWithBlockedStock_IsHinted()
        {
            Assert.That(AvailabilityHint.ShouldHint(required: 2, available: 0, blockedStock: 1), Is.True);
            Assert.That(AvailabilityHint.ShouldHint(required: 2, available: 0, blockedStock: 40), Is.True);
        }

        [Test]
        public void PartiallyBlockedShortfall_IsHinted()
        {
            // blockedStock need not cover the whole shortfall: the hint explains why the number looks
            // wrong, it never promises the item is reachable.
            Assert.That(AvailabilityHint.ShouldHint(required: 5, available: 1, blockedStock: 2), Is.True);
        }

        [Test]
        public void NoRequirement_IsNotHinted()
        {
            Assert.That(AvailabilityHint.ShouldHint(required: 0, available: 0, blockedStock: 5), Is.False);
        }

        // --- Suffix: the configured marker, verbatim ---

        [Test]
        public void Suffix_IsAppendedVerbatim()
        {
            Assert.That(AvailabilityHint.Suffix(" *"), Is.EqualTo(" *"));
            Assert.That(AvailabilityHint.Suffix(" (locked)"), Is.EqualTo(" (locked)"));
        }

        [Test]
        public void Suffix_EmptySettingAddsNothing()
        {
            // An empty marker is a supported configuration: the tint still carries the signal.
            Assert.That(AvailabilityHint.Suffix(""), Is.Empty);
            Assert.That(AvailabilityHint.Suffix(null), Is.Empty);
        }

        // --- ReasonPhrase: one phrase per flag, and one for the combination ---

        [Test]
        public void ReasonPhrase_SwitchedOff()
        {
            Assert.That(
                AvailabilityHint.ReasonPhrase(StorageBlockReasons.SwitchedOff),
                Is.EqualTo(AvailabilityHint.SwitchedOffPhrase));
        }

        [Test]
        public void ReasonPhrase_NoAccess()
        {
            Assert.That(
                AvailabilityHint.ReasonPhrase(StorageBlockReasons.NoAccess),
                Is.EqualTo(AvailabilityHint.NoAccessPhrase));
        }

        [Test]
        public void ReasonPhrase_BothReasons_NamesBothOnce()
        {
            StorageBlockReasons both = StorageBlockReasons.SwitchedOff | StorageBlockReasons.NoAccess;

            Assert.That(AvailabilityHint.ReasonPhrase(both), Is.EqualTo(AvailabilityHint.BothPhrase));
            Assert.That(AvailabilityHint.BothPhrase, Does.Contain(AvailabilityHint.SwitchedOffPhrase));
            Assert.That(AvailabilityHint.BothPhrase, Does.Contain(AvailabilityHint.NoAccessPhrase));
        }

        [Test]
        public void ReasonPhrase_None_IsEmpty()
        {
            Assert.That(AvailabilityHint.ReasonPhrase(StorageBlockReasons.None), Is.Empty);
        }

        // --- Message: the one-off HUD line ---

        [Test]
        public void Message_NamesTheItemAndTheReason()
        {
            string message = AvailabilityHint.Message("Seal Blubber", StorageBlockReasons.SwitchedOff);

            Assert.That(message, Does.StartWith("Seal Blubber"));
            Assert.That(message, Does.Contain(AvailabilityHint.SwitchedOffPhrase));
            Assert.That(message, Does.EndWith("."));
        }

        [Test]
        public void Message_LockedChest_SaysLocked()
        {
            string message = AvailabilityHint.Message("Seal Blubber", StorageBlockReasons.NoAccess);

            Assert.That(message, Does.Contain(AvailabilityHint.NoAccessPhrase));
        }

        [Test]
        public void Message_WithoutAReason_IsEmpty()
        {
            // Nothing blocked means there is nothing to say; the caller need not special-case it.
            Assert.That(AvailabilityHint.Message("Seal Blubber", StorageBlockReasons.None), Is.Empty);
        }

        [Test]
        public void Message_WithoutAUsableName_StillReadsAsASentence()
        {
            string message = AvailabilityHint.Message(null, StorageBlockReasons.SwitchedOff);

            Assert.That(message, Does.Contain(AvailabilityHint.SwitchedOffPhrase));
            Assert.That(message, Does.Not.StartWith(" "));
        }
    }
}
