namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure decision behind spending ammunition out of an equipped Adventure Backpacks backpack
    /// without moving it into the player's inventory.
    /// <para>
    /// <b>Why a redirect is needed at all.</b> Vanilla resolves ammunition through
    /// <c>Inventory.GetAmmoItem(ammoType)</c>, which walks the player's own <c>m_inventory</c>, and then
    /// spends the round with <c>Inventory.RemoveItem(ammoItem, 1)</c>. Adventure Backpacks never widens
    /// <c>GetAmmoItem</c>, so a bow with its arrows in the backpack reports "no arrows"; and
    /// <c>RemoveItem</c> refuses an item the inventory does not contain, so answering the lookup alone
    /// would fire the arrow for free.
    /// </para>
    /// <para>
    /// So the bridge answers the lookup with the backpack's round and redirects that one removal into
    /// the backpack - the stack never leaves it, matching how Adventure Backpacks pays for a craft
    /// (counted and deducted in place). This predicate is the redirect's guard, and both halves matter:
    /// a round the inventory does hold is vanilla's to remove (spending it again would take two rounds
    /// for one shot), and an item the backpack does not hold belongs to some other removal entirely.
    /// </para>
    /// </summary>
    public static class BackpackAmmoRules
    {
        /// <summary>
        /// Whether this removal is the spend of a backpack round: the player's inventory does not hold
        /// the item and the equipped backpack does.
        /// </summary>
        /// <param name="inventoryContainsItem">Whether the player's own inventory holds the item.</param>
        /// <param name="backpackContainsItem">Whether the equipped backpack holds it.</param>
        public static bool ShouldSpendFromBackpack(bool inventoryContainsItem, bool backpackContainsItem)
        {
            return !inventoryContainsItem && backpackContainsItem;
        }
    }
}
