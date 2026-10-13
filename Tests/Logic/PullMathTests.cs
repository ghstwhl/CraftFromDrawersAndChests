using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The arithmetic the pulls add on top of the planner: how much of a requirement storage is asked
    /// for. Two bases, because the mod pulls for two different reasons — a craft or a placement asks
    /// for the <b>shortfall</b> below what the player carries, while a deliberate fetch (the
    /// ctrl-click pull, the pull-key build press) asks for the <b>full cost</b> however much the player
    /// carries, which is what makes the same fetch repeatable. The rest of the mapping — turning a
    /// <c>Piece.Requirement</c> into a request — needs game types and cannot run here.
    /// </summary>
    [TestFixture]
    public class PullMathTests
    {
        [Test]
        public void Shortfall_PlayerHoldsNone_IsTheWholeRequirement()
        {
            Assert.That(PullMath.Shortfall(10, 0), Is.EqualTo(10));
        }

        [Test]
        public void Shortfall_PlayerHoldsSome_IsTheRemainder()
        {
            Assert.That(PullMath.Shortfall(10, 3), Is.EqualTo(7));
        }

        [Test]
        public void Shortfall_PlayerHoldsExactlyEnough_IsZero()
        {
            Assert.That(PullMath.Shortfall(10, 10), Is.EqualTo(0));
        }

        [Test]
        public void Shortfall_PlayerHoldsMoreThanEnough_IsZero()
        {
            // Never negative: a negative "request" is a subtraction where an amount is expected, and
            // the planner would only happen to treat it as satisfied.
            Assert.That(PullMath.Shortfall(10, 25), Is.EqualTo(0));
        }

        [Test]
        public void Shortfall_NothingRequired_IsZero()
        {
            Assert.That(PullMath.Shortfall(0, 0), Is.EqualTo(0));
        }

        // ---- Full cost: the deliberate fetch (the ctrl-click pull and the pull-key build press) ----

        [Test]
        public void RequestAmount_FullCost_IgnoresWhatThePlayerHolds()
        {
            // The reported defect: a second ctrl-click for the same recipe asked for nothing, because
            // the first pull had already supplied the whole cost. A fetch asks for the cost again.
            Assert.That(PullMath.RequestAmount(2, 2, RequestBasis.FullCost), Is.EqualTo(2));
        }

        [Test]
        public void RequestAmount_FullCost_HoldingMoreThanTheCost_IsStillTheWholeCost()
        {
            Assert.That(PullMath.RequestAmount(2, 10, RequestBasis.FullCost), Is.EqualTo(2));
        }

        [Test]
        public void RequestAmount_FullCost_HoldingNone_IsTheWholeCost()
        {
            Assert.That(PullMath.RequestAmount(2, 0, RequestBasis.FullCost), Is.EqualTo(2));
        }

        // ---- Shortfall: the crafting and placement pull (unchanged) ----

        [Test]
        public void RequestAmount_Shortfall_IsTheShortfall()
        {
            Assert.That(
                PullMath.RequestAmount(10, 3, RequestBasis.Shortfall),
                Is.EqualTo(PullMath.Shortfall(10, 3)));
        }

        [Test]
        public void RequestAmount_Shortfall_HoldingTheWholeCost_AsksForNothing()
        {
            // Pinned deliberately: this is the behaviour a fetch must NOT have, and the reason the two
            // bases exist side by side rather than one replacing the other.
            Assert.That(PullMath.RequestAmount(2, 2, RequestBasis.Shortfall), Is.EqualTo(0));
        }

        // ---- Edges shared by both bases ----

        [Test]
        public void RequestAmount_NothingRequired_IsZero()
        {
            Assert.That(PullMath.RequestAmount(0, 0, RequestBasis.FullCost), Is.EqualTo(0));
            Assert.That(PullMath.RequestAmount(0, 0, RequestBasis.Shortfall), Is.EqualTo(0));
        }

        [Test]
        public void RequestAmount_NegativeCost_IsZero()
        {
            // A request is an amount: a negative one would read as a subtraction (see Shortfall).
            Assert.That(PullMath.RequestAmount(-1, 0, RequestBasis.FullCost), Is.EqualTo(0));
            Assert.That(PullMath.RequestAmount(-1, 0, RequestBasis.Shortfall), Is.EqualTo(0));
        }
    }
}
