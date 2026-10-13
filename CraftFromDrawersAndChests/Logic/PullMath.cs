namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// Which amount a pull asks storage for. The two bases exist side by side because the mod pulls for
    /// two different reasons, and the difference is exactly what the player sees: a fetch must be
    /// repeatable, a craft must not over-ask.
    /// </summary>
    public enum RequestBasis
    {
        /// <summary>
        /// Only what the destination is short of (<see cref="PullMath.Shortfall"/>). The crafting and
        /// placement pull uses it: that pull exists so an action the game has already been told is
        /// possible can happen, so it asks for no more than the action still needs.
        /// </summary>
        Shortfall,

        /// <summary>
        /// The requirement's whole cost, whatever the destination already holds. The deliberate fetch
        /// uses it — the ctrl-click pull and the pull-key build press — so that pulling for a second
        /// copy of the same recipe supplies that copy's materials instead of nothing.
        /// </summary>
        FullCost,
    }

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

        /// <summary>
        /// The amount a pull asks storage for, from the requirement's total cost and what the
        /// destination already holds — the single definition of that rule, so a pull and the gate that
        /// promised it can never disagree about what is being asked for.
        /// <para>
        /// <see cref="RequestBasis.FullCost"/> is the deliberate fetch. It is CraftFromContainers' own
        /// rule (CFC 4.0.7's <c>PullResources</c> accumulates only what it has itself taken from
        /// containers, never what the player carries), and it is what makes a fetch repeatable: pulling
        /// for a second portal once the first portal's materials are in the inventory must supply the
        /// second portal's materials, not nothing. Asking for the full cost is safe here because the
        /// fetch is planned as a fill — it takes what storage actually has and what the inventory
        /// actually accepts, rather than refusing.
        /// </para>
        /// <para>
        /// <see cref="RequestBasis.Shortfall"/> is the crafting and placement pull, unchanged: it asks
        /// only for the shortfall, because its job is to satisfy an action, not to stockpile, and
        /// over-asking there would take an item the player already holds out of storage in its place.
        /// </para>
        /// <para>
        /// A non-positive cost asks for nothing under either basis, and the result is never negative: a
        /// negative request would read as a subtraction wherever an amount is expected.
        /// </para>
        /// </summary>
        /// <param name="required">The amount the recipe or piece consumes in total.</param>
        /// <param name="held">The amount the destination already holds; ignored under <see cref="RequestBasis.FullCost"/>.</param>
        /// <param name="basis">Whether the request is the shortfall or the whole cost.</param>
        public static int RequestAmount(int required, int held, RequestBasis basis)
        {
            if (required <= 0)
                return 0;

            return basis == RequestBasis.FullCost ? required : Shortfall(required, held);
        }
    }
}
