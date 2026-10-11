using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    [TestFixture]
    public class EligibilityTests
    {
        private static readonly Vec3 Origin = new Vec3(0f, 0f, 0f);

        /// <summary>A chest that passes every non-range check; tests vary one field at a time.</summary>
        private static StorageCandidate Chest(
            Vec3 position,
            string objectName = "piece_chest(Clone)",
            bool parentIsShip = false,
            bool parentIsWagon = false,
            bool toggledOff = false,
            bool hasAccess = true,
            bool hasInventory = true)
        {
            return new StorageCandidate(
                StorageKind.Chest, position, objectName, parentIsShip, parentIsWagon,
                toggledOff, hasAccess, hasInventory);
        }

        /// <summary>A drawer that passes every non-range check; tests vary one field at a time.</summary>
        private static StorageCandidate Drawer(
            Vec3 position,
            string objectName = "drawer(Clone)",
            bool toggledOff = false,
            bool hasAccess = true,
            bool hasInventory = true)
        {
            return new StorageCandidate(
                StorageKind.Drawer, position, objectName, false, false,
                toggledOff, hasAccess, hasInventory);
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
    }
}
