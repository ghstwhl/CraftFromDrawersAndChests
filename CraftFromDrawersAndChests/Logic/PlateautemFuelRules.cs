using System.Collections.Generic;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure decision behind feeding a Plateautem — erkle64's terrain-flattening totem
    /// (<c>Plateautem.Plateautem</c>): which item prefab one press of its interaction will consume.
    /// <para>
    /// <b>Why the mod has to ask at all.</b> The totem keeps its own fuel selection, so unlike a
    /// fireplace there is no single "fuel item": a press takes stone when the selection is
    /// <c>-1</c>, and otherwise the prefab of the selected entry in its configured fuel list
    /// (<c>Interact</c> in Plateautem 0.4.3 branches on <c>currentFuelItemIndex == -1</c> to
    /// <c>TakeStoneFromUser</c>, else <c>TakeFuelItemFromUser(currentFuelItemIndex)</c>).
    /// </para>
    /// <para>
    /// <b>Reading the selection is not optional.</b> The totem counts and removes the selected prefab
    /// by name from the player's own <c>Inventory</c>, so pulling any other item would fill the
    /// inventory with something it will not consume — and its own message would still say it has no
    /// fuel. There is no fallback: an index outside the configured list (a server can shrink
    /// "Fuel Item List" while a client still holds an older selection) has no item to ask storage for,
    /// and guesses are not taken here.
    /// </para>
    /// </summary>
    public static class PlateautemFuelRules
    {
        /// <summary>
        /// The prefab the totem takes for its stone mode, which is Plateautem's own
        /// <c>stoneItemPrefabName</c> constant (an item prefab name, unlike the <c>$item_stone</c>
        /// shared name the totem displays).
        /// </summary>
        public const string StonePrefabName = "Stone";

        /// <summary>
        /// The item prefab a press of the totem's interaction consumes, or <c>null</c> when the
        /// selection does not resolve to one (an index past the configured list, or a blank entry in
        /// it).
        /// </summary>
        /// <param name="selectedFuelIndex">
        /// The totem's <c>currentFuelItemIndex</c>: <c>-1</c> is stone mode, otherwise an index into
        /// <paramref name="fuelPrefabNames"/>.
        /// </param>
        /// <param name="fuelPrefabNames">
        /// The <c>prefabName</c> of each configured fuel entry, in the totem's own order; may be null.
        /// </param>
        public static string PrefabToSupply(int selectedFuelIndex, IReadOnlyList<string> fuelPrefabNames)
        {
            if (selectedFuelIndex < 0)
                return StonePrefabName;

            if (fuelPrefabNames == null || selectedFuelIndex >= fuelPrefabNames.Count)
                return null;

            string prefabName = fuelPrefabNames[selectedFuelIndex];
            return string.IsNullOrEmpty(prefabName) ? null : prefabName;
        }

        /// <summary>
        /// How many items of <paramref name="unitsPerItem"/> fuel each the totem can still hold: its
        /// own arithmetic, <c>floor((capacity - stored) / unitsPerItem)</c>, floored at zero.
        /// <para>
        /// Plateautem 0.4.3 computes exactly this in both of its take paths —
        /// <c>Mathf.FloorToInt((configMaximumFuel - GetTotalFuelStored()) / fuelValue)</c> for fuel and
        /// <c>Mathf.FloorToInt(configMaximumStone - currentStoneStored)</c> for stone (which is this with
        /// a value of 1) — and then takes <c>min(that, what the player holds)</c> on a hold press.
        /// </para>
        /// </summary>
        /// <param name="capacity">The totem's maximum stored fuel (or stone).</param>
        /// <param name="stored">What it already stores, in the same fuel units.</param>
        /// <param name="unitsPerItem">The fuel value of one item; stone counts as 1.</param>
        public static int UnitsInRoom(float capacity, float stored, float unitsPerItem)
        {
            if (unitsPerItem <= 0f)
                return 0;

            float room = (capacity - stored) / unitsPerItem;
            return room > 0f ? (int)System.Math.Floor(room) : 0;
        }

        /// <summary>
        /// How many units one interaction should supply: one for a single press (what the totem takes),
        /// and the whole remaining room for a hold (its <c>takeAll</c> path). Zero when the totem is
        /// already full, so nothing is pulled for an interaction that cannot consume it.
        /// </summary>
        /// <param name="hold">Whether the interaction is a held press (<c>Interact</c>'s hold flag).</param>
        /// <param name="unitsThatFit">What <see cref="UnitsInRoom"/> allowed for this interaction.</param>
        public static int UnitsToRequest(bool hold, int unitsThatFit)
        {
            if (unitsThatFit <= 0)
                return 0;

            if (!hold)
                return 1;

            return unitsThatFit;
        }
    }
}
