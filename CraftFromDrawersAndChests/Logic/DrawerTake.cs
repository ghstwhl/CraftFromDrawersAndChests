using System;
using System.Collections.Generic;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure decisions behind the game-facing <c>Storage.DrawerSource.Take</c>: how much a pull
    /// may remove, how that amount is split into the stack-sized chunks a single
    /// <c>Inventory.AddItem(ItemData)</c> call can deliver, and whether the destination can
    /// actually accept a chunk. A pull must never destroy items — the design's top anti-goal — so
    /// nothing may be removed unless it also has somewhere to go, nothing beyond what the drawer
    /// actually holds may be removed, a request larger than one stack must be delivered in chunks,
    /// and a chunk may be removed only when <see cref="CanDeliver"/> — which mirrors the delivery's
    /// own matching predicate, quality included — says it will be accepted.
    /// Lives in <c>Logic/</c> so these decisions are unit-testable without a live drawer or
    /// inventory; the game-facing code resolves the prefab, feeds these decisions the destination's
    /// stacks, and performs the removal and delivery one chunk at a time.
    /// </summary>
    public static class DrawerTake
    {
        /// <summary>
        /// The number of items <c>Take</c> should remove: the smaller of <paramref name="requested"/>
        /// and <paramref name="available"/>, or zero when there is nothing to do. A missing
        /// destination (<paramref name="hasDestination"/> false) means the taken items would have
        /// nowhere to go, so nothing is removed — the alternative destroys them.
        /// </summary>
        /// <param name="requested">The maximum number the caller asked for.</param>
        /// <param name="available">How many of the prefab the drawer holds.</param>
        /// <param name="hasDestination">Whether a non-null destination inventory is available to deliver into.</param>
        public static int AmountToRemove(int requested, int available, bool hasDestination)
        {
            if (!hasDestination || requested <= 0 || available <= 0)
                return 0;

            return requested < available ? requested : available;
        }

        /// <summary>
        /// The size of the next delivery chunk while working towards <paramref name="target"/>
        /// items with <paramref name="deliveredSoFar"/> already delivered: never more than one stack
        /// (<paramref name="maxStackSize"/>) and never more than the amount still needed. This is
        /// the chunk-size and running-total decision the game-facing delivery loop drives.
        /// <para>
        /// Keeping every chunk to at most one stack is what makes <see cref="CanDeliver"/> an exact
        /// prediction of the delivery: a chunk larger than a stack could no longer be placed from
        /// matching free space plus a single empty slot, so none is ever removed.
        /// </para>
        /// </summary>
        /// <param name="target">How many items the pull intends to deliver in total.</param>
        /// <param name="deliveredSoFar">How many have already been delivered (the running total).</param>
        /// <param name="maxStackSize">The prefab's stack size, i.e. the largest chunk one delivery call can be trusted with.</param>
        /// <returns>The next chunk size, or 0 when the target is met or no chunk is deliverable.</returns>
        public static int NextChunk(int target, int deliveredSoFar, int maxStackSize)
        {
            if (maxStackSize <= 0)
                return 0;

            int remaining = target - deliveredSoFar;
            if (remaining <= 0)
                return 0;

            return remaining < maxStackSize ? remaining : maxStackSize;
        }

        /// <summary>
        /// One inventory stack reduced to the facts the delivery decision matches on. A game-free
        /// view so the matching predicate — the part that decides whether a delivery can succeed —
        /// is unit-testable without a live <c>Inventory</c>.
        /// </summary>
        public readonly struct InventoryStack
        {
            /// <summary>The stack's shared item name (e.g. <c>"$item_wood"</c>); may be null.</summary>
            public readonly string Name;

            /// <summary>The stack's item quality.</summary>
            public readonly int Quality;

            /// <summary>The stack's world level.</summary>
            public readonly int WorldLevel;

            /// <summary>How many items the stack currently holds.</summary>
            public readonly int Stack;

            /// <summary>The stack's maximum size.</summary>
            public readonly int MaxStack;

            public InventoryStack(string name, int quality, int worldLevel, int stack, int maxStack)
            {
                Name = name;
                Quality = quality;
                WorldLevel = worldLevel;
                Stack = stack;
                MaxStack = maxStack;
            }
        }

        /// <summary>
        /// Whether <paramref name="stack"/> <b>is</b> the item described by <paramref name="name"/>,
        /// <paramref name="quality"/> and <paramref name="worldLevel"/>: the one predicate this
        /// project matches an inventory stack on. Names are compared by ordinal, never by the
        /// localised string's culture, and quality <b>and</b> world level must both be equal — so a
        /// partial stack of the same name at a different quality is a different item.
        /// <para>
        /// Both halves of the availability question reuse it, so they cannot disagree about which
        /// stacks are the item: <see cref="MatchingFreeStackSpace"/> asks how much of it the
        /// destination can still accept, and <see cref="MatchingAmount"/> how much of it the inventory
        /// already holds. It is also the predicate <c>Inventory.AddItem(ItemData)</c> itself merges on
        /// (<c>FindFreeStackItem</c>), which is what makes those two answers exact rather than
        /// optimistic.
        /// </para>
        /// </summary>
        /// <param name="stack">The candidate stack; a default (null-name) stack matches nothing.</param>
        /// <param name="name">The item's shared name (e.g. <c>"$item_wood"</c>); null or empty matches nothing.</param>
        /// <param name="quality">The item's quality, which must be equal.</param>
        /// <param name="worldLevel">The item's world level, which must be equal.</param>
        public static bool IsSameItem(InventoryStack stack, string name, int quality, int worldLevel)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!string.Equals(stack.Name, name, StringComparison.Ordinal))
                return false;

            return stack.Quality == quality && stack.WorldLevel == worldLevel;
        }

        /// <summary>
        /// The free stack space a delivery of <paramref name="name"/> at
        /// <paramref name="quality"/><paramref name="worldLevel"/> can use, given the destination's
        /// existing <paramref name="stacks"/>. This mirrors the predicate
        /// <c>Inventory.AddItem(ItemData)</c> actually merges on (<c>FindFreeStackItem</c>): a stack
        /// counts only when <see cref="IsSameItem"/> — name, <b>quality</b> and world level all equal —
        /// and it is not already full. It deliberately does <b>not</b> mirror
        /// <c>Inventory.CanAddItem</c>, whose <c>FindFreeStackSpace</c> ignores quality — the gap that
        /// let a chunk be removed and then refused (and destroyed) by <c>AddItem</c>.
        /// </summary>
        public static int MatchingFreeStackSpace(
            string name, int quality, int worldLevel, IEnumerable<InventoryStack> stacks)
        {
            if (stacks == null || string.IsNullOrEmpty(name))
                return 0;

            int space = 0;
            foreach (InventoryStack stack in stacks)
            {
                if (!IsSameItem(stack, name, quality, worldLevel))
                    continue;
                if (stack.Stack >= stack.MaxStack)
                    continue;

                space += stack.MaxStack - stack.Stack;
            }
            return space;
        }

        /// <summary>
        /// How many items the inventory already holds that <b>are</b> the item described by
        /// <paramref name="name"/>, <paramref name="quality"/> and <paramref name="worldLevel"/>:
        /// the sum of every matching stack's contents, counted with the very predicate
        /// <see cref="MatchingFreeStackSpace"/> uses (<see cref="IsSameItem"/>).
        /// <para>
        /// This is the source half of the availability question — what the player already has — and
        /// its mirror is the destination half. Because both match on the same three-part identity, a
        /// pull can subtract the held amount from a requirement and then prove room for exactly the
        /// remainder, without either side counting a stack the other would not.
        /// </para>
        /// </summary>
        /// <param name="name">The item's shared name; null or empty counts nothing.</param>
        /// <param name="quality">The item's quality, which must be equal to count.</param>
        /// <param name="worldLevel">The item's world level, which must be equal to count.</param>
        /// <param name="stacks">The inventory's stacks; null counts nothing.</param>
        public static int MatchingAmount(string name, int quality, int worldLevel, IEnumerable<InventoryStack> stacks)
        {
            if (stacks == null || string.IsNullOrEmpty(name))
                return 0;

            int held = 0;
            foreach (InventoryStack stack in stacks)
            {
                if (!IsSameItem(stack, name, quality, worldLevel))
                    continue;

                held += stack.Stack;
            }
            return held;
        }

        /// <summary>
        /// Whether a single <c>Inventory.AddItem(ItemData)</c> call is guaranteed to place
        /// <paramref name="chunk"/> items, given <paramref name="matchingFreeStackSpace"/> (see
        /// <see cref="MatchingFreeStackSpace"/>) and <paramref name="emptySlots"/> empty destination
        /// slots. <c>AddItem</c> merges into matching stacks and, for whatever is left, uses one
        /// empty slot, so for a chunk of at most one stack (<paramref name="maxStackSize"/>) it
        /// succeeds exactly when the chunk fits the matching free space plus the empty slots. A
        /// larger chunk is rejected: <c>Take</c> delivers in stack-sized chunks, and handing
        /// <c>AddItem</c> more than a stack would make it over-fill its empty slot beyond
        /// <paramref name="maxStackSize"/>. This is the gate <c>Take</c> applies <b>before</b>
        /// removing a chunk, so a chunk is removed only once its delivery is known to be accepted.
        /// </summary>
        public static bool CanDeliver(int chunk, int maxStackSize, int matchingFreeStackSpace, int emptySlots)
        {
            if (chunk <= 0 || maxStackSize <= 0 || chunk > maxStackSize)
                return false;

            long free = matchingFreeStackSpace > 0 ? matchingFreeStackSpace : 0;
            long slots = emptySlots > 0 ? emptySlots : 0;
            long capacity = free + slots * maxStackSize;
            return chunk <= capacity;
        }
    }
}
