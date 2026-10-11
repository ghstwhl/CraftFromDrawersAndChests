using System;
using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// Pins the pure decisions behind <c>DrawerSource.Take</c> (Task 9). These are the parts that
    /// can be checked without the game: how much a pull may remove (clamped to what the drawer holds,
    /// and nothing at all when the items would have nowhere to go — otherwise the pull would destroy
    /// them, the design's top anti-goal); how the amount is split into stack-sized chunks; and
    /// whether a chunk's delivery is guaranteed to be accepted, matched on the same predicate the
    /// delivery itself uses (name, quality and world level).
    /// <para>
    /// The game-facing materialisation around these decisions — resolving the prefab through
    /// <c>ObjectDB</c>, reading the destination's <c>Inventory</c> and calling
    /// <c>Inventory.AddItem(ItemData)</c> / <c>ForceRemove</c> — is not covered here; see the task
    /// report.
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

        // Delivery is chunked to at most one stack at a time. That is what lets the gate
        // (CanDeliver) be an exact prediction of the delivery: a chunk larger than a stack could no
        // longer be placed from matching free space plus a single empty slot. An amount larger than
        // one stack must therefore be split; these tests pin that chunk decision.

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

        // The gate that decides whether a chunk may be removed must agree with the delivery. The
        // game's Inventory.AddItem(ItemData) merges only into a stack whose name, quality AND world
        // level all match (FindFreeStackItem); Inventory.CanAddItem sums FindFreeStackSpace, which
        // ignores quality. Gating on CanAddItem therefore accepts a chunk AddItem then refuses — a
        // same-name partial stack of a different quality with no empty slot — removing items that
        // are then destroyed. These tests pin the quality/world-level aware gate that replaces it.

        private static DrawerTake.InventoryStack Stack(string name, int quality, int worldLevel, int stack, int maxStack)
        {
            return new DrawerTake.InventoryStack(name, quality, worldLevel, stack, maxStack);
        }

        [Test]
        public void MatchingFreeStackSpace_CountsOnlyNameQualityAndWorldLevelMatches()
        {
            var inventory = new[]
            {
                Stack("$item_wood", 1, 0, 5, 20),   // matches: 15 free
                Stack("$item_wood", 1, 0, 20, 20),  // matching but full: contributes nothing
                Stack("$item_wood", 2, 0, 5, 20),   // same name, different quality: ignored
                Stack("$item_wood", 1, 1, 5, 20),   // same name/quality, different world level: ignored
                Stack("$item_stone", 1, 0, 5, 20),  // different name: ignored
            };

            Assert.That(DrawerTake.MatchingFreeStackSpace("$item_wood", 1, 0, inventory), Is.EqualTo(15));
        }

        [Test]
        public void MatchingFreeStackSpace_NoInventoryOrNoName_IsZero()
        {
            Assert.That(DrawerTake.MatchingFreeStackSpace("$item_wood", 1, 0, null), Is.EqualTo(0));
            Assert.That(DrawerTake.MatchingFreeStackSpace(null, 1, 0, new DrawerTake.InventoryStack[0]), Is.EqualTo(0));
            Assert.That(DrawerTake.MatchingFreeStackSpace(string.Empty, 1, 0, new DrawerTake.InventoryStack[0]), Is.EqualTo(0));
        }

        [Test]
        public void MatchingFreeStackSpace_SumsAcrossEveryMatchingStack()
        {
            // Inventory.AddItem(ItemData) merges one item at a time, repeatedly taking the first
            // matching stack with room, so its usable free space is the SUM over every matching
            // stack, not just the largest one. This pins that summation (the crux of mirroring the
            // merge): a chunk of 18 fits the two partial stacks (15 + 3) even with no empty slot.
            var inventory = new[]
            {
                Stack("$item_wood", 1, 0, 5, 20),  // 15 free
                Stack("$item_wood", 1, 0, 17, 20), // 3 free
                Stack("$item_wood", 1, 0, 20, 20), // matching but full
                Stack("$item_stone", 1, 0, 1, 20), // other item
            };

            Assert.That(DrawerTake.MatchingFreeStackSpace("$item_wood", 1, 0, inventory), Is.EqualTo(18));
            Assert.That(DrawerTake.CanDeliver(18, 20, 18, 0), Is.True, "the whole sum is usable");
            Assert.That(DrawerTake.CanDeliver(19, 20, 18, 0), Is.False, "one over the sum, no empty slot");
        }

        // IsSameItem is the single item-identity predicate both halves of the availability question
        // match on: the delivery's free-space check (MatchingFreeStackSpace) and the held-stock count
        // that decides a pull's shortfall (MatchingAmount). Pinning it pins both.

        [Test]
        public void IsSameItem_MatchesOnlyNameQualityAndWorldLevel()
        {
            var stack = Stack("$item_wood", 1, 0, 5, 20);

            Assert.That(DrawerTake.IsSameItem(stack, "$item_wood", 1, 0), Is.True);

            Assert.That(DrawerTake.IsSameItem(stack, "$item_stone", 1, 0), Is.False, "different name");
            Assert.That(DrawerTake.IsSameItem(stack, "$item_wood", 2, 0), Is.False, "different quality");
            Assert.That(DrawerTake.IsSameItem(stack, "$item_wood", 1, 1), Is.False, "different world level");
        }

        [Test]
        public void IsSameItem_NoNameOrDifferentCase_MatchesNothing()
        {
            var stack = Stack("$item_wood", 1, 0, 5, 20);

            Assert.That(DrawerTake.IsSameItem(stack, null, 1, 0), Is.False);
            Assert.That(DrawerTake.IsSameItem(stack, string.Empty, 1, 0), Is.False);
            // Names are compared by ordinal, never by culture or case: this is a different item.
            Assert.That(DrawerTake.IsSameItem(stack, "$ITEM_WOOD", 1, 0), Is.False);
        }

        [Test]
        public void MatchingAmount_SumsOnlyNameQualityAndWorldLevelMatches()
        {
            var inventory = new[]
            {
                Stack("$item_wood", 1, 0, 5, 20),   // matches: 5
                Stack("$item_wood", 1, 0, 12, 20),  // matches: 12
                Stack("$item_wood", 1, 0, 20, 20),  // matching and full: still held
                Stack("$item_wood", 2, 0, 7, 20),   // same name, different quality: ignored
                Stack("$item_wood", 1, 1, 3, 20),   // same name/quality, different world level: ignored
                Stack("$item_stone", 1, 0, 9, 20),  // different name: ignored
            };

            Assert.That(DrawerTake.MatchingAmount("$item_wood", 1, 0, inventory), Is.EqualTo(37));
        }

        [Test]
        public void MatchingAmount_NoInventoryOrNoName_IsZero()
        {
            Assert.That(DrawerTake.MatchingAmount("$item_wood", 1, 0, null), Is.EqualTo(0));
            Assert.That(DrawerTake.MatchingAmount(null, 1, 0, new DrawerTake.InventoryStack[0]), Is.EqualTo(0));
            Assert.That(DrawerTake.MatchingAmount(string.Empty, 1, 0, new DrawerTake.InventoryStack[0]), Is.EqualTo(0));
        }

        [Test]
        public void MatchingAmount_IsTheMirrorOfMatchingFreeStackSpace()
        {
            // Same stacks, same predicate: a full matching stack is held (counts towards the shortfall)
            // yet offers no free space. A pull can therefore subtract the held amount and still prove
            // room for the remainder without a stack falling between the two rules.
            var inventory = new[]
            {
                Stack("$item_wood", 1, 0, 5, 20),
                Stack("$item_wood", 1, 0, 20, 20),
                Stack("$item_wood", 2, 0, 5, 20),
            };

            Assert.That(DrawerTake.MatchingAmount("$item_wood", 1, 0, inventory), Is.EqualTo(25));
            Assert.That(DrawerTake.MatchingFreeStackSpace("$item_wood", 1, 0, inventory), Is.EqualTo(15));
        }

        [Test]
        public void TheDefectCase_SameNameDifferentQualityWithNoEmptySlot_IsRefused()
        {
            // A single partial stack of the same item at a DIFFERENT quality, and no empty slot. The
            // old gate (CanAddItem -> FindFreeStackSpace, quality-agnostic) reports room here, but
            // AddItem cannot merge into the quality-2 stack and has no empty slot, so it would
            // refuse the chunk after the drawer had already given it up.
            var inventory = new[] { Stack("$item_wood", 2, 0, 5, 20) };

            int free = DrawerTake.MatchingFreeStackSpace("$item_wood", 1, 0, inventory);
            Assert.That(free, Is.EqualTo(0), "a different-quality stack is not free space for this delivery");

            Assert.That(DrawerTake.CanDeliver(1, 20, free, 0), Is.False, "no room -> the chunk is not removed");

            // A single empty slot makes the very same chunk deliverable.
            Assert.That(DrawerTake.CanDeliver(1, 20, free, 1), Is.True);
        }

        [Test]
        public void CanDeliver_FitsMatchingFreeSpaceOrAnEmptySlotButNeverMoreThanOneStack()
        {
            Assert.That(DrawerTake.CanDeliver(10, 20, 10, 0), Is.True);   // exactly the free space
            Assert.That(DrawerTake.CanDeliver(11, 20, 10, 0), Is.False);  // one over, no empty slot
            Assert.That(DrawerTake.CanDeliver(15, 20, 0, 1), Is.True);    // an empty slot holds a stack
            Assert.That(DrawerTake.CanDeliver(20, 20, 0, 1), Is.True);
            Assert.That(DrawerTake.CanDeliver(21, 20, 40, 5), Is.False);  // never rely on over-filling a stack
            Assert.That(DrawerTake.CanDeliver(0, 20, 40, 5), Is.False);
            Assert.That(DrawerTake.CanDeliver(5, 0, 40, 5), Is.False);    // unusable stack size
            Assert.That(DrawerTake.CanDeliver(5, 20, -1, 0), Is.False);   // negative space is not room
        }

        [Test]
        public void CanDeliver_AgreesWithAStackByStackAddItemSimulation()
        {
            var inventories = new[]
            {
                new[] { Stack("$item_wood", 1, 0, 19, 20) },
                new[] { Stack("$item_wood", 2, 0, 19, 20) },
                new[] { Stack("$item_wood", 1, 1, 19, 20) },
                new[] { Stack("$item_wood", 1, 0, 20, 20) },
                new[] { Stack("$item_wood", 1, 0, 5, 20), Stack("$item_wood", 2, 0, 5, 20) },
                // Two (or more) matching partial stacks: the merge can use both, so their free
                // space sums; a different-quality stack beside them is still ignored.
                new[] { Stack("$item_wood", 1, 0, 5, 20), Stack("$item_wood", 1, 0, 3, 20) },
                new[]
                {
                    Stack("$item_wood", 1, 0, 5, 20),
                    Stack("$item_wood", 1, 0, 3, 20),
                    Stack("$item_wood", 2, 0, 4, 20),
                    Stack("$item_stone", 1, 0, 1, 20),
                },
                new DrawerTake.InventoryStack[0],
            };

            const int maxStack = 20;
            foreach (var inventory in inventories)
            {
                int free = DrawerTake.MatchingFreeStackSpace("$item_wood", 1, 0, inventory);
                for (int emptySlots = 0; emptySlots <= 2; emptySlots++)
                {
                    // chunk is capped at one stack: Take never passes more (NextChunk).
                    for (int chunk = 1; chunk <= maxStack; chunk++)
                    {
                        bool expected = SimulateAddItem("$item_wood", 1, 0, chunk, maxStack, emptySlots, inventory);
                        Assert.That(
                            DrawerTake.CanDeliver(chunk, maxStack, free, emptySlots), Is.EqualTo(expected),
                            "chunk=" + chunk + " emptySlots=" + emptySlots);
                    }
                }
            }
        }

        // A small model of Inventory.AddItem(ItemData): place the chunk one item at a time into the
        // first matching (name + quality + world level) stack with room; for whatever is left, use a
        // single empty slot (the game writes the remainder into one slot). Returns false only when
        // neither is possible.
        private static bool SimulateAddItem(
            string name, int quality, int worldLevel, int chunk, int maxStack, int emptySlots,
            DrawerTake.InventoryStack[] inventory)
        {
            int remaining = chunk;
            foreach (var stack in inventory)
            {
                if (stack.Name != name || stack.Quality != quality || stack.WorldLevel != worldLevel)
                    continue;

                int space = stack.MaxStack - stack.Stack;
                if (space <= 0)
                    continue;

                remaining -= Math.Min(space, remaining);
                if (remaining == 0)
                    return true;
            }

            return remaining <= maxStack && emptySlots >= 1;
        }
    }
}
