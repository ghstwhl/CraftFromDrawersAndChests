using System.Collections.Generic;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The destination half of an availability gate (Task 13 fix round 3, R7): whether the player's own
    /// inventory can actually <b>accept</b> everything a pull would deliver into it.
    /// <para>
    /// A gate that only asks "does storage hold the shortfall?" can answer yes for an action the pull
    /// then cannot deliver, and — because vanilla creates the crafted item or places the piece
    /// <i>before</i> it consumes the materials — a pull that fails leaves the player with a free item.
    /// The pull delivers into the player's inventory, so the gate must also prove that inventory has
    /// room: matching free stack space plus empty slots, per item, matching quality and world level.
    /// This is the same question <c>Storage.DrawerSource.Take</c> already answers before each of its
    /// chunks, and this class reuses the very same pure decisions — <see cref="DrawerTake.NextChunk"/>
    /// to size the chunk, <see cref="DrawerTake.CanDeliver"/> to decide whether the game will place it,
    /// and <see cref="DrawerTake.MatchingFreeStackSpace"/> to measure the matching room — so the chest,
    /// crafting and building paths agree with the drawer path instead of diverging from it.
    /// </para>
    /// <para>
    /// The deliveries are modelled exactly as the game's <c>Inventory.AddItem(ItemData)</c> places them,
    /// one stack at a time: a chunk merges into matching (name, quality, world level) stacks with room
    /// first, and only the part that does not fit there is written into a single empty slot — whose own
    /// remaining room then joins the matching free space for the next chunk. This is why the model is
    /// not a plain "shortfall &lt;= matching space + slots * stack": the chunking and the re-use of a
    /// slot's leftover both matter.
    /// </para>
    /// <para>
    /// Lives in <c>Logic/</c> so it is unit-testable without a live <c>Inventory</c>; the game-facing
    /// caller reduces the destination to <see cref="DrawerTake.InventoryStack"/>s, counts its empty
    /// slots, and describes each delivery as a <see cref="Delivery"/>.
    /// </para>
    /// </summary>
    public static class DestinationCapacity
    {
        /// <summary>
        /// One thing a pull (or the craft's own output) will place into the destination: the item's
        /// <b>shared</b> name (what <c>Inventory.AddItem</c> merges on — see
        /// <see cref="DrawerTake.MatchingFreeStackSpace"/>), its quality and world level, how many are
        /// to be placed, and the prefab's stack size. Pure logic, so it must never reference
        /// UnityEngine, Valheim, BepInEx or Jotunn.
        /// </summary>
        public readonly struct Delivery
        {
            /// <summary>The shared item name (e.g. <c>"$item_wood"</c>), as the merge predicate matches on.</summary>
            public readonly string Name;

            /// <summary>The delivered items' quality.</summary>
            public readonly int Quality;

            /// <summary>The delivered items' world level.</summary>
            public readonly int WorldLevel;

            /// <summary>How many of the item are to be placed.</summary>
            public readonly int Amount;

            /// <summary>The prefab's stack size, i.e. the largest chunk a single delivery call can be trusted with.</summary>
            public readonly int MaxStack;

            public Delivery(string name, int quality, int worldLevel, int amount, int maxStack)
            {
                Name = name;
                Quality = quality;
                WorldLevel = worldLevel;
                Amount = amount;
                MaxStack = maxStack;
            }
        }

        /// <summary>
        /// Whether the destination described by <paramref name="stacks"/> (its live stacks) and
        /// <paramref name="emptySlots"/> can accept every <paramref name="deliveries"/> entry.
        /// <para>
        /// The deliveries are applied to one shared model of the destination, so the free space and the
        /// empty slots are charged exactly once across all of them: two items cannot both count the
        /// same empty slot. They are taken in the order given, which is the order the game places them
        /// (on the crafting path the crafted output goes in before the pull runs and is listed first);
        /// the verdict is the same whichever order they are taken in, because each item can reuse only
        /// its own stacks, but listing them in game order keeps the intent explicit.
        /// </para>
        /// <para>
        /// A delivery whose stack size is not positive can never be placed, so the answer is false; an
        /// amount of zero or less asks for nothing and is skipped. A null <paramref name="stacks"/> is
        /// treated as "no stacks", and a negative <paramref name="emptySlots"/> as none.
        /// </para>
        /// </summary>
        /// <param name="stacks">The destination's current stacks, as the pure merge view; may be null.</param>
        /// <param name="emptySlots">How many empty destination slots there are; may be zero.</param>
        /// <param name="deliveries">The deliveries to place, in game order; null or empty means nothing to place.</param>
        public static bool CanAccept(
            IReadOnlyList<DrawerTake.InventoryStack> stacks,
            int emptySlots,
            IReadOnlyList<Delivery> deliveries)
        {
            if (deliveries == null || deliveries.Count == 0)
                return true;

            // A mutable copy of the destination's stacks, so each delivery consumes free space the
            // ones before it left, exactly as the game's AddItem does across calls.
            var model = new List<DrawerTake.InventoryStack>();
            if (stacks != null)
            {
                for (int i = 0; i < stacks.Count; i++)
                    model.Add(stacks[i]);
            }
            int slots = emptySlots > 0 ? emptySlots : 0;

            for (int d = 0; d < deliveries.Count; d++)
            {
                Delivery delivery = deliveries[d];
                if (delivery.Amount <= 0)
                    continue;

                // A non-positive stack size can never place a positive amount (NextChunk would return 0
                // and the amount would silently go unmet), so refuse rather than answer optimistically.
                if (delivery.MaxStack <= 0)
                    return false;

                int delivered = 0;
                int chunk;
                while ((chunk = DrawerTake.NextChunk(delivery.Amount, delivered, delivery.MaxStack)) > 0)
                {
                    // The very gate the drawer path applies before removing a chunk, fed by the very
                    // matching predicate AddItem merges on.
                    int free = DrawerTake.MatchingFreeStackSpace(
                        delivery.Name, delivery.Quality, delivery.WorldLevel, model);
                    if (!DrawerTake.CanDeliver(chunk, delivery.MaxStack, free, slots))
                        return false;

                    PlaceChunk(model, ref slots, delivery, chunk);
                    delivered += chunk;
                }
            }
            return true;
        }

        /// <summary>
        /// Applies one already-approved chunk to the destination model the way <c>Inventory.AddItem</c>
        /// does: merge into the matching (name, quality, world level) stacks with room first, then write
        /// only the leftover into one empty slot — whose remaining room becomes matching free space for
        /// the next chunk. <paramref name="slots"/> is decremented for the slot the leftover occupies.
        /// The caller has already proved with <see cref="DrawerTake.CanDeliver"/> that the chunk fits, so
        /// a single slot always suffices for the leftover.
        /// </summary>
        private static void PlaceChunk(
            List<DrawerTake.InventoryStack> model, ref int slots, Delivery delivery, int chunk)
        {
            int remaining = chunk;
            for (int i = 0; i < model.Count && remaining > 0; i++)
            {
                DrawerTake.InventoryStack stack = model[i];
                if (!Matches(stack, delivery))
                    continue;

                int room = stack.MaxStack - stack.Stack;
                if (room <= 0)
                    continue;

                int take = room < remaining ? room : remaining;
                model[i] = new DrawerTake.InventoryStack(
                    stack.Name, stack.Quality, stack.WorldLevel, stack.Stack + take, stack.MaxStack);
                remaining -= take;
            }

            if (remaining > 0)
            {
                slots--;
                model.Add(new DrawerTake.InventoryStack(
                    delivery.Name, delivery.Quality, delivery.WorldLevel, remaining, delivery.MaxStack));
            }
        }

        /// <summary>
        /// Whether a destination stack is free space for a delivery: the exact predicate
        /// <see cref="DrawerTake.MatchingFreeStackSpace"/> matches on (name by ordinal, quality and
        /// world level all equal), so a partial stack of the same name at a different quality is not
        /// counted as room for this delivery.
        /// </summary>
        private static bool Matches(DrawerTake.InventoryStack stack, Delivery delivery)
        {
            if (!string.Equals(stack.Name, delivery.Name, System.StringComparison.Ordinal))
                return false;

            return stack.Quality == delivery.Quality && stack.WorldLevel == delivery.WorldLevel;
        }
    }
}
