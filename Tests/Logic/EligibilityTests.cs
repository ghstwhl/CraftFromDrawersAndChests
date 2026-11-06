using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    [TestFixture]
    public class EligibilityTests
    {
        private static readonly Vec3 Origin = new Vec3(0f, 0f, 0f);

        /// <summary>A chest that passes every non-range check; tests vary one field at a time.
        /// <c>isInsidePiece</c>/<c>passesGuardStone</c> default true so a chest stays eligible unless
        /// a test deliberately clears one.</summary>
        private static StorageCandidate Chest(
            Vec3 position,
            string objectName = "piece_chest(Clone)",
            bool parentIsShip = false,
            bool parentIsWagon = false,
            bool toggledOff = false,
            bool hasAccess = true,
            bool hasInventory = true,
            bool isInsidePiece = true,
            bool passesGuardStone = true)
        {
            return new StorageCandidate(
                StorageKind.Chest, position, objectName, parentIsShip, parentIsWagon,
                toggledOff, hasAccess, hasInventory, isInsidePiece, passesGuardStone);
        }

        /// <summary>A drawer that passes every non-range check; tests vary one field at a time.
        /// A drawer is never a <c>Piece</c> child and never has a guard stone, so those two fields
        /// default false and the predicate must ignore them for a drawer.</summary>
        private static StorageCandidate Drawer(
            Vec3 position,
            string objectName = "drawer(Clone)",
            bool toggledOff = false,
            bool hasAccess = true,
            bool hasInventory = true,
            bool isInsidePiece = false,
            bool passesGuardStone = false)
        {
            return new StorageCandidate(
                StorageKind.Drawer, position, objectName, false, false,
                toggledOff, hasAccess, hasInventory, isInsidePiece, passesGuardStone);
        }

        [Test]
        public void ChestAtExactlyRange_IsEligible()
        {
            // distance == range counts as in range: 10 away with range 10 must be eligible.
            var c = Chest(new Vec3(10f, 0f, 0f));
            Assert.That(
                Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.True);
        }

        [Test]
        public void ChestBeyondRange_IsNotEligible()
        {
            var c = Chest(new Vec3(10.5f, 0f, 0f));
            Assert.That(
                Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.False);
        }

        [Test]
        public void ChestToggledOff_IsNotEligible()
        {
            var c = Chest(new Vec3(1f, 0f, 0f), toggledOff: true);
            Assert.That(
                Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.False);
        }

        [Test]
        public void ChestWithoutAccess_IsNotEligible()
        {
            var c = Chest(new Vec3(1f, 0f, 0f), hasAccess: false);
            Assert.That(
                Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.False);
        }

        [Test]
        public void ChestWithNullInventory_IsNotEligible()
        {
            var c = Chest(new Vec3(1f, 0f, 0f), hasInventory: false);
            Assert.That(
                Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.False);
        }

        [Test]
        public void ChestOutsidePiece_IsNotEligible()
        {
            // A loose container is not part of a built Piece and is never a pull source.
            var c = Chest(new Vec3(1f, 0f, 0f), isInsidePiece: false);
            Assert.That(
                Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.False);
        }

        [Test]
        public void ChestDeniedByGuardStone_IsNotEligible()
        {
            // A guard-stone container is only a source where PrivateArea grants access.
            var c = Chest(new Vec3(1f, 0f, 0f), passesGuardStone: false);
            Assert.That(
                Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.False);
        }

        [Test]
        public void DrawerIsUnaffectedByPieceOrGuardStone()
        {
            // A drawer has no Piece requirement and no guard stone: both clauses are scoped to a
            // chest, so a drawer with both fields false is still eligible.
            var d = Drawer(new Vec3(1f, 0f, 0f), isInsidePiece: false, passesGuardStone: false);
            Assert.That(
                Eligibility.IsEligible(d, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.True);
        }

        [Test]
        public void ChestOpenByAnotherPlayer_IsEligible()
        {
            // Load-bearing: StorageCandidate has no "in use" field at all, by design. A chest that
            // is currently open (by this player or another) is indistinguishable from a closed one,
            // so it must stay eligible. This deliberately departs from CraftFromContainers, which
            // skipped chests another player had in use. Not redundant with the range test: it pins
            // the deliberate absence of an in-use exclusion.
            var c = Chest(new Vec3(1f, 0f, 0f));
            Assert.That(
                Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.True);
        }

        [Test]
        public void ChestsDisabled_DrawerStillEligible()
        {
            var drawer = Drawer(new Vec3(1f, 0f, 0f));
            var chest = Chest(new Vec3(1f, 0f, 0f));
            var rules = new ChestTypeRules();

            Assert.That(
                Eligibility.IsEligible(drawer, Origin, 10f, chestsEnabled: false, drawersEnabled: true, rules),
                Is.True);
            // The same candidate treated as a chest is not eligible while chests are disabled.
            Assert.That(
                Eligibility.IsEligible(chest, Origin, 10f, chestsEnabled: false, drawersEnabled: true, rules),
                Is.False);
        }

        [Test]
        public void DrawerIgnoresChestTypeRulesAndAccess()
        {
            // A drawer is not a Container: its name, access and inventory fields are irrelevant.
            // Even a name a chest rule would exclude, with no access and no inventory, it stays
            // eligible as long as drawers are enabled, it is not toggled off and it is in range.
            var rules = new ChestTypeRules
            {
                IgnoreShipContainers = true,
                IgnoreWagonContainers = true,
                IgnoreWoodChests = true,
                IgnoreReinforcedChests = true,
            };
            var drawer = Drawer(
                new Vec3(1f, 0f, 0f),
                objectName: "piece_chest_wood(Clone)",
                hasAccess: false,
                hasInventory: false);

            Assert.That(
                Eligibility.IsEligible(drawer, Origin, 10f, chestsEnabled: true, drawersEnabled: true, rules),
                Is.True);
        }

        [Test]
        public void ChestWithNullName_DoesNotThrow()
        {
            // Task 2 deferred a minor: ChestTypeRules.IsExcluded throws NullReferenceException when
            // objectName is null and a name rule is enabled. Eligibility is the caller that will
            // assemble candidates from possibly-missing components, so it guards at this call site
            // (Task 2's file is left untouched): a missing name is treated as "no name rule matches".
            var rules = new ChestTypeRules { IgnoreWoodChests = true, IgnoreReinforcedChests = true };
            var c = Chest(new Vec3(1f, 0f, 0f), objectName: null);

            Assert.That(
                Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, rules),
                Is.True);
        }

        [Test]
        public void RangeZero_MeansUnlimited()
        {
            // CraftFromContainers parity, by the owner's ruling (Task 15e). CFC treats a non-positive
            // range as unlimited, so a player carrying a 0 over from it used to get a silently dead mod
            // here: a zero radius excludes every container that is not exactly at the origin, and
            // nothing in the log said why. A chest 1000 away must therefore be in range when it is 0.
            var c = Chest(new Vec3(1000f, 0f, 0f));

            Assert.That(
                Eligibility.IsEligible(c, Origin, 0f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.True);
        }

        [Test]
        public void NegativeRange_MeansUnlimited()
        {
            var c = Chest(new Vec3(1000f, 0f, 0f));

            Assert.That(
                Eligibility.IsEligible(c, Origin, -5f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.True);
        }

        [Test]
        public void RangeZero_MeansUnlimitedForDrawersToo()
        {
            // Both kinds of storage read the one rule, so a non-positive range cannot mean unlimited for
            // a chest and nothing for a drawer.
            var drawer = Drawer(new Vec3(1000f, 0f, 0f));

            Assert.That(
                Eligibility.IsEligible(drawer, Origin, 0f, chestsEnabled: false, drawersEnabled: true, new ChestTypeRules()),
                Is.True);
        }

        [Test]
        public void RangeZero_StillObeysTheOtherChecks()
        {
            // Unlimited range must not become "ignores every other rule": a switched-off storage is
            // still excluded.
            var c = Chest(new Vec3(1000f, 0f, 0f), toggledOff: true);

            Assert.That(
                Eligibility.IsEligible(c, Origin, 0f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.False);
        }

        [Test]
        public void IsWithinRange_PositiveRangeStillLimits()
        {
            // The unlimited case must not have disabled the ordinary one.
            Assert.That(Eligibility.IsWithinRange(10f, 0f), Is.True);
            Assert.That(Eligibility.IsWithinRange(10f, 100f), Is.True, "distance exactly equal to the range is inside");
            Assert.That(Eligibility.IsWithinRange(10f, 100.25f), Is.False, "just beyond the range is outside");
        }

        [Test]
        public void IsWithinRange_NonPositiveRangeIsUnlimited()
        {
            Assert.That(Eligibility.IsWithinRange(0f, float.MaxValue), Is.True);
            Assert.That(Eligibility.IsWithinRange(-1f, float.MaxValue), Is.True);
        }

        // --- IsEligibleButForSwitch: the diagnostic question the debug readout asks ---
        //
        // The readout must be able to say "this storage holds what you need but is switched off",
        // and a switched-off storage is filtered out before the pullable list is built, so it can
        // never appear there. These tests pin the one difference between the two predicates: the
        // switch. Every other exclusion must still apply, or the readout would blame the switch for
        // a storage the pull could not use anyway.

        [Test]
        public void SwitchedOffDrawer_IsNotEligible_ButIsEligibleButForSwitch()
        {
            var c = Drawer(new Vec3(1f, 0f, 0f), toggledOff: true);
            var rules = new ChestTypeRules();

            Assert.That(Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, rules), Is.False);
            Assert.That(
                Eligibility.IsEligibleButForSwitch(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, rules),
                Is.True);
        }

        [Test]
        public void SwitchedOffChestInsideAPiece_IsEligibleButForSwitch()
        {
            var c = Chest(new Vec3(1f, 0f, 0f), toggledOff: true);
            var rules = new ChestTypeRules();

            Assert.That(Eligibility.IsEligible(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, rules), Is.False);
            Assert.That(
                Eligibility.IsEligibleButForSwitch(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, rules),
                Is.True);
        }

        [Test]
        public void EligibleStorage_IsEligibleButForSwitchToo()
        {
            // The predicate is not "off only": for an eligible storage both answers agree, so the
            // diagnostic list is the pullable list plus the switched-off storages and nothing else.
            var c = Drawer(new Vec3(1f, 0f, 0f));
            var rules = new ChestTypeRules();

            Assert.That(
                Eligibility.IsEligibleButForSwitch(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, rules),
                Is.True);
        }

        [Test]
        public void SwitchedOffChestOutsideAPiece_IsNotEligibleButForSwitch()
        {
            // A loose container is excluded for a reason the switch cannot excuse.
            var c = Chest(new Vec3(1f, 0f, 0f), toggledOff: true, isInsidePiece: false);

            Assert.That(
                Eligibility.IsEligibleButForSwitch(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.False);
        }

        [Test]
        public void SwitchedOffChestBeyondRange_IsNotEligibleButForSwitch()
        {
            var c = Chest(new Vec3(20f, 0f, 0f), toggledOff: true);

            Assert.That(
                Eligibility.IsEligibleButForSwitch(c, Origin, 10f, chestsEnabled: true, drawersEnabled: true, new ChestTypeRules()),
                Is.False);
        }

        [Test]
        public void SwitchedOffDrawerWithDrawersDisabled_IsNotEligibleButForSwitch()
        {
            var c = Drawer(new Vec3(1f, 0f, 0f), toggledOff: true);

            Assert.That(
                Eligibility.IsEligibleButForSwitch(c, Origin, 10f, chestsEnabled: true, drawersEnabled: false, new ChestTypeRules()),
                Is.False);
        }

        // --- TryBlockReason: the two states a player would call a lock ---
        //
        // The crafting readout asks "is this thing nearby, holding my item, but locked?" A true answer
        // must mean exactly that: in range, of an enabled kind, otherwise well-formed as storage, and
        // blocked only by the switch or by access. Everything else is "not a source" and must stay
        // silent, or the hint would blame a lock for a chest 90 m away.

        [Test]
        public void SwitchedOffDrawer_IsBlockedByTheSwitch()
        {
            var c = Drawer(new Vec3(1f, 0f, 0f), toggledOff: true);

            Assert.That(
                Eligibility.TryBlockReason(c, Origin, 10f, true, true, new ChestTypeRules(), out var reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(StorageBlockReason.SwitchedOff));
        }

        [Test]
        public void SwitchedOffChest_IsBlockedByTheSwitch()
        {
            var c = Chest(new Vec3(1f, 0f, 0f), toggledOff: true);

            Assert.That(
                Eligibility.TryBlockReason(c, Origin, 10f, true, true, new ChestTypeRules(), out var reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(StorageBlockReason.SwitchedOff));
        }

        [Test]
        public void ChestThatDeniesAccess_IsBlockedAsLocked()
        {
            // Another player's private chest.
            var c = Chest(new Vec3(1f, 0f, 0f), hasAccess: false);

            Assert.That(
                Eligibility.TryBlockReason(c, Origin, 10f, true, true, new ChestTypeRules(), out var reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(StorageBlockReason.NoAccess));
        }

        [Test]
        public void ChestDeniedByAWard_IsBlockedAsLocked()
        {
            var c = Chest(new Vec3(1f, 0f, 0f), passesGuardStone: false);

            Assert.That(
                Eligibility.TryBlockReason(c, Origin, 10f, true, true, new ChestTypeRules(), out var reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(StorageBlockReason.NoAccess));
        }

        [Test]
        public void SwitchWinsOverAccess_WhenBothApply()
        {
            // The player's own action is the more useful explanation, and one reason keeps the HUD
            // line short.
            var c = Chest(new Vec3(1f, 0f, 0f), toggledOff: true, hasAccess: false);

            Assert.That(
                Eligibility.TryBlockReason(c, Origin, 10f, true, true, new ChestTypeRules(), out var reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(StorageBlockReason.SwitchedOff));
        }

        [Test]
        public void EligibleStorage_IsNotReportedAsBlocked()
        {
            Assert.That(
                Eligibility.TryBlockReason(Chest(new Vec3(1f, 0f, 0f)), Origin, 10f, true, true, new ChestTypeRules(), out _),
                Is.False);
            Assert.That(
                Eligibility.TryBlockReason(Drawer(new Vec3(1f, 0f, 0f)), Origin, 10f, true, true, new ChestTypeRules(), out _),
                Is.False);
        }

        [Test]
        public void OutOfRangeStorage_IsNotReportedAsBlocked()
        {
            var c = Chest(new Vec3(50f, 0f, 0f), toggledOff: true);

            Assert.That(
                Eligibility.TryBlockReason(c, Origin, 10f, true, true, new ChestTypeRules(), out _),
                Is.False);
        }

        [Test]
        public void DisabledStorageKinds_AreNotReportedAsBlocked()
        {
            Assert.That(
                Eligibility.TryBlockReason(Drawer(new Vec3(1f, 0f, 0f), toggledOff: true), Origin, 10f, true, false, new ChestTypeRules(), out _),
                Is.False);
            Assert.That(
                Eligibility.TryBlockReason(Chest(new Vec3(1f, 0f, 0f), toggledOff: true), Origin, 10f, false, true, new ChestTypeRules(), out _),
                Is.False);
        }

        [Test]
        public void ChestExcludedForBeingOutsideAPiece_IsNotReportedAsBlocked()
        {
            var c = Chest(new Vec3(1f, 0f, 0f), toggledOff: true, isInsidePiece: false);

            Assert.That(
                Eligibility.TryBlockReason(c, Origin, 10f, true, true, new ChestTypeRules(), out _),
                Is.False);
        }

        [Test]
        public void ChestExcludedByItsType_IsNotReportedAsBlocked()
        {
            // A config exclusion is not a lock: the player chose not to use that chest kind.
            var rules = new ChestTypeRules { IgnoreWoodChests = true };
            var c = Chest(new Vec3(1f, 0f, 0f), objectName: "piece_chest_wood(Clone)", toggledOff: true);

            Assert.That(Eligibility.TryBlockReason(c, Origin, 10f, true, true, rules, out _), Is.False);
        }
    }
}
