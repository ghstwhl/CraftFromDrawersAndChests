using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using CraftFromDrawersAndChests.Pull;
using HarmonyLib;
using UnityEngine;

namespace CraftFromDrawersAndChests.Patches
{
    /// <summary>
    /// Station feeding (Task 15a): a fireplace's fuel, a smelter's ore and fuel, a cooking station's
    /// food and a turret's ammunition are fetched from chests and drawers within range of the
    /// <b>station</b>, not of the player.
    /// <para>
    /// <b>Range is measured from the station.</b> Every patch here hands
    /// <c>PullEngine</c> the station's own <c>transform.position</c> as the origin. CraftFromContainers
    /// 4.0.7 does exactly that for its fireplace, smelter and cooking-station patches — each collects
    /// its sources with <c>GetNearbyContainers(__instance.transform.position)</c> (CFC
    /// <c>BepInExPlugin.cs</c> lines 304, 361, 401, 485, 612) — so a chest next to the smelter feeds it
    /// even while the player stands further away, and a chest out of the smelter's own range cannot.
    /// <b>Its turret patch is the exception:</b> CFC measures that one from the <i>player</i>
    /// (<c>Player.m_localPlayer.transform.position</c>, CFC line 1160). This port keeps the station's
    /// own transform for the turret too — a deliberate divergence, since the brief asked for the
    /// station's position — so a chest beside a turret feeds it here even with no chest near the player.
    /// </para>
    /// <para>
    /// <b>The pull is the mod's existing one, unchanged.</b> Each patch makes one call to
    /// <see cref="PullEngine.PullToInventory"/> — the same source collection, the same all-or-nothing
    /// take sequencing and the same destination-capacity proof the crafting, building and ctrl-click
    /// pulls use — asking for a single item whose destination is the <i>interacting player's</i>
    /// inventory. Nothing here re-implements any of that. It is the
    /// <see cref="RequestBasis.Shortfall"/> basis, so storage is asked only for what the destination
    /// does not already hold: an interaction never takes a second item for something the player is
    /// already carrying. (The deliberate fetch — the ctrl-click pull and the pull-key build press —
    /// deliberately asks for the whole cost instead, so that repeating it supplies materials again; see
    /// <see cref="PullEngine.PullToPlayer"/>.) A
    /// station is not an <c>Inventory</c> (see the remarks on <see cref="PullEngine.PullToInventory"/>'s
    /// callers below), and vanilla consumes the station's fuel, ore, food and ammo <b>out of the
    /// player's inventory</b>, so delivering there and then letting vanilla run is both the only
    /// shape the engine has and the shape vanilla itself uses.
    /// </para>
    /// <para>
    /// <b>Nothing is pulled that cannot be delivered.</b> The pull proves storage can cover the item
    /// <i>and</i> that the destination inventory can accept it before anything moves, so a full
    /// player inventory means nothing is taken and nothing can reach the ground. The station's own
    /// remaining capacity is checked before the pull as well (<see cref="StationFeedRules"/>), so a
    /// full station is never even asked for an item — and a full station can therefore never cost the
    /// player stock that would then sit unused in their inventory.
    /// </para>
    /// <para>
    /// <b>Gating.</b> Each patch reads its own setting from the shared <c>PluginConfig.Flags</c>
    /// snapshot — <c>PullFuel</c> for the fireplace, the smelter's fuel, the cooking station's fuel and
    /// the shield generator's fuel (a generator's fuel is fuel, and CraftFromContainers has no
    /// generator patch to copy a fourth setting from), <c>PullOre</c> for the smelter's ore,
    /// <c>PullCooking</c> for the cooking station's food —
    /// and the mod-enabled flag, and obeys <see cref="PullKeyPatches.AllowByKey"/>, the same prevent
    /// gate the pull key obeys. CraftFromContainers gates its turret patch on the mod-enabled flag and
    /// the prevent key alone: it has <b>no</b> turret setting, and this port adds none (see the
    /// turret patch's remarks).
    /// </para>
    /// <para>
    /// <b>Fill-all.</b> Holding the fill-all modifier (<c>"Fill all mod key"</c>, CraftFromContainers
    /// 4.0.7's <c>fillAllModKey</c>) makes an interaction ask storage for the station's whole remaining
    /// capacity instead of a single item. CraftFromContainers reads that modifier in its fireplace
    /// (<c>pullAll</c>, CFC lines 278/280/317), smelter-ore (464/465/503) and smelter-fuel (580/582/624)
    /// patches and nowhere else — its cooking-station patches never mention it — so the three matching
    /// patches here read it and the two cooking-station patches do not. The amount itself is
    /// <see cref="StationFeedRules.FillAmount"/>; the same modifier also lifts the "the player already
    /// carries the fuel" guard on the fireplace and the smelter's fuel, exactly as CFC's
    /// <c>inventory.HaveItem(...) &amp;&amp; !pullAll</c> does (CFC lines 280 and 582) — and, since 1.0.6,
    /// the "the player already carries an input ore" guard on the smelter, which CFC does <b>not</b> lift
    /// (its input-ore guard returns true whatever the modifier says, CFC lines 471-482). That ore case is
    /// a deliberate divergence: without it shift-E cannot fill the one station whose stock <i>is</i> ore
    /// from ore already in hand (reported from play 2026-10-23 against a blast furnace, which added one
    /// flametal per press). Every one of those guards goes through
    /// <see cref="StationFeedRules.ShouldAttemptFeed"/>, and a pull that storage has nothing to add to
    /// counts as <b>satisfied</b> rather than failed, so the fill goes on to feed what the player is
    /// carrying rather than stopping at zero. That satisfied result must never decide the <b>walk</b>
    /// across a station's several inputs — only the inventory holding the input may end it (see
    /// <see cref="StationFeedRules.ShouldStopInputWalk"/>, and the remark on
    /// <see cref="PullFirstInputs"/>): 1.0.6 conflated the two and made every smelter ore past the first
    /// conversion unfeedable from storage. CFC additionally
    /// lets the modifier override its prevent key on the smelter patches (<c>!AllowByKey() &amp;&amp;
    /// !pullAll</c>, lines 465 and 582) but not on the fireplace (line 280); this port keeps the prevent
    /// gate authoritative everywhere and documents that as a deliberate divergence — see the gating
    /// remarks above.
    /// </para>
    /// <para>
    /// <b>A fill is not a craft (R31).</b> The fill-all request is planned as a <b>fill</b>, through
    /// <see cref="PullEngine.PullToInventoryFill"/>: it asks for the whole remaining capacity but
    /// accepts <c>min(available, requested)</c>, so a base that can only spare part of that capacity
    /// still feeds the station that part. Planning it as a fixed amount would be all-or-nothing (see
    /// <see cref="PullPlan.Plan"/>), so a base short of the whole remaining capacity would move
    /// <i>nothing</i> — strictly worse than releasing the key, whose one-item request the base can
    /// usually cover. The released interaction keeps the all-or-nothing plan unchanged; the request is
    /// still only the shortfall below what the player already carries, and the destination is still
    /// proved for whatever is actually taken.
    /// </para>
    /// <para>
    /// <b>The hint.</b> The smelter's fuel and ore hovers also advertise the modifier with the bracketed
    /// "[key+Use] … max" line CraftFromContainers appends (CFC lines 429-455). The text is
    /// <see cref="StationFeedRules.FillAllHint"/> and the two postfixes mirror CFC's own; the fireplace,
    /// the cooking station and the turret have no such hint in CraftFromContainers and get none here.
    /// </para>
    /// <para>
    /// <b>Shield generators (1.0.5).</b> <c>ShieldGenerator</c> is the fourth fuelled station and the
    /// only one CraftFromContainers 4.0.7 has no patch for. Its fuel is <b>not</b> added by
    /// <c>Interact</c> (that fires the shield) but by the private
    /// <c>OnAddFuel(Switch, Humanoid, ItemData)</c>, which <c>Start</c> wires into its add-fuel
    /// <c>Switch</c>: <c>Switch.Interact</c> calls it with a null item and <c>Switch.UseItem</c> with
    /// the held one. Its hover is the private <c>OnHoverAddFuel()</c>, wired to the same switch's
    /// <c>m_onHover</c> — so this station takes exactly the smelter's two fuel patches' shape. It
    /// differs in one place: it declares a <b>list</b> of fuel prefabs (<c>m_fuelItems</c>) where a
    /// smelter declares one <c>m_fuelItem</c>, so the request walks that list and asks storage for the
    /// first fuel it can supply — the same walk the smelter's ore patch makes over <c>m_conversion</c>.
    /// Vanilla's own add removes one item and adds one fuel per interaction, which is why an unpatched
    /// generator adds a single fuel per press however the fill-all key is held (reported from play
    /// 2026-10-23).
    /// </para>
    /// </summary>
    // A class-level [HarmonyPatch] is REQUIRED here - see the note on CraftPatches. Without it every
    // method-level target in this file is silently skipped and no station ever sees storage.
    [HarmonyPatch]
    internal static class StationPatches
    {
        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        // Seven of the guards below read vanilla state through methods that are PRIVATE in the shipping
        // assembly (assembly_valheim.dll) even though the publicized reference this project compiles
        // against reports them public: Smelter.GetQueueSize/GetFuel, ShieldGenerator.GetFuel and
        // CookingStation.GetFuel/HaveDoneItem/GetFreeSlot/IsFireLit. A direct call binds at compile time
        // and is unresolvable at runtime, so each goes through reflection instead. The MethodInfos are
        // resolved once here and cached in these fields, because the guards run inside per-frame station
        // interactions. (Turret.GetAmmo and Turret.GetAmmoType are public in the shipping assembly too —
        // the sanctioned probe confirms it — so those two stay direct calls.)
        //
        // Smelter.FindCookableItem and CookingStation.FindCookableItem are PRIVATE in the shipping
        // assembly as well, but are deliberately NOT reflected: a public equivalent exists — vanilla's
        // FindCookableItem(Inventory) is just a search of the passed inventory for the station's own
        // inputs — and CraftFromContainers already uses that equivalent (Inventory.GetItem over
        // m_conversion). Removing the dependency beats caching a MethodInfo for a private member. See
        // InventoryHasInput below.
        private static readonly MethodInfo SmelterGetQueueSize = Resolve(typeof(Smelter), "GetQueueSize");
        private static readonly MethodInfo SmelterGetFuel = Resolve(typeof(Smelter), "GetFuel");
        private static readonly MethodInfo ShieldGeneratorGetFuel = Resolve(typeof(ShieldGenerator), "GetFuel");
        private static readonly MethodInfo CookingStationGetFuel = Resolve(typeof(CookingStation), "GetFuel");
        private static readonly MethodInfo CookingStationHaveDoneItem = Resolve(typeof(CookingStation), "HaveDoneItem");
        private static readonly MethodInfo CookingStationGetFreeSlot = Resolve(typeof(CookingStation), "GetFreeSlot");
        private static readonly MethodInfo CookingStationIsFireLit = Resolve(typeof(CookingStation), "IsFireLit");
        private static bool _invokeFailureLogged;
        private static bool _returnTypeFailureLogged;

