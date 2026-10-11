using System.Collections.Generic;
using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// Pins the destination half of the availability gates (Task 13 fix round 3, R7): whether the
    /// player's own inventory can accept what a pull would deliver into it. The defect this closes is
    /// the mirror of the original Critical — a gate that says yes for a pull the destination then
    /// cannot take, which (because vanilla creates the item before consuming) hands out a free craft.
    /// <para>
    /// The decisive case is an inventory with no empty slot and no matching stack for the item: the
    /// shortfall simply cannot be placed, so the gate must decline. The tests also cover the subtler
    /// shapes — a chunk spilling into a slot whose leftover the next chunk reuses, and several
    /// deliveries sharing the same slots — and compare the decision against a faithful model of the
    /// game's <c>Inventory.AddItem(ItemData)</c>.
    /// </para>
    /// </summary>
    [TestFixture]
    public class DestinationCapacityTests
    {
        private static DrawerTake.InventoryStack Stack(string name, int quality, int worldLevel, int stack, int maxStack)
        {
            return new DrawerTake.InventoryStack(name, quality, worldLevel, stack, maxStack);
        }

        private static DestinationCapacity.Delivery Del(string name, int amount, int maxStack)
        {
            return new DestinationCapacity.Delivery(name, 1, 0, amount, maxStack);
        }

        [Test]
        public void NothingToPlace_Succeeds()
        {
            Assert.That(DestinationCapacity.CanAccept(null, 0, null), Is.True);
            Assert.That(
                DestinationCapacity.CanAccept(new DrawerTake.InventoryStack[0], 0, new DestinationCapacity.Delivery[0]),
                Is.True);
        }

        [Test]
        public void FullInventoryWithNoMatchingStack_CannotAcceptTheShortfall()
        {
            // Critical #2's shape: every slot is full and none holds the item, so the pulled shortfall
            // has nowhere to go. The gate must decline, or the craft is free (vanilla already created
            // the output before the pull runs).
            var inventory = new[] { Stack("$item_stone", 1, 0, 20, 20) };
            var deliveries = new[] { Del("$item_wood", 20, 20) };

            Assert.That(DestinationCapacity.CanAccept(inventory, 0, deliveries), Is.False);
        }

        [Test]
        public void AnEmptySlotTakesOneStackButNotMore()
        {
            var empty = new DrawerTake.InventoryStack[0];

            Assert.That(DestinationCapacity.CanAccept(empty, 1, new[] { Del("$item_wood", 20, 20) }), Is.True);
            // One item over the slot's stack size: the second chunk has no matching space and no slot.
            Assert.That(DestinationCapacity.CanAccept(empty, 1, new[] { Del("$item_wood", 21, 20) }), Is.False);
        }

        [Test]
        public void MatchingFreeSpaceIsSpendableUpToItsLimit()
        {
            // A partial stack of exactly the delivered item (same name, quality and world level).
            var inventory = new[] { Stack("$item_wood", 1, 0, 5, 20) }; // 15 free

            Assert.That(DestinationCapacity.CanAccept(inventory, 0, new[] { Del("$item_wood", 15, 20) }), Is.True);
            Assert.That(DestinationCapacity.CanAccept(inventory, 0, new[] { Del("$item_wood", 16, 20) }), Is.False);
        }

        [Test]
        public void AChunkThatSpillsFillsASlot_WhoseRoomTheNextChunkReuses()
        {
            // 15 matching free, 20 per stack, one empty slot: the first chunk fills the partial stack
            // and spills 5 into the slot, and the slot's remaining 15 room is then usable by the next
            // chunk — so exactly one stack-and-a-half (35) fits, and not 36.
            var inventory = new[] { Stack("$item_wood", 1, 0, 5, 20) };

            Assert.That(DestinationCapacity.CanAccept(inventory, 1, new[] { Del("$item_wood", 35, 20) }), Is.True);
            Assert.That(DestinationCapacity.CanAccept(inventory, 1, new[] { Del("$item_wood", 36, 20) }), Is.False);
        }

        [Test]
        public void DeliveriesShareTheDestinationsSlots()
        {
            var empty = new DrawerTake.InventoryStack[0];
            var two = new[]
            {
                new DestinationCapacity.Delivery("$item_wood", 1, 0, 15, 20),
                new DestinationCapacity.Delivery("$item_stone", 1, 0, 15, 20),
            };

            // Each item fits one empty slot, but a slot holds only one item, so one slot is not enough
            // for both even though either alone would fit.
            Assert.That(DestinationCapacity.CanAccept(empty, 1, two), Is.False);
            Assert.That(DestinationCapacity.CanAccept(empty, 2, two), Is.True);
        }

        [Test]
        public void MatchingRequiresQualityAndWorldLevel()
        {
            // Inventory.AddItem merges only on name AND quality AND world level (FindFreeStackItem);
            // a same-name stack at a different quality is not free space for this delivery.
            var inventory = new[] { Stack("$item_wood", 2, 0, 5, 20) };
            var deliveries = new[] { new DestinationCapacity.Delivery("$item_wood", 1, 0, 1, 20) };

            Assert.That(DestinationCapacity.CanAccept(inventory, 0, deliveries), Is.False);
            Assert.That(DestinationCapacity.CanAccept(inventory, 1, deliveries), Is.True);
        }

        [Test]
        public void DeliveryWithNoStackSize_CannotAccept()
        {
            var deliveries = new[] { Del("$item_wood", 1, 0) };
            Assert.That(DestinationCapacity.CanAccept(new DrawerTake.InventoryStack[0], 5, deliveries), Is.False);
        }

        [Test]
        public void NonPositiveAmountsAreIgnored()
        {
            var inventories = new[]
            {
                new DrawerTake.InventoryStack[0],
                new[] { Stack("$item_stone", 1, 0, 20, 20) },
            };
            var deliveries = new[] { Del("$item_wood", 0, 20), Del("$item_wood", -3, 20) };

            foreach (var inventory in inventories)
                Assert.That(DestinationCapacity.CanAccept(inventory, 0, deliveries), Is.True);
        }

        [Test]
        public void AgreesWithAStackByStackAddItemSimulation()
        {
            var inventories = new[]
            {
                new DrawerTake.InventoryStack[0],
                new[] { Stack("$item_wood", 1, 0, 5, 20) },
                new[] { Stack("$item_wood", 1, 0, 20, 20) },
                new[] { Stack("$item_wood", 2, 0, 5, 20) },
                new[] { Stack("$item_wood", 1, 0, 5, 20), Stack("$item_wood", 1, 0, 3, 20), Stack("$item_stone", 1, 0, 10, 20) },
            };

            var deliverySets = new[]
            {
                new[] { Del("$item_wood", 15, 20) },
                new[] { Del("$item_wood", 35, 20) },
                new[] { Del("$item_wood", 41, 20) },
                new[] { new DestinationCapacity.Delivery("$item_wood", 1, 0, 15, 20), new DestinationCapacity.Delivery("$item_stone", 1, 0, 15, 20) },
            };

            foreach (var inventory in inventories)
            {
                for (int emptySlots = 0; emptySlots <= 2; emptySlots++)
                {
                    foreach (var deliveries in deliverySets)
                    {
                        bool expected = SimulateInventory(inventory, emptySlots, deliveries);
                        Assert.That(
                            DestinationCapacity.CanAccept(inventory, emptySlots, deliveries), Is.EqualTo(expected),
                            "emptySlots=" + emptySlots + " deliveries=" + deliveries.Length);
                    }
                }
            }
        }

        // A faithful model of Inventory.AddItem(ItemData) driven chunk by chunk: merge one item at a
        // time into the first matching (name + quality + world level) stack with room, and when none is
        // found write the whole remainder into ONE empty slot that then joins the matching stacks.
        private sealed class SimStack
        {
            public string Name;
            public int Quality;
            public int WorldLevel;
            public int Count;
            public int MaxStack;
        }

        private static bool SimulateAddItem(
            List<SimStack> stacks, int[] slots, string name, int quality, int worldLevel, int chunk, int maxStack)
        {
            int remaining = chunk;
            while (remaining > 0)
            {
                SimStack target = null;
                for (int i = 0; i < stacks.Count; i++)
                {
                    SimStack s = stacks[i];
                    if (s.Name != name || s.Quality != quality || s.WorldLevel != worldLevel)
                        continue;
                    if (s.Count >= s.MaxStack)
                        continue;

                    target = s;
                    break;
                }

                if (target == null)
                    break;

                target.Count++;
                remaining--;
            }

            if (remaining == 0)
                return true;

            if (slots[0] <= 0)
                return false;

            slots[0]--;
            stacks.Add(new SimStack
            {
                Name = name,
                Quality = quality,
                WorldLevel = worldLevel,
                Count = remaining,
                MaxStack = maxStack,
            });
            return true;
        }

        private static bool SimulateInventory(
            DrawerTake.InventoryStack[] initial, int emptySlots, DestinationCapacity.Delivery[] deliveries)
        {
            var stacks = new List<SimStack>();
            if (initial != null)
            {
                foreach (var s in initial)
                {
                    stacks.Add(new SimStack
                    {
                        Name = s.Name,
                        Quality = s.Quality,
                        WorldLevel = s.WorldLevel,
                        Count = s.Stack,
                        MaxStack = s.MaxStack,
                    });
                }
            }

            int[] slots = { emptySlots < 0 ? 0 : emptySlots };

            if (deliveries != null)
            {
                foreach (var d in deliveries)
                {
                    if (d.Amount <= 0)
                        continue;
                    if (d.MaxStack <= 0)
                        return false;

                    int delivered = 0;
                    int chunk;
                    while ((chunk = DrawerTake.NextChunk(d.Amount, delivered, d.MaxStack)) > 0)
                    {
                        if (!SimulateAddItem(stacks, slots, d.Name, d.Quality, d.WorldLevel, chunk, d.MaxStack))
                            return false;
                        delivered += chunk;
                    }
                }
            }
            return true;
        }
    }
}
