using System;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure decisions behind station feeding (Task 15a): how much a station can still take, and
    /// whether an item's prefab name is on a configured disallow list.
    /// <para>
    /// Nothing here touches UnityEngine, Valheim, BepInEx or Jötunn. A station holds no
    /// <c>Inventory</c> of its own — a fireplace's fuel is a float on its ZDO, a smelter's queue is a
    /// count, a cooking station's food is a set of item-name slots and a turret's ammunition is a
    /// count — so its "capacity" is a plain number the game-facing patch reads off the station and
    /// the arithmetic over it lives here, where it can be tested without the game.
    /// </para>
    /// <para>
    /// The disallow lists are CraftFromContainers' <c>fuelDisallowTypes</c> / <c>oreDisallowTypes</c>
    /// ("Item prefab names that may not be used as fuel / ore, comma-separated"). CraftFromContainers
    /// tests them with <c>Value.Split(',').Contains(item.m_dropPrefab.name)</c> — an
    /// <b>ordinal</b>, exact match against the item's <b>prefab</b> name, with no trimming — and
    /// <see cref="IsDisallowedType"/> matches on the same name and the same case-sensitivity while
    /// trimming and dropping empty entries through <see cref="ConfigModel.ParseList"/> (the single
    /// list parser this project already pins with a test).
    /// </para>
    /// </summary>
    public static class StationFeedRules
    {
        /// <summary>
        /// Whether <paramref name="prefabName"/> appears in the comma-separated
        /// <paramref name="configuredTypes"/>. The comparison is ordinal (so <c>RoundLog</c> does not
        /// match <c>roundlog</c>), exact (so <c>Log</c> does not match <c>RoundLog</c>) and against
        /// the item's <b>prefab</b> name, mirroring CraftFromContainers' own
        /// <c>Split(',').Contains(item.m_dropPrefab.name)</c>. A null or empty list, and a null or
        /// empty prefab name, are never a match.
        /// </summary>
        /// <param name="configuredTypes">The configured comma-separated prefab names; may be null.</param>
        /// <param name="prefabName">The candidate item's prefab name; may be null.</param>
        public static bool IsDisallowedType(string configuredTypes, string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return false;

            string[] types = ConfigModel.ParseList(configuredTypes);
            for (int i = 0; i < types.Length; i++)
            {
                if (string.Equals(types[i], prefabName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// How many whole items a station whose capacity is a count can still take: the difference,
        /// floored at zero so a station already at (or over) its capacity asks for nothing. Used for a
        /// smelter's ore queue (<c>m_maxOre</c> less <c>GetQueueSize()</c>) and a turret's ammunition
        /// (<c>m_maxAmmo</c> less <c>GetAmmo()</c>).
        /// </summary>
        /// <param name="capacity">The station's capacity, in items.</param>
        /// <param name="used">How many it already holds.</param>
        public static int RemainingCapacity(int capacity, int used)
        {
            int remaining = capacity - used;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>
        /// How many fuel items a station whose fuel is a float can still take. Vanilla adds one unit of
        /// fuel per item and calls a fire full when the ceiling of its stored fuel reaches the
        /// capacity (<c>Fireplace.Interact</c> tests <c>Mathf.CeilToInt(num) &gt;= m_maxFuel</c>;
        /// <c>Smelter.OnAddFuel</c> and <c>CookingStation.OnAddFuelSwitch</c> test
        /// <c>GetFuel() &gt; m_maxFuel - 1</c>, the same boundary), so the remaining count is the
        /// capacity less the ceiling of what is stored, floored at zero. Both values are rounded up
        /// first, so a fractional capacity is measured on the same boundary vanilla uses.
        /// </summary>
        /// <param name="maxFuel">The station's fuel capacity.</param>
        /// <param name="fuel">The fuel it currently stores.</param>
        public static int RemainingFuelCapacity(float maxFuel, float fuel)
        {
            int remaining = (int)Math.Ceiling(maxFuel) - (int)Math.Ceiling(fuel);
            return remaining > 0 ? remaining : 0;
        }
    }
}