        /// <summary>
        /// Resolves a parameterless vanilla method for reflection, logging once — at type initialization
        /// — when it is missing. A null result is not fatal: every guard that uses it fails closed, so
        /// the station is simply not fed. Uses <see cref="AccessTools"/> so the same member resolves
        /// whether the assembly exposes it as public or private.
        /// <para>
        /// The lookup itself is wrapped: <see cref="AccessTools.Method(Type, string)"/> can throw
        /// (<c>AmbiguousMatchException</c> when a type declares two same-named overloads), and a throw
        /// from a <c>static readonly</c> initializer surfaces as <c>TypeInitializationException</c>
        /// from every prefix — bypassing the fail-closed design entirely. On throw this logs once and
        /// returns <c>null</c>, the same as "not found".
        /// </para>
        /// </summary>
        private static MethodInfo Resolve(Type type, string name)
        {
            MethodInfo method;
            try
            {
                method = AccessTools.Method(type, name);
            }
            catch (Exception e)
            {
                Log.LogWarning("StationPatches: resolving vanilla member " + type.Name + "." + name +
                    "() threw; stations of this kind will not be fed from storage. " + e.Message);
                return null;
            }

            if (method == null)
                Log.LogWarning("StationPatches: vanilla member " + type.Name + "." + name + "() was not found; " +
                    "stations of this kind will not be fed from storage.");
            return method;
        }

        /// <summary>
        /// Invokes a cached parameterless getter. Returns false — never throwing — when the member is
        /// unavailable or the invoke fails, so a caller can treat "cannot read" as "cannot feed". A
        /// failing invoke is logged once, not once per frame.
        /// </summary>
        private static bool TryInvoke(MethodInfo method, object instance, out object result)
        {
            result = null;
            if (method == null || instance == null)
                return false;

            try
            {
                result = method.Invoke(instance, null);
                return true;
            }
            catch (Exception e)
            {
                if (!_invokeFailureLogged)
                {
                    _invokeFailureLogged = true;
                    Log.LogWarning("StationPatches: a vanilla station member threw on invoke; affected " +
                        "stations will not be fed. " + e.Message);
                }
                return false;
            }
        }

        /// <summary>
        /// Logs once when a cached member resolves and invokes but returns an unexpected type. That is
        /// the one failure mode that is otherwise indistinguishable from "the member is absent":
        /// a right-name/wrong-signature lookup would disable the feature silently. The invoke itself
        /// already logs a failed call; this logs the type mismatch that a successful call can hide.
        /// </summary>
        private static void LogUnexpectedReturnType(string expected, object actual)
        {
            if (_returnTypeFailureLogged)
                return;

            _returnTypeFailureLogged = true;
            Log.LogWarning("StationPatches: a vanilla station member returned " +
                (actual == null ? "null" : actual.GetType().Name) + " where " + expected +
                " was expected; affected stations will not be fed. Vanilla may have changed shape.");
        }

        /// <summary>Reads a cached parameterless <c>int</c> getter; false when it cannot be read.</summary>
        private static bool TryInvokeInt(MethodInfo method, object instance, out int value)
        {
            value = 0;
            if (!TryInvoke(method, instance, out object result))
                return false;

            if (!(result is int))
            {
                LogUnexpectedReturnType("int", result);
                return false;
            }

            value = (int)result;
            return true;
        }

        /// <summary>Reads a cached parameterless <c>float</c> getter; false when it cannot be read.</summary>
        private static bool TryInvokeFloat(MethodInfo method, object instance, out float value)
        {
            value = 0f;
            if (!TryInvoke(method, instance, out object result))
                return false;

            if (!(result is float))
            {
                LogUnexpectedReturnType("float", result);
                return false;
            }

            value = (float)result;
            return true;
        }

        /// <summary>
        /// Reads a cached parameterless <c>bool</c> getter; returns whether it could be read (so the
        /// caller can tell a real <c>false</c> from an unreadable one), with the value in
        /// <paramref name="value"/> when it could.
        /// </summary>
        private static bool TryInvokeBool(MethodInfo method, object instance, out bool value)
        {
            value = false;
            if (!TryInvoke(method, instance, out object result))
                return false;

            if (!(result is bool))
            {
                LogUnexpectedReturnType("bool", result);
                return false;
            }

            value = (bool)result;
            return true;
        }

        /// <summary>
        /// Feeds a fireplace from storage near the fireplace. Mirrors CraftFromContainers 4.0.7's
        /// <c>Fireplace_Interact_Patch</c> (CFC lines 272-345): the same triggers and guards (line
        /// 280 — the mod's prevent gate, <c>pullFuel</c>, a held button, a missing inventory, and "the
        /// player is already carrying the fuel"), the same "not already full" condition (line 302,
        /// <c>m_maxFuel</c> against the ZDO's <c>"fuel"</c>), the same <c>fuelDisallowTypes</c> match
        /// on the item's prefab name (line 311), and the same range origin (line 304).
        /// <para>
        /// The fire is fed by leaving the fuel in the player's inventory: vanilla's own
        /// <c>Interact</c> then consumes one and invokes <c>RPC_AddFuel</c>, exactly as if the player
        /// had been carrying it. So the item is consumed by the game's own path and this prefix never
        /// writes the fireplace's ZDO.
        /// </para>
        /// <para>
        /// <b>Fill-all.</b> While the fill-all modifier is held the pull asks storage for the whole
        /// remaining capacity instead of a single item — CraftFromContainers' own
        /// <c>amount = pullAll ? Mathf.Min(m_maxFuel - CeilToInt(fuel), stack) : 1</c>, CFC line 317 —
        /// taken as far as storage allows rather than all-or-nothing (see the class remarks on
        /// fill-all), and the "the player already carries the fuel" guard above is lifted, as CFC's
        /// <c>inventory.HaveItem(...) &amp;&amp; !pullAll</c> (line 280) does. Because this port delivers
        /// into the interacting player's inventory and leaves the station to vanilla's own consumption
        /// of one item per interaction, the fetched stack is fed to the fire over the following
        /// interactions rather than all at once.
        /// </para>
        /// </summary>
        [HarmonyPatch(typeof(Fireplace), "Interact")]
        [HarmonyPrefix]
        private static bool FireplaceInteractPrefix(
            Fireplace __instance, Humanoid user, bool hold, ZNetView ___m_nview)
        {
            // CFC's guard line (280): a held button is vanilla's repeat behaviour, not a feed.
            if (__instance == null || ___m_nview == null || hold)
                return true;

            FeatureFlags flags = Enabled(PullSetting.Fuel);
            if (flags == null)
                return true;

            Player player = user as Player;
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null)
                return true;

            ItemDrop fuelItem = __instance.m_fuelItem;
            if (!HasItemData(fuelItem))
                return true;

            // CFC's guard (280) and vanilla's own rule (Fireplace.Interact): while the player already
            // carries fuel, the interaction is vanilla's — it consumes the player's own item. The
            // fill-all modifier lifts this, exactly as CFC's guard does: it reads
            // `inventory.HaveItem(...) && !pullAll` (line 280), so with the modifier held the pull runs
            // even when the player is already carrying some fuel.
            bool fillAll = FillAllHeld(flags);
            if (!StationFeedRules.ShouldAttemptFeed(fillAll, inventory.HaveItem(SharedName(fuelItem))))
                return true;

            ZDO zdo = ___m_nview.GetZDO();
            float fuel = zdo != null ? zdo.GetFloat(ZDOVars.s_fuel, 0f) : 0f;
            int remaining = StationFeedRules.RemainingFuelCapacity(__instance.m_maxFuel, fuel);
            if (remaining <= 0)
                return true;

