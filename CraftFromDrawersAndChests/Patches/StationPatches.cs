using System.Collections.Generic;
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
    /// <c>PullEngine</c> the station's own <c>transform.position</c> as the origin, which is exactly
    /// what CraftFromContainers 4.0.7 does — every one of its station patches collects its sources
    /// with <c>GetNearbyContainers(__instance.transform.position)</c> (CFC <c>BepInExPlugin.cs</c>
    /// lines 304, 361, 401, 485, 612). So a chest next to the smelter feeds it even while the player
    /// stands further away, and a chest out of the smelter's own range cannot.
    /// </para>
    /// <para>
    /// <b>The pull is the mod's existing one, unchanged.</b> Each patch makes one call to
    /// <see cref="PullEngine.PullToInventory"/> — the same shortfall planner, the same source
    /// collection, the same all-or-nothing take sequencing and the same destination-capacity proof
    /// the crafting, building and ctrl-click pulls use — asking for a single item whose destination
    /// is the <i>interacting player's</i> inventory. Nothing here re-implements any of that. A
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
    /// snapshot — <c>PullFuel</c> for the fireplace, the smelter's fuel and the cooking station's
    /// fuel, <c>PullOre</c> for the smelter's ore, <c>PullCooking</c> for the cooking station's food —
    /// and the mod-enabled flag, and obeys <see cref="PullKeyPatches.AllowByKey"/>, the same prevent
    /// gate the pull key obeys. CraftFromContainers gates its turret patch on the mod-enabled flag and
    /// the prevent key alone: it has <b>no</b> turret setting, and this port adds none (see the
    /// turret patch's remarks).
    /// </para>
    /// <para>
    /// <b>Fill-all is not here.</b> CraftFromContainers reads its fill-all modifier (<c>pullAll</c>)
    /// in these patches and takes the whole remaining capacity when it is held; that modifier is
    /// Task 15b. This pass takes one item per interaction, which is CFC's own behaviour when the
    /// modifier is not held.
    /// </para>
    /// </summary>
    internal static class StationPatches
    {
        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

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
            // carries fuel, the interaction is vanilla's — it consumes the player's own item.
            if (inventory.HaveItem(SharedName(fuelItem)))
                return true;

            ZDO zdo = ___m_nview.GetZDO();
            float fuel = zdo != null ? zdo.GetFloat(ZDOVars.s_fuel, 0f) : 0f;
            if (StationFeedRules.RemainingFuelCapacity(__instance.m_maxFuel, fuel) <= 0)
                return true;

            string prefab = PrefabName(fuelItem);
            if (StationFeedRules.IsDisallowedType(flags.FuelDisallowTypes, prefab))
                return true;

            PullOne(player, __instance.transform.position, prefab, fuelItem, flags);
            return true;
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
        private static bool SmelterOnAddOrePrefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item)
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

            if (StationFeedRules.RemainingCapacity(__instance.m_maxOre, __instance.GetQueueSize()) <= 0)
                return true;

            // CFC lines 468-479: if the player's own inventory already holds one of the smelter's
            // input types, vanilla's own OnAddOre will find and queue it, so this leaves it alone.
            if (__instance.FindCookableItem(inventory) != null)
                return true;

            PullFirstInputs(
                player, __instance.transform.position,
                InputItems(__instance.m_conversion), flags.OreDisallowTypes, flags);
            return true;
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
        private static bool SmelterOnAddFuelPrefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item)
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

            if (StationFeedRules.RemainingFuelCapacity(__instance.m_maxFuel, __instance.GetFuel()) <= 0)
                return true;

            ItemDrop fuelItem = __instance.m_fuelItem;
            if (!HasItemData(fuelItem))
                return true;

            if (inventory.HaveItem(SharedName(fuelItem)))
                return true;

            string prefab = PrefabName(fuelItem);
            if (StationFeedRules.IsDisallowedType(flags.FuelDisallowTypes, prefab))
                return true;

            PullOne(player, __instance.transform.position, prefab, fuelItem, flags);
            return true;
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
            CookingStation __instance, Humanoid user, ItemDrop.ItemData item)
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

            if (StationFeedRules.RemainingFuelCapacity(__instance.m_maxFuel, __instance.GetFuel()) <= 0)
                return true;

            ItemDrop fuelItem = __instance.m_fuelItem;
            if (!HasItemData(fuelItem))
                return true;

            if (inventory.HaveItem(SharedName(fuelItem)))
                return true;

            string prefab = PrefabName(fuelItem);
            if (StationFeedRules.IsDisallowedType(flags.FuelDisallowTypes, prefab))
                return true;

            PullOne(player, __instance.transform.position, prefab, fuelItem, flags);
            return true;
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
        /// calls the station's own <c>FindCookableItem</c> on the player's inventory, which is exactly
        /// the <c>__result != null</c> test CFC's postfix made.
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

            // This same method collects the finished food first; pulling an ingredient for that would
            // be pulling for an interaction that only wants to take the cooked item out.
            if (__instance.HaveDoneItem())
                return true;

            Player player = user as Player;
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null)
                return true;

            // CFC's guard line (395): a station with nowhere to put food, or whose fire is out while
            // the station requires one, is not fed.
            if (__instance.GetFreeSlot() == -1)
                return true;

            if (__instance.m_requireFire && !__instance.IsFireLit())
                return true;

            if (__instance.FindCookableItem(inventory) != null)
                return true;

            PullFirstInputs(
                player, __instance.transform.position,
                InputItems(__instance.m_conversion), flags.OreDisallowTypes, flags);
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
        /// The ammunition type follows CFC's own choice (line 1140): the type the turret already has
        /// loaded when it holds any, and its configured <c>m_ammoType</c> otherwise, so the item this
        /// patch supplies is the one vanilla's <c>UseItem</c> will look for.
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

            string ammoType = __instance.GetAmmo() > 0 ? __instance.GetAmmoType() : __instance.m_ammoType;
            if (string.IsNullOrEmpty(ammoType))
                return true;

            ItemDrop ammo = AmmoItem(ammoType);
            if (ammo == null)
            {
                Debug(flags, "Turret: no item prefab for ammunition type '" + ammoType + "'; nothing pulled.");
                return true;
            }

            PullOne(player, __instance.transform.position, ammoType, ammo, flags);
            return true;
        }

        /// <summary>
        /// Pulls one of <paramref name="prefab"/> into <paramref name="player"/>'s inventory from
        /// storage in range of <paramref name="origin"/> (the station), through the mod's one pull
        /// entry point. Returns whether anything was actually pulled; a pull that storage cannot cover,
        /// or that the inventory cannot take, moves nothing.
        /// </summary>
        private static bool PullOne(Player player, Vector3 origin, string prefab, ItemDrop item, FeatureFlags flags)
        {
            if (player == null || item == null || string.IsNullOrEmpty(prefab) || flags == null)
                return false;

            Inventory inventory = player.GetInventory();
            if (inventory == null)
                return false;

            // One of the station's item, at the quality plane a station consumes at (vanilla's
            // requirement arithmetic reports m_amount for quality <= 1, which is what a station feed
            // is). An upgrader resource is never planned for a station: an Idol is only ever consumed
            // at a crafting station, so allowUpgraderResources is false.
            var requirements = new[]
            {
                new Piece.Requirement { m_amount = 1, m_resItem = item }
            };

            bool pulled = PullEngine.PullToInventory(
                requirements, 0, 1, inventory, Origin(origin), flags, false, out bool requested);

            if (pulled && requested)
                Debug(flags, "Station: pulled 1x '" + prefab + "' from storage into " + player.name + "'s inventory.");

            return pulled && requested;
        }

        /// <summary>
        /// Pulls one of the first input in <paramref name="inputs"/> that storage can supply, skipping
        /// any input whose prefab name is on <paramref name="disallowTypes"/>. Walks the station's own
        /// input order, so a smelter that accepts several ores asks for the first one the base can
        /// actually provide, exactly as CraftFromContainers' loop over <c>m_conversion</c> does.
        /// </summary>
        private static bool PullFirstInputs(
            Player player, Vector3 origin, List<ItemDrop> inputs, string disallowTypes, FeatureFlags flags)
        {
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

                if (PullOne(player, origin, prefab, input, flags))
                    return true;
            }
            return false;
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

            switch (setting)
            {
                case PullSetting.Fuel:
                    return flags.PullFuel ? flags : null;
                case PullSetting.Ore:
                    return flags.PullOre ? flags : null;
                case PullSetting.Cooking:
                    return flags.PullCooking ? flags : null;
                default:
                    return null;
            }
        }

        /// <summary>Which station setting gates a patch.</summary>
        private enum PullSetting
        {
            /// <summary><c>PullFuel</c> — a fireplace's fuel, a smelter's fuel, a cooking station's fire.</summary>
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
