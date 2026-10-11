using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The one piece of arithmetic the crafting/building pull adds on top of the planner: how much of
    /// a requirement storage must still supply once the player's own inventory is counted. The rest
    /// of the mapping — turning a <c>Piece.Requirement</c> into a request — needs game types and
    /// cannot run here.
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
    }
}