            string prefab = PrefabName(fuelItem);
            if (StationFeedRules.IsDisallowedType(flags.FuelDisallowTypes, prefab))
                return true;

            // Feed the fire directly - one unit normally, the whole remaining capacity while the fill
            // modifier is held (Task 15c) - so fuel never passes through the player's inventory. Leaving
            // the unit for vanilla to consume instead is what put fuel in a full inventory and, with no
            // room at all, kept the fire unfed. CraftFromContainers feeds at both amounts (its
            // `pullAll ? Min(m_maxFuel - CeilToInt(fuel), available) : 1`, CFC line 317), and reports the
            // interaction consumed once it has fed, which is what swallowing the prefix does here.
            Vector3 origin = __instance.transform.position;
            int fed = FeedStation(player, inventory, ___m_nview, flags,
                StationFeedRules.FillAmount(fillAll, remaining), AddFuelRpc,
                wanted => PullFromStorage(player, origin, prefab, fuelItem, flags, wanted, fillAll) ? prefab : null,
                () => FuelRemaining(__instance, () => zdo != null ? zdo.GetFloat(ZDOVars.s_fuel, 0f) : 0f));
            return fed <= 0;
        }

        /// <summary>
        /// Feeds a smelter's ore queue from storage near the smelter. Mirrors CraftFromContainers
        /// 4.0.7's <c>Smelter_OnAddOre_Patch</c> (CFC lines 459-573): the same triggers and guards
        /// (line 465 — the mod's prevent gate, <c>pullOre</c>, the player using an item on the switch,
        /// and a full queue), the same "the player already has an ore of one of this smelter's input
        /// types, so let vanilla add it" branch (lines 471-482), the same <c>oreDisallowTypes</c> match
        /// on the item's prefab name (lines 497 and 525) and the same range origin (line 485).
        /// <para>
        /// The ore is queued by leaving it in the player's inventory: vanilla's own <c>OnAddOre</c>
        /// finds the ore there (its <c>FindCookableItem</c> call), consumes it and invokes
        /// <c>RPC_AddOre</c>. The pull asks for the first of the smelter's input types that storage can
        /// actually supply, in the order the smelter declares its conversions, which is the same order
        /// CFC walks (<c>Smelter.m_conversion</c>, CFC line 487).
        /// </para>
        /// </summary>
        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        [HarmonyPrefix]
        private static bool SmelterOnAddOrePrefix(
            Smelter __instance, Humanoid user, ItemDrop.ItemData item, ZNetView ___m_nview, ref bool __result)
        {
            // CFC's guard line (465): an item the player is actively using is vanilla's to add.
            if (__instance == null || item != null)
                return true;

            FeatureFlags flags = Enabled(PullSetting.Ore);
            if (flags == null)
                return true;

            Player player = user as Player;
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null)
                return true;

            // Smelter.GetQueueSize() is private in the shipping assembly: reflect, fail closed.
            if (!TryInvokeInt(SmelterGetQueueSize, __instance, out int queueSize))
                return true;

            int remaining = StationFeedRules.RemainingCapacity(__instance.m_maxOre, queueSize);
            if (remaining <= 0)
                return true;

            // The fill-all modifier widens one request to the whole remaining queue (CFC line 503: the
            // same `pullAll ? Min(m_maxOre - GetQueueSize(), available) : 1`). It also lifts the "the
            // player already carries an input ore" guard below, which CFC does NOT do (its input-ore
            // test returns true whatever the modifier says, CFC lines 471-482) — a deliberate
            // divergence, because without it shift-E cannot fill the one station whose stock is ore
            // from ore already in hand (a blast furnace with flametal in the inventory added one per
            // press; reported from play 2026-10-23).
            bool fillAll = FillAllHeld(flags);

            List<ItemDrop> inputs = InputItems(__instance.m_conversion);

            // CFC lines 468-479: if the player's own inventory already holds one of the smelter's
            // input types, vanilla's own OnAddOre will find and queue it, so the released interaction
            // leaves it alone. Vanilla's own probe, Smelter::FindCookableItem(Inventory), is PRIVATE in
            // the shipping assembly and cannot be called directly; this tests the same thing with the
            // public Inventory.GetItem(<shared name>, -1, false) walked over m_conversion, exactly as
            // CFC does.
            if (!StationFeedRules.ShouldAttemptFeed(fillAll, InventoryHasInput(inventory, inputs)))
                return true;

            // Queue the ore directly - one unit normally, the whole remaining queue while the fill
            // modifier is held (Task 15c) - so ore never passes through the player's inventory. Leaving
            // the unit for vanilla to consume instead is what queued ore out of a full inventory and,
            // with no room at all, left the queue unfilled. CraftFromContainers feeds at both amounts
            // (its `pullAll ? Min(m_maxOre - GetQueueSize(), available) : 1`, CFC line 503).
            Vector3 origin = __instance.transform.position;

            // One tally per ore type, so a fill that draws on several ores can say what it added
            // (ShowFedOres). The fuel and food paths pass no tally: they feed one prefab per station.
            var fedByPrefab = new Dictionary<string, int>(StringComparer.Ordinal);
            int fed = FeedStation(player, inventory, ___m_nview, flags,
                StationFeedRules.FillAmount(fillAll, remaining), AddOreRpc,
                wanted => PullFirstInputs(
                    player, inventory, origin, inputs, flags.OreDisallowTypes, flags, wanted, fillAll),
                () => QueueRemaining(__instance),
                (prefab, units) => Tally(fedByPrefab, prefab, units));

            // Swallow the interaction once anything was queued so vanilla does not add a second item,
            // but report it as handled - the value CraftFromContainers sets (CFC line 569) - and say
            // what went in, which vanilla's own "$msg_added" cannot because it never sees this ore.
            if (fed > 0)
            {
                __result = true;
                ShowFedOres(player, inputs, fedByPrefab);
            }

