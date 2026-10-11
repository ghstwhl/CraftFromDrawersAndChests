using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The pure decisions behind the build-mode pull key: CraftFromContainers' guard line for whether
    /// an attack press is ours at all (CFC <c>BepInExPlugin.cs</c> line 1102), the placement-status
    /// test that guards the pull (CFC lines 1115-1116), and the branch order that decides whether the
    /// press is swallowed, handed back to vanilla, or turned into a pull (CFC lines 1108-1123). They
    /// live in <see cref="BuildPullRules"/> so they run outside the game; the Harmony patch that reads
    /// the live input and the placement state cannot.
    /// </summary>
    [TestFixture]
    public class BuildPullRulesTests
    {
        /// <summary>An attack press with every guard satisfied — the only shape that is ours.</summary>
        private static bool Intercepts(
            bool modEnabled = true,
            bool pullKeyHeld = true,
            bool preventKeyHeld = false,
            bool switchPrevent = false,
            bool inPlaceMode = true,
            bool takeInput = true,
            bool pieceSelectionVisible = false)
        {
            return BuildPullRules.ShouldIntercept(
                modEnabled, pullKeyHeld, preventKeyHeld, switchPrevent, inPlaceMode, takeInput, pieceSelectionVisible);
        }

        [Test]
        public void ShouldIntercept_EveryGuardSatisfied_IsTrue()
        {
            Assert.That(Intercepts(), Is.True);
        }

        [Test]
        public void ShouldIntercept_ModDisabled_IsFalse()
        {
            Assert.That(Intercepts(modEnabled: false), Is.False);
        }

        [Test]
        public void ShouldIntercept_PullKeyNotHeld_IsFalse()
        {
            Assert.That(Intercepts(pullKeyHeld: false), Is.False);
        }

        [Test]
        public void ShouldIntercept_PullKeyNotHeldWithSwitchOff_IsFalse()
        {
            // With the switch off, holding the prevent key suppresses the mod anyway; a missing pull
            // key refuses for its own reason. Neither state may consume the press.
            Assert.That(Intercepts(pullKeyHeld: false, preventKeyHeld: false, switchPrevent: false), Is.False);
            Assert.That(Intercepts(pullKeyHeld: false, preventKeyHeld: true, switchPrevent: false), Is.False);
        }

        [Test]
        public void ShouldIntercept_PullKeyNotHeldWithSwitchOn_IsFalse()
        {
            // With the switch on the prevent gate alone allows (prevent held) or refuses (prevent not
            // held); the pull key must be required in both, so a missing one never intercepts.
            Assert.That(Intercepts(pullKeyHeld: false, preventKeyHeld: true, switchPrevent: true), Is.False);
            Assert.That(Intercepts(pullKeyHeld: false, preventKeyHeld: false, switchPrevent: true), Is.False);
        }

        [Test]
        public void ShouldIntercept_PullKeyHeldButSuppressedByPreventKey_IsFalse()
        {
            // The shipped default: holding the prevent key suppresses the mod, so the press is vanilla's.
            Assert.That(Intercepts(preventKeyHeld: true), Is.False);
        }

        [Test]
        public void ShouldIntercept_SwitchOnAndPreventKeyHeld_IsTrue()
        {
            // Switched: the mod only acts while the prevent key is held.
            Assert.That(Intercepts(preventKeyHeld: true, switchPrevent: true), Is.True);
        }

        [Test]
        public void ShouldIntercept_SwitchOnAndPreventKeyNotHeld_IsFalse()
        {
            Assert.That(Intercepts(preventKeyHeld: false, switchPrevent: true), Is.False);
        }

        [Test]
        public void ShouldIntercept_NotInPlaceMode_IsFalse()
        {
            Assert.That(Intercepts(inPlaceMode: false), Is.False);
        }

        [Test]
        public void ShouldIntercept_NotTakingInput_IsFalse()
        {
            Assert.That(Intercepts(takeInput: false), Is.False);
        }

        [Test]
        public void ShouldIntercept_PieceSelectionVisible_IsFalse()
        {
            // The build menu's piece picker is open: the press belongs to the menu.
            Assert.That(Intercepts(pieceSelectionVisible: true), Is.False);
        }

        [Test]
        public void IsPlacementValid_ValidZero_IsTrue()
        {
            // Player.PlacementStatus.Valid == 0, the value CraftFromContainers compares against.
            Assert.That(BuildPullRules.IsPlacementValid(0), Is.True);
        }

        [Test]
        public void IsPlacementValid_EveryNonZeroStatus_IsFalse()
        {
            // Invalid, BlockedbyPlayer, NoBuildZone, MoreSpace, WrongBiome, DeepSnow — none may pull.
            foreach (int status in new[] { 1, 2, 3, 5, 8, 14 })
                Assert.That(BuildPullRules.IsPlacementValid(status), Is.False, "status=" + status);
        }

        [Test]
        public void IsPlacementValid_UnreadableStatus_IsFalse()
        {
            // The field could not be read (a later game version): fail closed, do not pull.
            Assert.That(BuildPullRules.IsPlacementValid(-1), Is.False);
        }

        [Test]
        public void PlacementStatusOf_NullField_IsMinusOne()
        {
            // The reader half of the gate: a null field value (a renamed or absent
            // Player.m_placementStatus) becomes the -1 sentinel, which IsPlacementValid treats as not
            // valid — so the press is swallowed rather than pulling on an unknown layout. Only the
            // predicate side was pinned before; this half would degrade silently otherwise.
            Assert.That(BuildPullRules.PlacementStatusOf(null), Is.EqualTo(-1));
            Assert.That(BuildPullRules.IsPlacementValid(BuildPullRules.PlacementStatusOf(null)), Is.False);
        }

        [Test]
        public void PlacementStatusOf_BoxedStatus_IsItsNumber()
        {
            Assert.That(BuildPullRules.PlacementStatusOf(0), Is.EqualTo(0));
            Assert.That(BuildPullRules.PlacementStatusOf(5), Is.EqualTo(5));

            // The field's real shape is a boxed enum; it converts through its underlying value, so a
            // valid status still reads as 0.
            Assert.That(BuildPullRules.PlacementStatusOf(System.DayOfWeek.Monday), Is.EqualTo(1));
        }

        [Test]
        public void PlacementStatusOf_UnconvertibleValue_IsMinusOne()
        {
            Assert.That(BuildPullRules.PlacementStatusOf("not a status"), Is.EqualTo(-1));
        }

        [Test]
        public void Decide_PlaceableSelectedPiece_Pulls()
        {
            Assert.That(
                BuildPullRules.Decide(pieceSelected: true, repairPiece: false, placementGhostPresent: true, placementValid: true),
                Is.EqualTo(BuildPullDecision.Pull));
        }

        [Test]
        public void Decide_NoPieceSelected_RefusesWithoutPulling()
        {
            Assert.That(
                BuildPullRules.Decide(pieceSelected: false, repairPiece: false, placementGhostPresent: true, placementValid: true),
                Is.EqualTo(BuildPullDecision.RefuseWithoutPull));
        }

        [Test]
        public void Decide_RepairPiece_LetsVanillaHandle()
        {
            // A repair piece is not built and consumes nothing, so the press stays vanilla's.
            Assert.That(
                BuildPullRules.Decide(pieceSelected: true, repairPiece: true, placementGhostPresent: true, placementValid: true),
                Is.EqualTo(BuildPullDecision.LetVanillaHandle));
        }

        [Test]
        public void Decide_NoPlacementGhost_RefusesWithoutPulling()
        {
            Assert.That(
                BuildPullRules.Decide(pieceSelected: true, repairPiece: false, placementGhostPresent: false, placementValid: true),
                Is.EqualTo(BuildPullDecision.RefuseWithoutPull));
        }

        [Test]
        public void Decide_PlacementNotValid_RefusesWithoutPulling()
        {
            Assert.That(
                BuildPullRules.Decide(pieceSelected: true, repairPiece: false, placementGhostPresent: true, placementValid: false),
                Is.EqualTo(BuildPullDecision.RefuseWithoutPull));
        }

        [Test]
        public void Decide_RepairPieceBeatsAMissingGhost()
        {
            // Branch order is CraftFromContainers': the repair test runs before the ghost test.
            Assert.That(
                BuildPullRules.Decide(pieceSelected: true, repairPiece: true, placementGhostPresent: false, placementValid: false),
                Is.EqualTo(BuildPullDecision.LetVanillaHandle));
        }

        [Test]
        public void Decide_NoPieceBeatsARepairPiece()
        {
            // ... and the selected-piece test runs before the repair test.
            Assert.That(
                BuildPullRules.Decide(pieceSelected: false, repairPiece: true, placementGhostPresent: true, placementValid: true),
                Is.EqualTo(BuildPullDecision.RefuseWithoutPull));
        }
    }
}
