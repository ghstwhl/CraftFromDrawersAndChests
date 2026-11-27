using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The decision the absorbed chest layer makes when a player asks to open a container. Version 2.1.0
    /// changed the custody rule: a container a player has already claimed keeps that owner, and only a
    /// container nobody has claimed is handed to the requester. Before that, every request from a
    /// non-owner moved ownership, which turned opening a chest into a change of who could count it.
    /// </summary>
    [TestFixture]
    public class ChestOpenRulesTests
    {
        [Test]
        public void AnIgnoredContainerIsLeftToVanilla()
        {
            Assert.That(
                ChestOpenRules.Decide(ignored: true, isOwner: true, hasAccess: true, containerClaimed: false),
                Is.EqualTo(ChestOpenAction.Vanilla));
        }

        [Test]
        public void AnInstanceThatIsNotTheOwnerLetsVanillaAnswer()
        {
            Assert.That(
                ChestOpenRules.Decide(ignored: false, isOwner: false, hasAccess: true, containerClaimed: true),
                Is.EqualTo(ChestOpenAction.Vanilla));
        }

        [Test]
        public void NoAccessIsRefused()
        {
            Assert.That(
                ChestOpenRules.Decide(ignored: false, isOwner: true, hasAccess: false, containerClaimed: false),
                Is.EqualTo(ChestOpenAction.DenyAccess));
        }

        [Test]
        public void AContainerNobodyHasClaimedIsHandedOver()
        {
            // The server acts as custodian of an unclaimed container (its ZDO owner is 0), and only here
            // may ownership move to the requester.
            Assert.That(
                ChestOpenRules.Decide(ignored: false, isOwner: true, hasAccess: true, containerClaimed: false),
                Is.EqualTo(ChestOpenAction.HandOver));
        }

        [Test]
        public void AContainerAPlayerHasClaimedIsNotStolenFromThem()
        {
            // The regression this rule exists for: B opening A's chest moved ownership to B, and A's
            // crafting panel then counted the chest as zero (ChestTake.CanServeSynchronously).
            Assert.That(
                ChestOpenRules.Decide(ignored: false, isOwner: true, hasAccess: true, containerClaimed: true),
                Is.EqualTo(ChestOpenAction.GrantInUse));
        }

        [Test]
        public void TheInUseFlagNoLongerDecidesTheBranch()
        {
            // Custody is the ZDO owner, not the in-use flag: the owner of a claimed container keeps it
            // whether or not its UI happens to be open.
            Assert.That(
                ChestOpenRules.Decide(ignored: false, isOwner: true, hasAccess: true, containerClaimed: true),
                Is.EqualTo(ChestOpenAction.GrantInUse));

            Assert.That(
                ChestOpenRules.IsInUse(containerInUse: true, wagonInUse: false, requesterIsLocal: false), Is.True);
            Assert.That(
                ChestOpenRules.IsInUse(containerInUse: false, wagonInUse: false, requesterIsLocal: false), Is.False);
        }

        [Test]
        public void TheInUseTestIsUnchangedForTheDiagnosticLine()
        {
            Assert.That(ChestOpenRules.IsInUse(containerInUse: true, wagonInUse: false, requesterIsLocal: false), Is.True);
            Assert.That(ChestOpenRules.IsInUse(containerInUse: false, wagonInUse: true, requesterIsLocal: false), Is.True);
            Assert.That(ChestOpenRules.IsInUse(containerInUse: false, wagonInUse: false, requesterIsLocal: false), Is.False);
            Assert.That(ChestOpenRules.IsInUse(containerInUse: true, wagonInUse: true, requesterIsLocal: true), Is.False);
        }
    }
}
