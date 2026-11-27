using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The payment decision behind "both players can craft from the same chest": a cost that has to be
    /// fetched from a chest another player owns cannot be paid inside the same call, so the craft is held
    /// until the items land rather than run and leave part of the cost unpaid.
    /// </summary>
    [TestFixture]
    public class CraftPaymentRulesTests
    {
        [Test]
        public void ACostThatCanBeTakenNowProceeds()
        {
            Assert.That(
                CraftPaymentRules.Decide(planFeasible: true, destinationAllows: true, linesNeedingFetch: 0),
                Is.EqualTo(CraftPaymentAction.Proceed));
        }

        [Test]
        public void ACostThatMustComeFromAnotherPlayersChestIsFetchedAndHeld()
        {
            Assert.That(
                CraftPaymentRules.Decide(planFeasible: true, destinationAllows: true, linesNeedingFetch: 1),
                Is.EqualTo(CraftPaymentAction.FetchAndHold));
        }

        [Test]
        public void NothingInRangeMeansRefuse()
        {
            Assert.That(
                CraftPaymentRules.Decide(planFeasible: false, destinationAllows: true, linesNeedingFetch: 0),
                Is.EqualTo(CraftPaymentAction.Refuse));
        }

        [Test]
        public void AnInventoryThatCannotTakeTheDeliveryMeansRefuse()
        {
            // The pull delivers into the player's inventory before vanilla consumes, so a destination that
            // cannot take it would leave the craft unpaid.
            Assert.That(
                CraftPaymentRules.Decide(planFeasible: true, destinationAllows: false, linesNeedingFetch: 2),
                Is.EqualTo(CraftPaymentAction.Refuse));
        }

        [Test]
        public void AHeldCraftWaitsWhileTheItemsAreInFlight()
        {
            Assert.That(
                CraftPaymentRules.EvaluatePending(playerHoldsEverything: false, secondsWaited: 0.1f),
                Is.EqualTo(PendingCraftOutcome.Wait));
        }

        [Test]
        public void AHeldCraftRunsOnceTheItemsHaveArrived()
        {
            Assert.That(
                CraftPaymentRules.EvaluatePending(playerHoldsEverything: true, secondsWaited: 0.1f),
                Is.EqualTo(PendingCraftOutcome.Complete));
        }

        [Test]
        public void AHeldCraftIsAbandonedRatherThanHeldForever()
        {
            Assert.That(
                CraftPaymentRules.EvaluatePending(
                    playerHoldsEverything: false, secondsWaited: CraftPaymentRules.PendingTimeoutSeconds),
                Is.EqualTo(PendingCraftOutcome.Abandon));
        }
    }
}
