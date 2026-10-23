using System;
using System.Collections.Generic;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure decisions behind station feeding (Task 15a): how much a station can still take, how
    /// many items an interaction asks for, and whether an item's prefab name is on a configured
    /// disallow list.
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
    /// <para>
    /// <b>The trim-and-drop is a deliberate divergence from CraftFromContainers.</b> CFC's untrimmed
    /// <c>Split(',').Contains(...)</c> treats the <c>" Stone"</c> in <c>"Wood, Stone"</c> as its own
    /// literal entry, so a config entry written with a stray space silently fails to match. Trimming
    /// and dropping empties is friendlier and is kept on purpose, so the test pinning it documents a
    /// choice rather than asserting the oracle's behaviour.
    /// </para>
    /// </summary>
    public static class StationFeedRules
    {
        /// <summary>
        /// Whether <paramref name="prefabName"/> appears in the comma-separated
        /// <paramref name="configuredTypes"/>. The comparison is ordinal (so <c>RoundLog</c> does not
        /// match <c>roundlog</c>), exact (so <c>Log</c> does not match <c>RoundLog</c>) and against
        /// the item's <b>prefab</b> name, mirroring CraftFromContainers' own
        /// <c>Split(',').Contains(item.m_dropPrefab.name)</c> except that entries are trimmed and
        /// empty ones dropped (a deliberate divergence — see the class remarks). A null or empty list,
        /// and a null or empty prefab name, are never a match.
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

        /// <summary>
        /// How many of a station's item an interaction asks storage for: <b>one</b> by default, or the
        /// whole <paramref name="remaining"/> capacity while the fill-all modifier is held.
        /// <para>
        /// This is CraftFromContainers 4.0.7's <c>pullAll</c> amount — its fireplace, smelter-ore and
        /// smelter-fuel patches all read
        /// <c>pullAll ? (int)Mathf.Min(capacity - used, available) : 1</c> (CFC
        /// <c>BepInExPlugin.cs</c> lines 317, 503 and 624) — reduced to the station-capacity half,
        /// which is the only half this pure layer can own: "available" is the pull planner's own
        /// question, and asking a storage for more than it holds is already the planner's problem.
        /// A station with no room left asks for nothing, with or without the modifier, so a full
        /// station is never fed and never costs the player stock.
        /// </para>
        /// <para>
        /// This is the station-capacity half of the request only, and therefore the <i>most</i> an
        /// interaction may ask storage for. The fill path caps it further to what the destination
        /// inventory can actually accept and accepts <c>min(available, requested)</c> from storage
        /// (see <c>PullEngine.PullToInventoryFill</c>), so holding the modifier never pulls less than
        /// releasing it does.
        /// </para>
        /// </summary>
        /// <param name="fillAll">Whether the fill-all modifier is held.</param>
        /// <param name="remaining">How many whole items the station can still take; see
        /// <see cref="RemainingCapacity"/> and <see cref="RemainingFuelCapacity"/>.</param>
        public static int FillAmount(bool fillAll, int remaining)
        {
            if (remaining <= 0)
                return 0;

            return fillAll ? remaining : 1;
        }

        /// <summary>
        /// Whether a station interaction should feed at all, given whether the player already carries an
        /// item the station accepts.
        /// <para>
        /// Without the modifier the answer is "only when the player carries none": vanilla's own add
        /// would find and consume the carried item one unit at a time, and leaving it alone is what
        /// CraftFromContainers does (<c>inventory.HaveItem(...) &amp;&amp; !pullAll</c>, CFC lines 280/582).
        /// The modifier inverts that: a fill is an explicit request for the station's whole remaining
        /// capacity, and the stock that pays for it is the player's own plus storage's — so the guard
        /// must not short-circuit, or holding the key would change nothing whenever the fuel or ore is
        /// in the inventory rather than in a chest (reported from play 2026-10-23 against a blast
        /// furnace: shift-E added one flametal per press).
        /// </para>
        /// <para>
        /// CraftFromContainers only applies that inversion to a station's <i>fuel</i>; its smelter-ore
        /// guard returns true whatever the modifier says (CFC lines 464-482), so applying it to ore is a
        /// deliberate divergence — the alternative leaves the one station whose stock is ore unable to
        /// fill from carried ore.
        /// </para>
        /// </summary>
        /// <param name="fillAll">Whether the fill-all modifier is held.</param>
        /// <param name="playerHoldsInput">Whether the player's inventory already holds an item the
        /// station accepts.</param>
        public static bool ShouldAttemptFeed(bool fillAll, bool playerHoldsInput)
        {
            return fillAll || !playerHoldsInput;
        }

        /// <summary>
        /// Whether a station's multi-input walk should <b>stop</b> at an input, given whether the
        /// destination inventory holds that input once the attempt for it has run.
        /// <para>
        /// The only thing that may stop the walk is the destination holding the input — never "the
        /// pull returned success". Those are different questions: a pull whose shortfall is already
        /// covered, or which storage has nothing to add to, succeeds as a <b>satisfied no-op</b>, and
        /// stopping there skips every later input. That is exactly what happened in 1.0.6: the walk
        /// stopped at the smelter's first conversion even when storage held none of it, so tin, iron
        /// and silver could no longer be fed from storage at all (reported from play 2026-10-23). A
        /// blast furnace, which converts one ore, hid it.
        /// </para>
        /// <para>
        /// This signature deliberately takes <b>one</b> fact. The tempting wrong implementation adds a
        /// "did anything move?" argument and stops on it; there is no such parameter here so that
        /// cannot be reintroduced silently.
        /// </para>
        /// </summary>
        /// <param name="destinationHoldsInput">Whether the destination inventory holds one of the
        /// item an attempt has just run for.</param>
        public static bool ShouldStopInputWalk(bool destinationHoldsInput)
        {
            return destinationHoldsInput;
        }

        /// <summary>
        /// The hover hint that advertises the fill-all modifier on a smelter's add action: the
        /// bracketed <c>[shift+Use] … max</c> line CraftFromContainers 4.0.7 appends to the smelter's
        /// fuel and ore hovers (CFC <c>BepInExPlugin.cs</c> lines 436-438 and 451-453). Returns null
        /// when the modifier is unbound, because CFC only prints the hint for a non-empty key
        /// (<c>fillAllModKey.Value?.Length &gt; 0</c>).
        /// <para>
        /// The result is deliberately not localized: the game-facing patch hands it to
        /// <c>Localization.instance.Localize</c> exactly as CFC does, so the <c>$KEY_Use</c> token and
        /// the <paramref name="action"/> token (CFC's <c>$piece_smelter_add</c>, or a smelter's own
        /// <c>m_addOreTooltip</c>) resolve at display time rather than being baked in here.
        /// </para>
        /// </summary>
        /// <param name="fillAllKey">The configured fill-all modifier; null or empty hides the hint.</param>
        /// <param name="action">The localization token naming the action, e.g. <c>"$piece_smelter_add"</c>.</param>
        public static string FillAllHint(string fillAllKey, string action)
        {
            if (string.IsNullOrEmpty(fillAllKey))
                return null;

            return "\n[<color=yellow><b>" + fillAllKey + "+$KEY_Use</b></color>] " + action + " max";
        }

        /// <summary>One item type a station feed added, and how many of it. Pure; the game-facing
        /// patch fills it with the item's localized display name.</summary>
        public readonly struct FedAmount
        {
            /// <summary>The item's display name as it should read on screen (already localized).</summary>
            public readonly string Name;

            /// <summary>How many were added.</summary>
            public readonly int Amount;

            public FedAmount(string name, int amount)
            {
                Name = name;
                Amount = amount;
            }
        }

        /// <summary>
        /// The one-line HUD summary of what a station feed added: <c>"Added 10 Scrap iron, 3 Tin"</c>.
        /// Entries keep the order given — the station's own conversion order — so a smelter that takes
        /// several ores lists them the way it declares them, not the way storage happened to hand them
        /// over. An entry with no name or a non-positive amount is skipped, and a feed that added
        /// nothing produces <c>null</c>, which the caller must not show.
        /// <para>
        /// Vanilla prints its own <c>$msg_added</c> for the unit it consumes out of the player's hands;
        /// this is the same information for the units the mod fed, which vanilla never sees. It is a
        /// summary rather than one message per unit so that a fill-all press over several ore types
        /// reads as a single line instead of a dozen swapping messages.
        /// </para>
        /// </summary>
        /// <param name="fed">What was added, in display order; may be null.</param>
        public static string FeedSummary(IReadOnlyList<FedAmount> fed)
        {
            if (fed == null || fed.Count == 0)
                return null;

            var parts = new List<string>(fed.Count);
            for (int i = 0; i < fed.Count; i++)
            {
                FedAmount entry = fed[i];
                if (string.IsNullOrEmpty(entry.Name) || entry.Amount <= 0)
                    continue;

                parts.Add(entry.Amount + " " + entry.Name);
            }

            return parts.Count == 0 ? null : "Added " + string.Join(", ", parts.ToArray());
        }
    }
}
