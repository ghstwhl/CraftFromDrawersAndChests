using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// Pins the pure "how much may this drawer pull remove" decision (Task 9, Ruling 20). This is
    /// the part of <c>DrawerSource.Take</c> that can be checked without the game: the amount to
    /// remove is clamped to what the drawer holds, and nothing may be removed when the items would
    /// have nowhere to go — otherwise the pull would destroy them, the design's top anti-goal.
    /// <para>
    /// The delivery itself (resolving the prefab through <c>ObjectDB</c> and adding it to the
    /// player's <c>Inventory</c>) is game-facing and is not covered here; see the task report.
    /// </para>
    /// </summary>
    [TestFixture]
    public class DrawerTakeTests
    {
        [Test]
        public void DestinationPresent_RemovesTheSmallerOfRequestAndStock()
        {
            Assert.That(DrawerTake.AmountToRemove(2, 5, true), Is.EqualTo(2));
            Assert.That(DrawerTake.AmountToRemove(5, 2, true), Is.EqualTo(2));
            Assert.That(DrawerTake.AmountToRemove(5, 5, true), Is.EqualTo(5));
        }

        [Test]
        public void NoDestination_RemovesNothing()
        {
            // The items would have nowhere to go, so removing them would destroy them.
            Assert.That(DrawerTake.AmountToRemove(5, 5, false), Is.EqualTo(0));
        }

        [Test]
        public void ZeroOrNegativeRequest_RemovesNothing()
        {
            Assert.That(DrawerTake.AmountToRemove(0, 5, true), Is.EqualTo(0));
            Assert.That(DrawerTake.AmountToRemove(-3, 5, true), Is.EqualTo(0));
        }

        [Test]
        public void EmptyDrawer_RemovesNothing()
        {
            Assert.That(DrawerTake.AmountToRemove(5, 0, true), Is.EqualTo(0));
            Assert.That(DrawerTake.AmountToRemove(5, -3, true), Is.EqualTo(0));
        }
    }
}
