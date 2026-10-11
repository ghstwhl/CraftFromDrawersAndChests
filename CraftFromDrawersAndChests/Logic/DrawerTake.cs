namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure "how much may this drawer pull remove" decision behind the game-facing
    /// <c>Storage.DrawerSource.Take</c>. A pull must never destroy items — the design's top
    /// anti-goal — so nothing may be removed unless it also has somewhere to go, and nothing beyond
    /// what the drawer actually holds may be removed. Lives in <c>Logic/</c> so that decision is
    /// unit-testable without a live drawer or inventory; the game-facing code resolves the prefab,
    /// checks room and performs the removal and delivery.
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
    }
}