            return fed <= 0;
        }

        /// <summary>
        /// Shows the "what this feed added" HUD line for a smelter's ore: one entry per ore type, in the
        /// smelter's own conversion order, with the item's localized display name. A fill that drew on
        /// several ores therefore names every one of them and its count on a single line, instead of a
        /// message per unit swapping itself off the screen.
        /// </summary>
        /// <param name="inputs">The smelter's own input list, which supplies the order and the names.</param>
        /// <param name="fedByPrefab">Prefab name to units fed; may be empty.</param>
        private static void ShowFedOres(Player player, List<ItemDrop> inputs, Dictionary<string, int> fedByPrefab)
        {
            if (player == null || inputs == null || fedByPrefab == null || fedByPrefab.Count == 0)
                return;

            var fed = new List<StationFeedRules.FedAmount>(fedByPrefab.Count);
            for (int i = 0; i < inputs.Count; i++)
            {
                ItemDrop input = inputs[i];
                string prefab = PrefabName(input);
                if (string.IsNullOrEmpty(prefab))
                    continue;

                int units;
                if (!fedByPrefab.TryGetValue(prefab, out units))
                    continue;

                fed.Add(new StationFeedRules.FedAmount(DisplayName(input), units));
            }

            string message = StationFeedRules.FeedSummary(fed);
            if (string.IsNullOrEmpty(message))
                return;

            // TopLeft is where vanilla prints its own "$msg_added" line, so the feed reads like the
            // game rather than like a mod banner.
            player.Message(MessageHud.MessageType.TopLeft, message, 0, null);
        }

        /// <summary>Adds one pass's units to a per-prefab tally, creating the entry when needed.</summary>
        private static void Tally(Dictionary<string, int> tally, string prefab, int units)
        {
            if (tally == null || string.IsNullOrEmpty(prefab) || units <= 0)
                return;

            int existing;
            tally.TryGetValue(prefab, out existing);
            tally[prefab] = existing + units;
        }

        /// <summary>
        /// Whether <paramref name="inventory"/> holds any of <paramref name="item"/>, matched by the
        /// item's shared name through the public <c>Inventory.GetItem(sharedName, -1, false)</c> — the
        /// same test vanilla's own station discovery makes and CraftFromContainers uses (its lines
        /// 468-479). Quality is deliberately unconstrained: the source half of a shortfall counts by
        /// shared name alone, so it can only ever over-state what is held.
        /// </summary>
        private static bool InventoryHolds(Inventory inventory, ItemDrop item)
        {
            if (inventory == null || !HasItemData(item))
                return false;

            string sharedName = SharedName(item);
            return !string.IsNullOrEmpty(sharedName) && inventory.GetItem(sharedName, -1, false) != null;
        }

        /// <summary>
        /// Feeds a smelter's fuel from storage near the smelter. Mirrors CraftFromContainers 4.0.7's
        /// <c>Smelter_OnAddFuel_Patch</c> (CFC lines 575-660): the same triggers and guards (line 582 —
        /// the mod's prevent gate, <c>pullFuel</c>, the player using an item on the switch, a missing
        /// inventory and "the player already carries the fuel"), the same full-fuel boundary (line 589,
        /// <c>GetFuel() &gt; m_maxFuel - 1</c>, which <see cref="StationFeedRules.RemainingFuelCapacity"/>
        /// expresses), the same <c>fuelDisallowTypes</c> match (line 619) and the same range origin
        /// (line 612).
        /// <para>
        /// As with the fireplace, the fuel is left in the player's inventory for vanilla's own
        /// <c>OnAddFuel</c> to consume and add.
        /// </para>
        /// </summary>
        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        [HarmonyPrefix]
        private static bool SmelterOnAddFuelPrefix(
            Smelter __instance, Humanoid user, ItemDrop.ItemData item, ZNetView ___m_nview, ref bool __result)
        {
            if (__instance == null || item != null)
                return true;

            FeatureFlags flags = Enabled(PullSetting.Fuel);
            if (flags == null)
                return true;

            Player player = user as Player;
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null)
                return true;

            // Smelter.GetFuel() is private in the shipping assembly: reflect, fail closed.
            if (!TryInvokeFloat(SmelterGetFuel, __instance, out float smelterFuel))
                return true;

            int remaining = StationFeedRules.RemainingFuelCapacity(__instance.m_maxFuel, smelterFuel);
            if (remaining <= 0)
                return true;

            ItemDrop fuelItem = __instance.m_fuelItem;
            if (!HasItemData(fuelItem))
                return true;

            // As on the fireplace, the fill-all modifier lifts the "the player already carries the
            // fuel" guard, mirroring CFC's `inventory.HaveItem(...) && !pullAll` (CFC line 582).
            bool fillAll = FillAllHeld(flags);
            if (!StationFeedRules.ShouldAttemptFeed(fillAll, inventory.HaveItem(SharedName(fuelItem))))
                return true;

            string prefab = PrefabName(fuelItem);
            if (StationFeedRules.IsDisallowedType(flags.FuelDisallowTypes, prefab))
                return true;

            // Feed the station directly - one unit normally, the whole remaining capacity while the fill
            // modifier is held (Task 15c) - so fuel never passes through the player's inventory. Leaving
            // the unit for vanilla to consume instead is what put fuel in a full inventory and, with no
            // room at all, left the fire unfed. CraftFromContainers feeds at both amounts (its
            // `pullAll ? Min(m_maxFuel - GetFuel(), available) : 1`, CFC line 624).
            Vector3 origin = __instance.transform.position;
            int fed = FeedStation(player, inventory, ___m_nview, flags,
                StationFeedRules.FillAmount(fillAll, remaining), AddFuelRpc,
                wanted => PullFromStorage(player, origin, prefab, fuelItem, flags, wanted, fillAll) ? prefab : null,
                () => SmelterFuelRemaining(__instance));

            // Swallow the interaction once anything was fed so vanilla does not add a second item, but
            // report it as handled - the value CraftFromContainers sets (CFC line 592).
            if (fed > 0)
                __result = true;

            return fed <= 0;
        }

        /// <summary>
        /// Feeds a shield generator's fuel from storage near the generator. There is no
        /// CraftFromContainers 4.0.7 patch to mirror here — its 4.0.7 release predates the piece — so
        /// this follows <b>this class's own smelter-fuel patch</b> step for step, which is the same
        /// vanilla shape: <c>OnAddFuel(Switch, Humanoid, ItemData)</c> is the method vanilla itself
        /// wires into the add-fuel switch, it takes a null item when the player presses use on the
        /// switch and the held item when the player uses one, and it adds exactly one fuel per call.
        /// <para>
        /// <b>The one difference is the fuel list.</b> A generator declares <c>m_fuelItems</c>, a list,
        /// where a smelter declares a single <c>m_fuelItem</c>; so the "player already carries the
        /// fuel" guard and the request both walk the list, asking storage for the first fuel it can
        /// supply — the same walk the smelter's ore patch makes over <c>m_conversion</c>.
        /// </para>
        /// <para>
        /// As on the fireplace and the smelter, the fill-all modifier widens the request from one unit
        /// to the generator's whole remaining capacity and lifts the "already carrying fuel" guard;
        /// the fuel is fed straight into the generator (one <c>RPC_AddFuel</c> per unit, with the unit
        /// removed from the inventory first) rather than left there for vanilla, so a full inventory
        /// never blocks the feed.
        /// </para>
        /// </summary>
        [HarmonyPatch(typeof(ShieldGenerator), "OnAddFuel")]
        [HarmonyPrefix]
        private static bool ShieldGeneratorOnAddFuelPrefix(
            ShieldGenerator __instance, Humanoid user, ItemDrop.ItemData item, ZNetView ___m_nview,
            ref bool __result)
        {
            // A held fuel item is vanilla's: the player deliberately used it on the switch, and
            // Switch.UseItem already named it, so vanilla adds that one unit exactly as before.
            if (__instance == null || item != null)
                return true;

            FeatureFlags flags = Enabled(PullSetting.Fuel);
            if (flags == null)
                return true;

            Player player = user as Player;
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null)
                return true;

            // ShieldGenerator.GetFuel() is private in the shipping assembly: reflect, fail closed.
            if (!TryInvokeFloat(ShieldGeneratorGetFuel, __instance, out float generatorFuel))
                return true;

            // Vanilla's own full boundary is GetFuel() > m_maxFuel - 1, which is the same boundary
            // RemainingFuelCapacity expresses for the fireplace and the smelter's fuel.
            int remaining = StationFeedRules.RemainingFuelCapacity(__instance.m_maxFuel, generatorFuel);
            if (remaining <= 0)
                return true;

            List<ItemDrop> fuels = FuelItems(__instance.m_fuelItems);
            if (fuels.Count == 0)
                return true;

            // As on the fireplace and the smelter, the fill-all modifier lifts the "the player already
            // carries the fuel" guard. Vanilla's own branch does the same search with HaveItem, so
            // this only decides whether the pull runs, never what vanilla does afterwards.
            bool fillAll = FillAllHeld(flags);
            if (!StationFeedRules.ShouldAttemptFeed(fillAll, InventoryHasInput(inventory, fuels)))
                return true;

            Vector3 origin = __instance.transform.position;
            int fed = FeedStation(player, inventory, ___m_nview, flags,
                StationFeedRules.FillAmount(fillAll, remaining), AddFuelRpc,
                wanted => PullFirstInputs(
                    player, inventory, origin, fuels, flags.FuelDisallowTypes, flags, wanted, fillAll),
                () => ShieldFuelRemaining(__instance));

            // Swallow the interaction once anything was fed so vanilla does not add a second unit, but
            // report it as handled - the same shape the smelter's fuel prefix uses.
            if (fed > 0)
                __result = true;

            return fed <= 0;
        }

        /// <summary>
        /// Appends the fill-all hint to a shield generator's add-fuel hover, which vanilla wires into
        /// the add-fuel switch's <c>m_onHover</c> (so <c>Switch.GetHoverText</c> returns it). The action
        /// word is the generator's own <c>m_add</c> — the same string vanilla puts after the key in
        /// <c>OnHoverAddFuel</c> — rather than a token invented here.
        /// </summary>
        [HarmonyPatch(typeof(ShieldGenerator), "OnHoverAddFuel")]
        [HarmonyPostfix]
        private static void ShieldGeneratorOnHoverAddFuelPostfix(ShieldGenerator __instance, ref string __result)
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.m_add))
                return;

            AppendFillAllHint(ref __result, __instance.m_add, PullSetting.Fuel);
        }

        /// <summary>
        /// Feeds a cooking station's fire from storage near the station. Mirrors CraftFromContainers
        /// 4.0.7's <c>CookingStation_OnAddFuelSwitch_Patch</c> (CFC lines 348-385): the same guards
        /// (line 355 — the mod-enabled flag, <c>pullFuel</c>, the prevent gate, the player using an
        /// item on the switch, "the fire is already full" at <c>GetFuel() &gt; m_maxFuel - 1</c> and
        /// "the player already carries the fuel"), the same <c>fuelDisallowTypes</c> match (line 368)
        /// and the same range origin (line 361). Note that CFC gates this one on <c>pullFuel</c>, not
        /// on <c>pullCooking</c> — a fire is a fire — and so does this port.
        /// </summary>
        [HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")]
        [HarmonyPrefix]
        private static bool CookingStationOnAddFuelSwitchPrefix(
            CookingStation __instance, Humanoid user, ItemDrop.ItemData item, ZNetView ___m_nview,
            ref bool __result)
        {
            if (__instance == null || item != null)
                return true;

            FeatureFlags flags = Enabled(PullSetting.Fuel);
            if (flags == null)
                return true;

            Player player = user as Player;
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null)
                return true;

            // CookingStation.GetFuel() is private in the shipping assembly: reflect, fail closed.
            if (!TryInvokeFloat(CookingStationGetFuel, __instance, out float cookingFuel)
                || StationFeedRules.RemainingFuelCapacity(__instance.m_maxFuel, cookingFuel) <= 0)
                return true;

            ItemDrop fuelItem = __instance.m_fuelItem;
            if (!HasItemData(fuelItem))
                return true;

            if (inventory.HaveItem(SharedName(fuelItem)))
                return true;

            string prefab = PrefabName(fuelItem);
            if (StationFeedRules.IsDisallowedType(flags.FuelDisallowTypes, prefab))
                return true;

            // CraftFromContainers' cooking-station fuel patch has no fill-all branch (its `pullAll` is
            // read only in the three patches above), so this always asks for one - and with a request of
            // one a fill plan and the all-or-nothing plan are the same plan. The unit is fed straight
            // into the fire rather than left in the inventory (Task 15c), as CFC does (CFC line 376).
            Vector3 origin = __instance.transform.position;
            int fed = FeedStation(player, inventory, ___m_nview, flags, 1, AddFuelRpc,
                wanted => PullFromStorage(player, origin, prefab, fuelItem, flags, wanted, false) ? prefab : null,
                () => CookingFuelRemaining(__instance));

            if (fed > 0)
                __result = true;

            return fed <= 0;
        }

        /// <summary>
        /// A fireplace's remaining fuel capacity, or a negative number when it cannot be read. The fuel
        /// is a float on the fireplace's own ZDO, so it is read the same way the request amount is.
        /// </summary>
        private static int FuelRemaining(Fireplace fireplace, Func<float> readFuel)
        {
            if (fireplace == null || readFuel == null)
                return -1;

            return StationFeedRules.RemainingFuelCapacity(fireplace.m_maxFuel, readFuel());
        }

        /// <summary>
        /// A smelter's remaining ore-queue capacity, or a negative number when the private getter cannot
        /// be invoked. Reads the same reflected member the request amount uses.
        /// </summary>
        private static int QueueRemaining(Smelter smelter)
        {
            if (smelter == null || !TryInvokeInt(SmelterGetQueueSize, smelter, out int queueSize))
                return -1;

            return StationFeedRules.RemainingCapacity(smelter.m_maxOre, queueSize);
        }

        /// <summary>
        /// A smelter's remaining fuel capacity, or a negative number when the private getter cannot be
        /// invoked. Reads the same reflected member the request amount uses, so the two cannot disagree.
        /// </summary>
        private static int SmelterFuelRemaining(Smelter smelter)
        {
            if (smelter == null || !TryInvokeFloat(SmelterGetFuel, smelter, out float fuel))
                return -1;

            return StationFeedRules.RemainingFuelCapacity(smelter.m_maxFuel, fuel);
        }

        /// <summary>
        /// A shield generator's remaining fuel capacity, or a negative number when the private getter
        /// cannot be invoked. Reads the same reflected member the request amount uses.
        /// </summary>
        private static int ShieldFuelRemaining(ShieldGenerator generator)
        {
            if (generator == null || !TryInvokeFloat(ShieldGeneratorGetFuel, generator, out float fuel))
                return -1;

            return StationFeedRules.RemainingFuelCapacity(generator.m_maxFuel, fuel);
        }

        /// <summary>
        /// A cooking station's remaining fuel capacity, or a negative number when the private getter
        /// cannot be invoked.
        /// </summary>
        private static int CookingFuelRemaining(CookingStation station)
        {
            if (station == null || !TryInvokeFloat(CookingStationGetFuel, station, out float fuel))
                return -1;

            return StationFeedRules.RemainingFuelCapacity(station.m_maxFuel, fuel);
        }

        /// <summary>
        /// Puts a cookable food item in the player's inventory before the cooking station looks for
        /// one, so the station can cook from storage. Mirrors CraftFromContainers 4.0.7's
        /// <c>CookingStation_FindCookableItem_Patch</c> (CFC lines 388-427): the same guards (line 395
        /// — the mod-enabled flag, <c>pullCooking</c>, the prevent gate, "the station already found
        /// something to cook", an unlit fire when the station requires one, and a full station), the
        /// same <c>oreDisallowTypes</c> match (line 410 — CFC really does test the cooking station's
        /// ingredients against the <b>ore</b> list, and this port mirrors that rather than inventing a
        /// third list) and the same range origin (line 401).
        /// <para>
        /// <b>Patched at <c>OnInteract</c>, not at <c>FindCookableItem</c>.</b> CFC postfixes
        /// <c>FindCookableItem</c> because that is what its target game version called from the
        /// interaction. In this game version <c>FindCookableItem(Inventory)</c> has a second caller,
        /// <c>CookingStation.CanUseItems</c>, which <c>TryGetItems</c> reaches from the radial menu's
        /// config screen — so a postfix there would pull ingredients while merely filling in a menu.
        /// <c>OnInteract</c> is the method both of this version's interaction paths funnel through
        /// (<c>CookingStation.Interact</c> calls it directly, and <c>OnAddFoodSwitch</c> calls it when
        /// the player is not holding an item), and it runs before the station looks for cookable food,
        /// so hooking it preserves the behaviour without the menu-time side effect.
        /// </para>
        /// <para>
        /// The item is found before it is pulled whenever the player already carries one: the guard
        /// tests the player's inventory for one of the station's own inputs with the public
        /// <c>Inventory.GetItem(&lt;shared name&gt;, -1, false)</c> walked over <c>m_conversion</c> — the
        /// public-API shape CraftFromContainers uses, and the equivalent of the <c>__result != null</c>
        /// test CFC's postfix made. Vanilla's own <c>FindCookableItem(Inventory)</c> is <b>private</b> in
        /// the shipping assembly, so it is never called directly here.
        /// </para>
        /// </summary>
        [HarmonyPatch(typeof(CookingStation), "OnInteract")]
        [HarmonyPrefix]
        private static bool CookingStationOnInteractPrefix(CookingStation __instance, Humanoid user)
        {
            if (__instance == null)
                return true;

            FeatureFlags flags = Enabled(PullSetting.Cooking);
            if (flags == null)
                return true;

            // CFC DIVERGENCE: CFC's FindCookableItem postfix has no such guard and can pull an
            // ingredient for an interaction that only wants to take the finished food out. This patch
            // runs earlier (OnInteract) and skips the pull when the station already holds a done item,
            // which can only make it pull less; the station is fed on the next interaction.
            // HaveDoneItem() is private in the shipping assembly: reflect, fail closed.
            if (!TryInvokeBool(CookingStationHaveDoneItem, __instance, out bool doneItem) || doneItem)
                return true;

            Player player = user as Player;
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null)
                return true;

            // CFC's guard line (395): a station with nowhere to put food, or whose fire is out while
            // the station requires one, is not fed. GetFreeSlot() and IsFireLit() are private in the
            // shipping assembly: reflect, fail closed.
            if (!TryInvokeInt(CookingStationGetFreeSlot, __instance, out int freeSlot) || freeSlot == -1)
                return true;

            if (__instance.m_requireFire
                && (!TryInvokeBool(CookingStationIsFireLit, __instance, out bool fireLit) || !fireLit))
                return true;

            List<ItemDrop> inputs = InputItems(__instance.m_conversion);

            // CFC's guard (395): the player already carries one of the station's own inputs, so
            // vanilla's own food search will find it and this pulls nothing. Vanilla's probe,
            // CookingStation::FindCookableItem(Inventory), is PRIVATE in the shipping assembly; the
            // public Inventory.GetItem(<shared name>, -1, false) over m_conversion tests the same thing
            // — the mirror of CFC's smelter-ore loop (lines 468-479), which CFC reuses here.
            if (InventoryHasInput(inventory, inputs))
                return true;

            // CraftFromContainers' cooking-station patch has no fill-all branch either, so this
            // always asks for one.
            PullFirstInputs(
                player, inventory, __instance.transform.position,
                inputs, flags.OreDisallowTypes, flags, 1, false);
            return true;
        }

        /// <summary>
        /// Loads a turret from storage near the turret when the player has no ammunition for it.
        /// Mirrors CraftFromContainers 4.0.7's <c>Turret_UseItem_Patch</c> (CFC lines 1129-1156): the
        /// same guards (line 1135 — the mod-enabled flag, the prevent gate, an item the player is
        /// already using, and a user that is not a player), the same "no ammo of that type in the
        /// inventory" condition, and the same remedy: put an ammunition item in the player's
        /// inventory and let vanilla's own <c>UseItem</c> take it from there (CFC line 1152 is the
        /// very same "pull into the player's inventory" call, its <c>PullResources</c>).
        /// <para>
        /// <b>There is no turret flag, and this port does not invent one.</b> CraftFromContainers
        /// binds <c>pullFuel</c>, <c>pullOre</c> and <c>pullCooking</c> (CFC lines 114-116) and
        /// nothing for the turret — its turret patch's only gates are <c>modEnabled</c> and
        /// <c>AllowByKey()</c> (line 1137) — and this project's <c>ConfigModel</c> likewise has no
        /// turret key. Turret loading is therefore gated on the mod-enabled flag and the prevent gate,
        /// matching CraftFromContainers exactly.
        /// </para>
        /// <para>
        /// <b>The ammunition type is <c>GetAmmoType()</c>, unconditionally.</b> That is the prefab
        /// lookup CraftFromContainers itself performs for this case (CFC line 1145); CFC passes the
        /// configured <c>m_ammoType</c> only to its <i>inventory</i> lookup (line 1140), which vanilla
        /// performs here because this patch hands the item back to vanilla's own <c>UseItem</c>. The
        /// probe shows <c>Turret::GetAmmoType</c> reads the ZDO string <c>s_ammoType</c> with
        /// <c>m_ammoType</c> as its default, while <c>Turret::GetAmmo</c> reads the independent ZDO int
        /// <c>s_ammo</c>, so <c>GetAmmoType()</c> can differ from <c>m_ammoType</c> while
        /// <c>GetAmmo() == 0</c> — a turret emptied of a non-default type keeps that type's name in
        /// its ZDO. Unconditional <c>GetAmmoType()</c> is therefore the name vanilla's <c>UseItem</c>
        /// will accept, and the type this patch looks for in storage.
        /// </para>
        /// </summary>
        [HarmonyPatch(typeof(Turret), "UseItem")]
        [HarmonyPrefix]
        private static bool TurretUseItemPrefix(Turret __instance, Humanoid user, ItemDrop.ItemData item)
        {
            // CFC's guard line (1137): an item already in hand is vanilla's, and so is a non-player user.
            if (__instance == null || item != null)
                return true;

            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || !flags.ModEnabled)
                return true;

            if (!PullKeyPatches.AllowByKey())
                return true;

            Player player = user as Player;
            if (player == null)
                return true;

            // A turret with no room for ammunition is never supplied: vanilla's own full/other-ammo
            // branches decide what to report, and nothing is taken from storage for an item that could
            // not be loaded.
            if (StationFeedRules.RemainingCapacity(__instance.m_maxAmmo, __instance.GetAmmo()) <= 0)
                return true;

            // GetAmmoType() is public in the shipping assembly and reads the turret's own ZDO
            // "ammoType" (default m_ammoType), so it is the prefab name to look up in both the empty
            // and loaded cases — exactly the prefab CFC resolves at its line 1145.
            string ammoType = __instance.GetAmmoType();
            if (string.IsNullOrEmpty(ammoType))
                return true;

            ItemDrop ammo = AmmoItem(ammoType);
            if (ammo == null)
            {
                Debug(flags, "Turret: no item prefab for ammunition type '" + ammoType + "'; nothing pulled.");
                return true;
            }

            PullFromStorage(player, __instance.transform.position, ammoType, ammo, flags, 1, false);
            return true;
        }

        /// <summary>The station add RPC that takes no arguments: fuel, for a fireplace, smelter or cooking station.</summary>
        private const string AddFuelRpc = "RPC_AddFuel";

        /// <summary>The station add RPC for a smelter's ore queue, called with the prefab name and the item's cheated flag.</summary>
        private const string AddOreRpc = "RPC_AddOre";

        /// <summary>
        /// The fill path's whole job: repeatedly pull a batch from storage into the player's inventory and
        /// feed that batch into the station, until the station is full, storage is exhausted, or a pass
        /// makes no progress. Returns how many units reached the station.
        /// <para>
        /// <b>Batched on purpose.</b> One pass can only move what the inventory can accept at that
        /// moment, so a station needing a hundred ore still fills from a base whose player has three
        /// free slots; batching is what makes the fill independent of inventory size rather than one
        /// large request that fails outright. Progress is guaranteed - every pass either feeds at least
        /// one unit or ends the loop - and the total fed is bounded by the station's remaining capacity,
        /// so the loop always terminates.
        /// </para>
        /// <para>
        /// <b>Why the inventory is involved at all.</b> The items have to come out of storage through
        /// <see cref="MultiUserChestBridge"/> - the only sanctioned removal path (Ruling 5) - and that
        /// path delivers into an <c>Inventory</c>. A fireplace's fuel is a float on its ZDO and a
        /// smelter's queue is a count, so a station cannot be a removal destination directly. The
        /// inventory is a one-pass staging area here and nothing lingers in it. (See
        /// <c>docs/sdd/.../task-15c-design.md</c>.)
        /// </para>
        /// </summary>
        /// <param name="pullOneBatch">
        /// One pull step: given the number of units this pass may ask for, pulls into the inventory and
        /// returns the <b>prefab name</b> it left there - or null when nothing arrived <b>and the
        /// inventory holds none of the station's inputs</b>, which ends the fill. A pull storage cannot
        /// improve on is not a failure: the batch still names the input the player is already carrying,
        /// so a held fill feeds that stock too. The name is returned rather than assumed because a
        /// single-prefab station (a fireplace's fuel) and a multi-input one (a smelter's ore) ask
        /// storage differently, and the station's add RPC has to be told which input the smelter was
        /// actually fed.
        /// </param>
        /// <param name="recordFed">
        /// Optional: receives each pass's prefab name and the units it fed, so a caller can report what
        /// went in. The smelter's ore patch tallies by ore type for <see cref="ShowFedOres"/>; the fuel
        /// and food paths pass null.
        /// </param>
        private static int FeedStation(
            Player player, Inventory inventory, ZNetView nview, FeatureFlags flags,
            int remaining, string rpcName, Func<int, string> pullOneBatch, Func<int> remainingNow,
            Action<string, int> recordFed = null)
        {
            if (player == null || inventory == null || nview == null || flags == null || remaining <= 0 ||
                pullOneBatch == null)
                return 0;

            int fedTotal = 0;
            while (fedTotal < remaining)
            {
                string prefab = pullOneBatch(remaining - fedTotal);
                if (prefab == null)
                    break;

                int fed = FeedStationFromInventory(
                    inventory, nview, prefab, remaining - fedTotal, rpcName, remainingNow);
                if (fed <= 0)
                    break;

                fedTotal += fed;

                if (recordFed != null)
                    recordFed(prefab, fed);
            }

            return fedTotal;
        }

        /// <summary>
        /// Moves up to <paramref name="maxUnits"/> units of <paramref name="prefab"/> from the inventory
        /// into the station: one unit is removed from the inventory and the station's own add RPC is
        /// invoked for it, once per unit. Returns how many units were fed.
        /// <para>
        /// <b>No duplication is possible.</b> This method removes the unit itself and the RPC only
        /// advances station-side state, so exactly one unit leaves the inventory per unit the station
        /// gains. That is why the station's own add <i>method</i> is not used: it is private in the
        /// shipping assembly, and the public RPC is the shape CraftFromContainers 4.0.7 invokes.
        /// Both RPC names were verified against the shipping assembly's own literals; the argument
        /// shapes are CFC's.
        /// </para>
        /// </summary>
        /// <param name="remainingNow">
        /// Reads the station's remaining capacity again, or returns a negative number when it cannot be
        /// read. Used to confirm each unit actually reached the station before another is removed.
        /// </param>
        private static int FeedStationFromInventory(
            Inventory inventory, ZNetView nview, string prefab, int maxUnits, string rpcName,
            Func<int> remainingNow)
        {
            if (inventory == null || nview == null || string.IsNullOrEmpty(prefab) || maxUnits <= 0)
                return 0;

            int fed = 0;
            while (fed < maxUnits)
            {
                // Re-located each pass: the previous removal may have emptied the stack entirely.
                ItemDrop.ItemData stack = FindInventoryStack(inventory, prefab);
                if (stack == null)
                    break;

                // Read before the removal: the RPC needs the cheated flag the item was created with,
                // and the stack may be gone afterwards.
                bool cheated = stack.m_cheated;
                string sharedName = stack.m_shared != null ? stack.m_shared.m_name : null;

                int before = CountInventory(inventory, prefab);

                // Removed by NAME, not by stack reference. Vanilla's own consumption and
                // CraftFromContainers both use Inventory.RemoveItem(<shared name>, <amount>), and a
                // reference-based removal that quietly does nothing is exactly what put station fuel
                // in a player's backpack in play (reported 2026-10-12).
                inventory.RemoveItem(sharedName, 1);

                int after = CountInventory(inventory, prefab);
                if (after >= before)
                {
                    // Never leave a removal that did not take effect invisible, and never loop on it:
                    // the unit is still where it was, so the station would starve while the inventory
                    // filled up.
                    Log.LogWarning(ModInfo.ModName + ": the station feed could not remove '" + prefab +
                                   "' from the inventory, so it was left where it was and the feed stopped " +
                                   "rather than repeating a removal that is not taking effect.");
                    break;
                }

                // The station's own remaining capacity before this unit, so the check below compares like
                // with like. Comparing it against maxUnits was the bug: maxUnits is this call's unit
                // bound (1 on the single-item path), not a capacity, so a station with any room at all
                // read as "did not take it" - and the interaction was then left to vanilla, which printed
                // its own "you don't have any X" message after the item had already been fed.
                int stationBefore = remainingNow != null ? remainingNow() : -1;

                nview.InvokeRPC(rpcName, StationRpcArgs(rpcName, prefab, cheated));

                // Confirm the station took it BEFORE removing another unit. A removal whose RPC does not
                // land destroys an item, and losing a hundred of them silently would be far worse than
                // the failure itself; checked per unit, the most this can ever cost is one. A station
                // gains exactly one unit of capacity per unit fed, so its remaining count must fall.
                if (stationBefore >= 0)
                {
                    int stationAfter = remainingNow();
                    if (stationAfter >= 0 && stationAfter >= stationBefore)
                    {
                        Log.LogError(ModInfo.ModName + ": the station did not take a fed '" + prefab +
                                     "' (its remaining capacity did not fall), so the feed has stopped " +
                                     "after losing at most one unit rather than continuing to remove items " +
                                     "that are not arriving.");
                        break;
                    }
                }

                fed++;
            }

            return fed;
        }

        /// <summary>
        /// The argument shape a station add RPC takes. Ore names its prefab and carries the item's
        /// cheated flag; fuel takes nothing. Both are CraftFromContainers 4.0.7's own shapes (CFC lines
        /// 533-536 for ore, 341 for fuel).
        /// </summary>
        private static object[] StationRpcArgs(string rpcName, string prefab, bool cheated)
        {
            return string.Equals(rpcName, AddOreRpc, StringComparison.Ordinal)
                ? new object[] { prefab, cheated }
                : new object[0];
        }

        /// <summary>
        /// How many items of <paramref name="prefab"/> the inventory holds, counted on the item's prefab
        /// name so it matches <see cref="FindInventoryStack"/> exactly (Ruling 1: storage is keyed by
        /// prefab name everywhere in this mod). Used by the station feed to confirm a removal took
        /// effect. Deliberately not <c>Inventory.CountItems</c>, which matches the localized shared name
        /// and whose quality argument this mod would otherwise be guessing at.
        /// </summary>
        private static int CountInventory(Inventory inventory, string prefab)
        {
            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            if (items == null)
                return 0;

            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData item = items[i];
                if (item == null || item.m_dropPrefab == null)
                    continue;

                if (string.Equals(item.m_dropPrefab.name, prefab, StringComparison.Ordinal))
                    total += item.m_stack;
            }

            return total;
        }

        /// <summary>
        /// The largest stack of <paramref name="prefab"/> in <paramref name="inventory"/>, or null.
        /// Matched on the item's prefab name, the identity every other part of this mod carries for
        /// storage (Ruling 1); the removal itself goes by stack reference, so no name lookup is needed
        /// for it. Choosing the largest keeps the common case to one pass.
        /// </summary>
        private static ItemDrop.ItemData FindInventoryStack(Inventory inventory, string prefab)
        {
            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            if (items == null)
                return null;

            ItemDrop.ItemData best = null;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData candidate = items[i];
                if (candidate == null || candidate.m_dropPrefab == null)
                    continue;

                if (!string.Equals(candidate.m_dropPrefab.name, prefab, StringComparison.Ordinal))
                    continue;

                if (best == null || candidate.m_stack > best.m_stack)
                    best = candidate;
            }

            return best;
        }

        /// <summary>
        /// Pulls <paramref name="amount"/> of <paramref name="prefab"/> into <paramref name="player"/>'s
        /// inventory from storage in range of <paramref name="origin"/> (the station), through the
        /// mod's one pull entry point. Returns whether the destination now holds what was asked for —
        /// which includes the case where storage did not have to contribute anything because the
        /// inventory already held it all. It returns false only when the pull could not be satisfied
        /// (storage cannot cover it and the inventory does not hold it, or the inventory cannot take
        /// what would be delivered).
        /// <para>
        /// <b>Why "satisfied" rather than "something moved".</b> The planner asks storage only for the
        /// shortfall below what the inventory already holds, so a fill with the stock already in hand
        /// produces no plan lines at all. Treating that as a failed pull made the feed stop before
        /// feeding anything, which left the interaction to vanilla — one item per press, whatever the
        /// modifier said (reported from play 2026-10-23 against a blast furnace holding flametal).
        /// </para>
        /// <para>
        /// <paramref name="amount"/> is the station's whole remaining capacity while the fill-all
        /// modifier is held and <c>1</c> otherwise (see <see cref="StationFeedRules.FillAmount"/>). The
        /// planner asks storage only for the shortfall below what the inventory already holds, so an
        /// amount the player already carries pulls nothing extra.
        /// </para>
        /// <para>
        /// <paramref name="fillUpTo"/> is the modifier's own flag and selects the <b>fill</b> plan
        /// (<see cref="PullEngine.PullToInventoryFill"/>): the request asks for the whole remaining
        /// capacity but accepts <c>min(available, requested)</c>, so a base that can only spare part
        /// of it still feeds the station that part (Task 15b fix round 1, R31). Without it — every
        /// unmodified interaction and every single-item call site — the request is the all-or-nothing
        /// pull it has always been, so releasing the key behaves exactly as before.
        /// </para>
        /// </summary>
        private static bool PullFromStorage(
            Player player, Vector3 origin, string prefab, ItemDrop item, FeatureFlags flags, int amount,
            bool fillUpTo)
        {
            if (player == null || item == null || string.IsNullOrEmpty(prefab) || flags == null || amount <= 0)
                return false;

            Inventory inventory = player.GetInventory();
            if (inventory == null)
                return false;

            // The station's item, at the quality plane a station consumes at (vanilla's requirement
            // arithmetic reports m_amount for quality <= 1, which is what a station feed is). An upgrader
            // resource is never planned for a station: an Idol is only ever consumed at a crafting
            // station, so allowUpgraderResources is false.
            var requirements = new[]
            {
                new Piece.Requirement { m_amount = amount, m_resItem = item }
            };

            bool requested;
            bool pulled = fillUpTo
                ? PullEngine.PullToInventoryFill(
                    requirements, 0, 1, inventory, Origin(origin), flags, false, out requested)
                : PullEngine.PullToInventory(
                    requirements, 0, 1, inventory, Origin(origin), flags, false, out requested);

            if (pulled && requested)
                Debug(flags, "Station: pulled up to " + amount + "x '" + prefab +
                    "' from storage into " + player.name + "'s inventory.");

            // `pulled` alone: a pull the destination already covers is a satisfied no-op, and the feed
            // must carry on to consume what the player is holding. Requiring `requested` here is what
            // made a held fill stop at zero when the stock was in the inventory rather than a chest.
            //
            // The value says "the destination is satisfied for THIS prefab", which is NOT the same as
            // "this prefab is now in the inventory" (a full inventory caps the request to nothing). A
            // caller that walks several candidate items must therefore decide where to stop from the
            // inventory itself — see PullFirstInputs and StationFeedRules.ShouldStopInputWalk.
            return pulled;
        }

        /// <summary>
        /// Pulls <paramref name="amount"/> of the first of a station's <paramref name="inputs"/> that the
        /// destination inventory ends up holding, walking them in the station's own order, and returns
        /// that input's prefab name — or null when it ends up holding none.
        /// <para>
        /// <b>The walk stops on the destination, never on the pull's result.</b> A pull that moved
        /// nothing is still a success when the destination already covers the request (a satisfied
        /// no-op, see <see cref="PullFromStorage"/>), so treating "the pull returned true" as "stop
        /// here" skips every input after the first. 1.0.6 shipped exactly that: a smelter whose storage
        /// held tin but no copper walked no further than copper and fed nothing, so tin, iron and silver
        /// could not be fed from storage at all (reported from play 2026-10-23). A blast furnace — one
        /// conversion — hid it. Only a destination that actually holds the input may end the walk; see
        /// <see cref="StationFeedRules.ShouldStopInputWalk"/>.
        /// </para>
        /// <para>
        /// Skips any input whose prefab name is on <paramref name="disallowTypes"/>, and takes nothing
        /// when the request is zero. Walking the station's own order is CraftFromContainers' loop over
        /// <c>m_conversion</c> (its lines 487-510): a smelter that accepts several ores asks for the
        /// first one the base can actually provide, or the first one the player is already carrying.
        /// </para>
        /// </summary>
        /// <param name="inventory">The destination the stock must end up in; null feeds nothing.</param>
        /// <param name="fillUpTo">Whether the request is a fill: see <see cref="PullFromStorage"/>.</param>
        /// <returns>The prefab name of the input to feed, or null when there is nothing to feed.</returns>
        private static string PullFirstInputs(
            Player player, Inventory inventory, Vector3 origin, List<ItemDrop> inputs,
            string disallowTypes, FeatureFlags flags, int amount, bool fillUpTo)
        {
            if (amount <= 0 || inventory == null)
                return null;

            for (int i = 0; i < inputs.Count; i++)
            {
                ItemDrop input = inputs[i];
                if (input == null)
                    continue;

                string prefab = PrefabName(input);
                if (string.IsNullOrEmpty(prefab))
                    continue;

                if (StationFeedRules.IsDisallowedType(disallowTypes, prefab))
                    continue;

                // Whatever this attempt did — moved items, or was a satisfied no-op — the only question
                // for the walk is whether the inventory now holds this input.
                PullFromStorage(player, origin, prefab, input, flags, amount, fillUpTo);

                if (StationFeedRules.ShouldStopInputWalk(InventoryHolds(inventory, input)))
                    return prefab;
            }
            return null;
        }

        /// <summary>The input item of each of a smelter's conversions, in the order the smelter declares them.</summary>
        private static List<ItemDrop> InputItems(List<Smelter.ItemConversion> conversions)
        {
            var inputs = new List<ItemDrop>();
            if (conversions == null)
                return inputs;

            for (int i = 0; i < conversions.Count; i++)
            {
                Smelter.ItemConversion conversion = conversions[i];
                inputs.Add(conversion != null ? conversion.m_from : null);
            }
            return inputs;
        }

        /// <summary>The input item of each of a cooking station's conversions, in the order the station declares them.</summary>
        private static List<ItemDrop> InputItems(List<CookingStation.ItemConversion> conversions)
        {
            var inputs = new List<ItemDrop>();
            if (conversions == null)
                return inputs;

            for (int i = 0; i < conversions.Count; i++)
            {
                CookingStation.ItemConversion conversion = conversions[i];
                inputs.Add(conversion != null ? conversion.m_from : null);
            }
            return inputs;
        }

        /// <summary>
        /// The usable fuel prefabs of a shield generator, in the order the generator declares them.
        /// A generator holds a list where a smelter holds one item, so the list is filtered once here
        /// (dropping a null or an entry with no item data) and then walked by the same helpers the
        /// smelter's inputs use.
        /// </summary>
        private static List<ItemDrop> FuelItems(List<ItemDrop> fuelItems)
        {
            var fuels = new List<ItemDrop>();
            if (fuelItems == null)
                return fuels;

            for (int i = 0; i < fuelItems.Count; i++)
            {
                if (HasItemData(fuelItems[i]))
                    fuels.Add(fuelItems[i]);
            }
            return fuels;
        }

        /// <summary>
        /// The hover-hint action word for adding fuel: the game's own smelter token, whose text is the
        /// word "Add" — the shipping assembly uses it in a hover hint of exactly this shape, so it reads
        /// correctly on a fire too. A fireplace has no hover token of its own, and this is the word
        /// CraftFromContainers uses for a smelter's fuel hint, so the two hovers read alike.
        /// </summary>
        private const string AddFuelHintToken = "$piece_smelter_add";

        /// <summary>
        /// Appends the fill-all hint to the smelter's fuel hover. Mirrors CraftFromContainers 4.0.7's
        /// <c>Smelter_OnHoverAddFuel_Patch</c> (CFC lines 429-440): the same mod-enabled and
        /// <c>pullFuel</c> gates, the same "only when the modifier is bound" check, and the same
        /// localized <c>[key+Use] … max</c> suffix — with CFC's <c>$piece_smelter_add</c> action word,
        /// now shared with the fireplace hint below through <see cref="AddFuelHintToken"/>.
        /// </summary>
        [HarmonyPatch(typeof(Smelter), "OnHoverAddFuel")]
        [HarmonyPostfix]
        private static void SmelterOnHoverAddFuelPostfix(ref string __result)
        {
            AppendFillAllHint(ref __result, AddFuelHintToken, PullSetting.Fuel);
        }

        /// <summary>
        /// Appends the fill-all hint to a fireplace's hover text, so a fire says how to fill it.
        /// CraftFromContainers puts this hint on the smelter's two hovers only (CFC lines 431 and 446),
        /// which left a fire with no way to discover the key — reported from play. It goes through the
        /// same helper and the same action word as the smelter's fuel hint, so the two cannot drift.
        /// </summary>
        [HarmonyPatch(typeof(Fireplace), "GetHoverText")]
        [HarmonyPostfix]
        private static void FireplaceGetHoverTextPostfix(Fireplace __instance, ref string __result)
        {
            AppendFillAllHint(ref __result, AddFuelHintToken, PullSetting.Fuel);
        }

        /// <summary>
        /// Appends the fill-all hint to the smelter's ore hover, which CraftFromContainers 4.0.7 does
        /// at CFC lines 444-455: the same gates as the fuel hint, but with the smelter's own
        /// <c>m_addOreTooltip</c> (public in the shipping assembly) in place of the hard-coded token.
        /// </summary>
        [HarmonyPatch(typeof(Smelter), "OnHoverAddOre")]
        [HarmonyPostfix]
        private static void SmelterOnHoverAddOrePostfix(Smelter __instance, ref string __result)
        {
            AppendFillAllHint(
                ref __result, __instance != null ? __instance.m_addOreTooltip : null, PullSetting.Ore);
        }

        /// <summary>
        /// Appends the fill-all hint to a smelter hover when the mod is on, the matching pull setting is
        /// on and the modifier is bound. CraftFromContainers gates these two hovers on <c>modEnabled</c>
        /// and <c>pullFuel</c>/<c>pullOre</c> only (CFC lines 431 and 446) — not on the prevent key — and
        /// so does this port, because the hint describes what the key <i>would</i> do rather than acting.
        /// Nothing is appended when the string is unbound or the localizer is unavailable, so a hint
        /// never displaces the game's own hover text.
        /// </summary>
        private static void AppendFillAllHint(ref string result, string action, PullSetting setting)
        {
            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || !flags.ModEnabled || !PullSettingOn(flags, setting))
                return;

            string hint = StationFeedRules.FillAllHint(flags.FillAllModKey, action);
            if (hint == null || Localization.instance == null)
                return;

            result += Localization.instance.Localize(hint);
        }

        /// <summary>
        /// The station's own feeding setting, read from the shared snapshot: the mod must be enabled
        /// and the named flag must be on. Null when the snapshot is missing or the setting is off, so
        /// each patch has one line deciding whether it may act at all. A <see cref="FeatureFlags"/> is
        /// never constructed here — the snapshot the config layer filled in is the only source.
        /// <para>
        /// The prevent gate (<see cref="PullKeyPatches.AllowByKey"/>) is checked separately, by the
        /// patches, because CraftFromContainers checks it separately too.
        /// </para>
        /// </summary>
        private static FeatureFlags Enabled(PullSetting setting)
        {
            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || !flags.ModEnabled)
                return null;

            if (!PullKeyPatches.AllowByKey())
                return null;

            return PullSettingOn(flags, setting) ? flags : null;
        }

        /// <summary>Whether the named station setting is on. The one place the three settings map to their flags.</summary>
        private static bool PullSettingOn(FeatureFlags flags, PullSetting setting)
        {
            if (flags == null)
                return false;

            switch (setting)
            {
                case PullSetting.Fuel:
                    return flags.PullFuel;
                case PullSetting.Ore:
                    return flags.PullOre;
                case PullSetting.Cooking:
                    return flags.PullCooking;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the fill-all modifier (<c>"Fill all mod key"</c>) is held this frame. It is the same
        /// held-key read every other modifier uses (<see cref="PullKeyPatches.KeyHeld"/>), so an unbound
        /// or unrecognised key reads as "not held" rather than throwing.
        /// </summary>
        private static bool FillAllHeld(FeatureFlags flags)
        {
            return flags != null && PullKeyPatches.KeyHeld(flags.FillAllModKey);
        }

        /// <summary>Which station setting gates a patch.</summary>
        private enum PullSetting
        {
            /// <summary><c>PullFuel</c> — a fireplace's fuel, a smelter's fuel, a cooking station's fire, a shield generator's fuel.</summary>
            Fuel,

            /// <summary><c>PullOre</c> — a smelter's ore queue.</summary>
            Ore,

            /// <summary><c>PullCooking</c> — a cooking station's food.</summary>
            Cooking
        }

        /// <summary>The prefab name of an item — the identifier every storage lookup is keyed by.</summary>
        private static string PrefabName(ItemDrop item)
        {
            if (item == null)
                return null;

            GameObject go = item.gameObject;
            return go != null ? go.name : null;
        }

        /// <summary>The item's shared name, the identity vanilla consumes a station's fuel by.</summary>
        private static string SharedName(ItemDrop item)
        {
            return item != null && item.m_itemData != null && item.m_itemData.m_shared != null
                ? item.m_itemData.m_shared.m_name
                : null;
        }

        /// <summary>
        /// The item's name as the game shows it: the shared name through the localizer, so the feed
        /// summary reads "Scrap iron" in English and in whatever language the player runs. Falls back
        /// to the shared name, then to the prefab name, rather than showing a null.
        /// </summary>
        private static string DisplayName(ItemDrop item)
        {
            string sharedName = SharedName(item);
            if (string.IsNullOrEmpty(sharedName))
                return PrefabName(item);

            return Localization.instance != null ? Localization.instance.Localize(sharedName) : sharedName;
        }

        /// <summary>
        /// Whether <paramref name="inventory"/> already holds one of the station's own input items,
        /// matched by the item's shared name. This is the public-API equivalent of vanilla's own
        /// <c>FindCookableItem(Inventory)</c> probe — a search of an inventory for the station's inputs
        /// — which is <b>private</b> in the shipping assembly on <c>Smelter</c>, <c>CookingStation</c>
        /// and <c>Fermenter</c> alike and therefore cannot be called directly from here.
        /// CraftFromContainers does exactly this for its smelter-ore patch: it walks the station's
        /// <c>m_conversion</c> and tests the player's inventory with the public
        /// <c>Inventory.GetItem(&lt;shared name&gt;, -1, false)</c> (CFC <c>BepInExPlugin.cs</c> lines
        /// 468-479). <c>m_conversion</c> and <c>Inventory.GetItem</c> are both public in the shipping
        /// assembly, so this removes the non-public dependency rather than reflecting it.
        /// </summary>
        private static bool InventoryHasInput(Inventory inventory, List<ItemDrop> inputs)
        {
            if (inventory == null || inputs == null)
                return false;

            for (int i = 0; i < inputs.Count; i++)
            {
                string sharedName = SharedName(inputs[i]);
                if (string.IsNullOrEmpty(sharedName))
                    continue;

                if (inventory.GetItem(sharedName, -1, false) != null)
                    return true;
            }
            return false;
        }

        /// <summary>Whether an item carries the item data this mod reads its names from.</summary>
        private static bool HasItemData(ItemDrop item)
        {
            return item != null && item.m_itemData != null && item.m_itemData.m_shared != null;
        }

        /// <summary>The item prefab behind an ammunition type name, as vanilla itself resolves it.</summary>
        private static ItemDrop AmmoItem(string ammoType)
        {
            if (string.IsNullOrEmpty(ammoType) || ZNetScene.instance == null)
                return null;

            GameObject prefab = ZNetScene.instance.GetPrefab(ammoType);
            return prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        }

        /// <summary>A world position as the pure <see cref="Vec3"/> the range maths uses.</summary>
        private static Vec3 Origin(Vector3 position)
        {
            return new Vec3(position.x, position.y, position.z);
        }

        /// <summary>Logs a line only when debug logging is on, so the default is quiet.</summary>
        private static void Debug(FeatureFlags flags, string message)
        {
            if (flags != null && flags.IsDebug)
                Log.LogInfo(message);
        }
    }
}
