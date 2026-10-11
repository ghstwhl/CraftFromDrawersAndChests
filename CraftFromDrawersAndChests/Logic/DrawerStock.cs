using System;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure "how many of this item does this drawer contribute" decision. Storage is keyed by
    /// prefab name (<c>"Wood"</c>), not by <c>m_shared.m_name</c> (<c>"$item_wood"</c>): a drawer's
    /// ZDO stores a prefab name and the other storage mods on the server key by prefab too. Lives in
    /// <c>Logic/</c> so the decision is unit-testable without a live ZDO — the game-facing
    /// <c>DrawerSource</c> reads the ZDO's <c>"Prefab"</c> and <c>"Amount"</c> and delegates here.
    /// </summary>
    public static class DrawerStock
    {
        /// <summary>
        /// How many of <paramref name="itemName"/> the drawer contributes. A drawer whose prefab
        /// does not match contributes nothing, and one reporting a prefab with a zero (or negative)
        /// amount contributes nothing — and never throws (Review Focus 4).
        /// </summary>
        /// <param name="drawerPrefab">The prefab name read from the drawer's ZDO <c>"Prefab"</c>; may be null or empty.</param>
        /// <param name="drawerAmount">The count read from the drawer's ZDO <c>"Amount"</c>.</param>
        /// <param name="itemName">The prefab name being looked for (not a localized display name); may be null.</param>
        public static int CountFor(string drawerPrefab, int drawerAmount, string itemName)
        {
            if (drawerAmount <= 0)
                return 0;

            if (string.IsNullOrEmpty(drawerPrefab) || string.IsNullOrEmpty(itemName))
                return 0;

            return string.Equals(drawerPrefab, itemName, StringComparison.Ordinal)
                ? drawerAmount
                : 0;
        }
    }
}
