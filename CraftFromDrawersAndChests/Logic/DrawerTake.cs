namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure decisions behind the game-facing <c>Storage.DrawerSource.Take</c>: how much a pull
    /// may remove, and how that amount is split into the stack-sized chunks a single
    /// <c>Inventory.AddItem(GameObject, int)</c> call can deliver. A pull must never destroy items —
    /// the design's top anti-goal — so nothing may be removed unless it also has somewhere to go,
    /// nothing beyond what the drawer actually holds may be removed, and a request larger than one
    /// stack must be delivered in chunks because one <c>AddItem</c> call places at most one stack.
    /// Lives in <c>Logic/</c> so these decisions are unit-testable without a live drawer or
    /// inventory; the game-facing code resolves the prefab, checks room and performs the removal and
    /// delivery one chunk at a time.
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
        /// One <c>Inventory.AddItem(GameObject, int)</c> call places <b>at most one stack</b> — it
        /// sets <c>m_stack = Mathf.Min(amount, m_maxStackSize)</c> and adds a single
        /// <c>ItemData</c> — so an amount larger than one stack must be split into chunks of at most
        /// this size; delivering it in one call silently destroys the excess.
        /// </para>
        /// </summary>
        /// <param name="target">How many items the pull intends to deliver in total.</param>
        /// <param name="deliveredSoFar">How many have already been delivered (the running total).</param>
        /// <param name="maxStackSize">The prefab's maximum stack size, i.e. the most one <c>AddItem</c> call can place.</param>
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
    }
}
