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

        // Inventory.AddItem(GameObject, int) places AT MOST ONE stack: it clones the prefab's item
        // data, sets m_stack = Min(amount, m_maxStackSize) and adds a single ItemData. A request
        // larger than one stack must therefore be delivered in stack-sized chunks, or every item
        // beyond the first stack is silently destroyed. These tests pin that chunk decision.

        [Test]
        public void NextChunk_RequestExceedingOneStack_YieldsOneStackAtATime()
        {
            // The case that failed: 70 requested, 20 per stack. The first chunk is one stack (20),
            // not the whole 70.
            Assert.That(DrawerTake.NextChunk(70, 0, 20), Is.EqualTo(20));
            Assert.That(DrawerTake.NextChunk(70, 20, 20), Is.EqualTo(20));
            Assert.That(DrawerTake.NextChunk(70, 60, 20), Is.EqualTo(10));
            Assert.That(DrawerTake.NextChunk(70, 70, 20), Is.EqualTo(0));
        }

        [Test]
        public void NextChunk_NeverExceedsOneStackOrTheRemainder()
        {
            Assert.That(DrawerTake.NextChunk(10, 0, 20), Is.EqualTo(10)); // fits one stack
            Assert.That(DrawerTake.NextChunk(20, 0, 20), Is.EqualTo(20)); // exactly one stack
            Assert.That(DrawerTake.NextChunk(21, 0, 20), Is.EqualTo(20)); // one over a stack
            Assert.That(DrawerTake.NextChunk(1, 0, 1), Is.EqualTo(1));    // non-stackable item
        }

        [Test]
        public void NextChunk_NothingLeftOrNothingDeliverable_ReturnsZero()
        {
            Assert.That(DrawerTake.NextChunk(0, 0, 20), Is.EqualTo(0));
            Assert.That(DrawerTake.NextChunk(5, 5, 20), Is.EqualTo(0)); // already delivered
            Assert.That(DrawerTake.NextChunk(5, 10, 20), Is.EqualTo(0)); // over-delivered
            Assert.That(DrawerTake.NextChunk(5, 0, 0), Is.EqualTo(0));   // unusable stack size
            Assert.That(DrawerTake.NextChunk(5, 0, -1), Is.EqualTo(0));
        }

        [Test]
        public void NextChunk_DrivingTheDeliveryLoop_ReachesTheWholeRequestInStacks()
        {
            // Simulates the game-facing loop: keep asking for the next chunk until it returns 0.
            // The running total must reach the request exactly, in pieces no larger than one stack.
            const int target = 70;
            const int stack = 20;
            var chunks = new System.Collections.Generic.List<int>();
            int delivered = 0;
            int chunk;
            while ((chunk = DrawerTake.NextChunk(target, delivered, stack)) > 0)
            {
                Assert.That(chunk, Is.LessThanOrEqualTo(stack));
                chunks.Add(chunk);
                delivered += chunk;
                Assert.That(delivered, Is.LessThanOrEqualTo(target));
            }

            Assert.That(chunks, Is.EqualTo(new[] { 20, 20, 20, 10 }));
            Assert.That(delivered, Is.EqualTo(target));
        }

        [Test]
        public void AmountToRemove_ThenChunkedStillDeliversEverythingTaken()
        {
            // Request 70 from a drawer holding 50, with a 20-per-stack item: take 50, deliver it as
            // 20 + 20 + 10 so removed == delivered.
            int take = DrawerTake.AmountToRemove(70, 50, true);
            Assert.That(take, Is.EqualTo(50));

            int delivered = 0;
            int chunk;
            while ((chunk = DrawerTake.NextChunk(take, delivered, 20)) > 0)
                delivered += chunk;

            Assert.That(delivered, Is.EqualTo(take));
        }
    }
}
