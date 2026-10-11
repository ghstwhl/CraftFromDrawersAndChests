namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The one piece of arithmetic the crafting and building pull adds on top of the planner: how
    /// much of a requirement storage must still supply once the player's own inventory is counted.
    /// Pure logic, so it must never reference UnityEngine, Valheim, BepInEx or Jotunn; the mapping
    /// from a game <c>Piece.Requirement</c> to a request cannot be pure and stays game-facing.
    /// </summary>
    public static class PullMath
    {
        /// <summary>
        /// How many more of an item are still needed, given what the player already carries. Never
        /// negative: holding at least <paramref name="required"/> means storage need not contribute
        /// anything, and a negative "shortfall" would read as a subtraction wherever an amount is
        /// expected.
        /// </summary>
        /// <param name="required">The amount the recipe or piece consumes in total.</param>
        /// <param name="held">The amount the player already carries.</param>
        public static int Shortfall(int required, int held)
        {
            return required > held ? required - held : 0;
        }
    }
}
