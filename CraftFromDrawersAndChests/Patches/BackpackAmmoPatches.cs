using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using CraftFromDrawersAndChests.Logic;
using HarmonyLib;

namespace CraftFromDrawersAndChests.Patches
{
    /// <summary>
    /// Lets a bow or crossbow fire ammunition that is in an equipped Adventure Backpacks backpack.
    /// <para>
    /// <b>The gap.</b> Vanilla resolves ammunition in <c>Attack.HaveAmmo</c>, <c>FindAmmo</c> and
    /// <c>UseAmmo</c> through <c>Inventory.GetAmmoItem(ammoType)</c>, which scans the player's own
    /// <c>m_inventory</c> directly. Adventure Backpacks folds a backpack into
    /// <c>CountItems</c>/<c>RemoveItem</c>/<c>HaveItem</c> only while its crafting scope is open, and
    /// never into <c>GetAmmoItem</c> at all, so arrows in the backpack are invisible to a bow and the
    /// player is told they are out of ammunition while carrying plenty (reported from play
    /// 2026-11-22). Unlike the Plateautem bridge, opening Adventure Backpacks' scope cannot help here —
    /// the lookup it would need to widen is not one AB patches.
    /// </para>
    /// <para>
    /// <b>What this does instead.</b> A prefix on <c>Attack.HaveAmmo</c> — the check that is about to
    /// print "$msg_outof" — moves the matching stack from the equipped backpack into the player's own
    /// inventory, using vanilla's own <c>Inventory.MoveItemToThis</c>, and then lets vanilla's check run
    /// and find it. <c>MoveItemToThis</c> removes from the source only when the destination accepted the
    /// stack, so a full inventory leaves the backpack untouched: no duplication and no loss. The whole
    /// stack moves, because ammunition is consumed one round at a time and a partial move would only
    /// repeat on the next shot.
    /// </para>
    /// <para>
    /// Everything is resolved by name at runtime: Adventure Backpacks is a soft dependency, so its API is
    /// looked up by type name and <see cref="Prepare"/> disables this class when the mod is absent or its
    /// API has changed shape.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class BackpackAmmoPatches
    {
        /// <summary>Adventure Backpacks' plugin GUID.</summary>
        private const string BackpackGuid = "vapok.mods.adventurebackpacks";

        /// <summary>Its public API facade, resolved at runtime (soft dependency).</summary>
        private const string BackpackApiTypeName = "AdventureBackpacks.API.ABAPI";

        private static MethodBase getAmmoItemMethod;
        private static MethodInfo tryGetBackpack;
        private static int prepared;   // 0 = not resolved yet, 1 = ready, 2 = unavailable

        /// <summary>
        /// Ammo types already reported as answered from the backpack. The lookup runs on every attack
        /// update - every frame while aiming - so reporting each answer individually buries the rest of
        /// a debug log; the per-shot detail comes from the spend line instead.
        /// </summary>
        private static readonly HashSet<string> loggedAnswers = new HashSet<string>();

        /// <summary>
        /// Resolves the API once and reports whether every class in this file may run. Harmony calls each
        /// class's <c>Prepare</c> in no particular order, so this is idempotent rather than a flag set by
        /// one class for the others.
        /// </summary>
        internal static bool EnsurePrepared()
        {
            if (prepared == 0)
                prepared = Resolve() ? 1 : 2;

            return prepared == 1;
        }

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return EnsurePrepared();
        }

        /// <summary>The equipped backpack's inventory, for the sibling patches in this file.</summary>
        internal static Inventory BackpackFor(Player player)
        {
            return EquippedBackpack(player);
        }

        /// <summary>Debug-gated logging, shared with the sibling patches in this file.</summary>
        internal static void Trace(FeatureFlags flags, string message)
        {
            Debug(flags, message);
        }

        /// <summary>
        /// An item's prefab name where the game knows it ("ArrowFrost"), falling back to its shared-name
        /// token - the ammo lookup is keyed by that token ("$ammo_arrows"), which reads poorly in a log.
        /// </summary>
        internal static string PrefabLabel(ItemDrop.ItemData item, string fallback)
        {
            if (item != null && item.m_dropPrefab != null)
                return item.m_dropPrefab.name;

            return item != null && item.m_shared != null ? item.m_shared.m_name : fallback;
        }

        private static bool Resolve()
        {
            if (!Chainloader.PluginInfos.ContainsKey(BackpackGuid))
                return false;

            getAmmoItemMethod = AccessTools.Method(
                typeof(Inventory), "GetAmmoItem", new[] { typeof(string), typeof(string) });

            Type api = AccessTools.TypeByName(BackpackApiTypeName);
            tryGetBackpack = api != null
                ? AccessTools.Method(api, "TryGetEquippedBackpackInventory",
                    new[] { typeof(Player), typeof(Inventory).MakeByRefType() })
                : null;

            if (getAmmoItemMethod == null || tryGetBackpack == null || !tryGetBackpack.IsStatic)
            {
                LogWarning(ModInfo.ModName + ": Adventure Backpacks was found but its backpack API could " +
                           "not be resolved, so ammunition in a backpack will not be usable for a bow or " +
                           "crossbow by this mod. (expected Inventory.GetAmmoItem(string,string) and " +
                           "ABAPI.TryGetEquippedBackpackInventory(Player, out Inventory))");
                return false;
            }

            return true;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return getAmmoItemMethod;
        }

        /// <summary>
        /// Answers the ammunition lookup with the equipped backpack's round when the player's own
        /// inventory has none. Every ammo path in the game resolves through this one method
        /// (<c>Attack.HaveAmmo</c> :752, <c>FindAmmo</c> :717, <c>UseAmmo</c> :781), so one postfix
        /// covers the check, the projectile and the spend.
        /// </summary>
        [HarmonyPostfix]
        private static void GetAmmoItemPostfix(
            Inventory __instance, string ammoName, string matchPrefabName, ref ItemDrop.ItemData __result)
        {
            if (__result != null || __instance == null || string.IsNullOrEmpty(ammoName))
                return;

            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || !flags.ModEnabled)
                return;

            if (!PullKeyPatches.AllowByKey())
                return;

            Player player = Player.m_localPlayer;
            if (player == null || __instance != player.GetInventory())
                return;

            Inventory backpack = EquippedBackpack(player);
            if (backpack == null)
                return;

            // The backpack's own lookup runs this postfix too; __instance differs there, so it stops.
            __result = backpack.GetAmmoItem(ammoName, matchPrefabName);
            if (__result != null && loggedAnswers.Add(ammoName))
            {
                Debug(flags, "Backpack ammo: answering '" + PrefabLabel(__result, ammoName) + "' from the " +
                             "equipped backpack (" + __result.m_stack + "x there). Logged once per ammo type.");
            }
        }

        /// <summary>
        /// Adventure Backpacks exposes its backpack inventory through this one call, or null when no
        /// backpack is equipped (or the API is unavailable).
        /// </summary>
        private static Inventory EquippedBackpack(Player player)
        {
            if (tryGetBackpack == null)
                return null;

            object[] args = { player, null };

            try
            {
                return tryGetBackpack.Invoke(null, args) is bool ok && ok ? args[1] as Inventory : null;
            }
            catch (Exception e)
            {
                LogWarning(ModInfo.ModName + ": Adventure Backpacks' backpack could not be read, so " +
                           "backpack ammunition is not used for this shot: " + e.Message);
                return null;
            }
        }

        /// <summary>Logs a line only when debug logging is on, so the default is quiet.</summary>
        private static void Debug(FeatureFlags flags, string message)
        {
            if (flags != null && flags.IsDebug)
                Plugin.Log.LogInfo(message);
        }

        /// <summary>Logs a resolution problem at warning level, so it is visible without debug.</summary>
        private static void LogWarning(string message)
        {
            Plugin.Log.LogWarning(message);
        }
    }

    /// <summary>
    /// Spends a backpack round in place. Vanilla's <c>Attack.UseAmmo</c> consumes with
    /// <c>m_character.GetInventory().RemoveItem(ammoItem, 1)</c>, and that method refuses an item the
    /// inventory does not contain (<c>Inventory.decompiled.cs:374-377</c> for a whole stack, <c>:391</c>
    /// for a partial one). Answering the lookup without this would fire arrows for free, so the one
    /// removal that names a backpack round is redirected into the backpack.
    /// </summary>
    [HarmonyPatch]
    internal static class BackpackAmmoRemovePatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return BackpackAmmoPatches.EnsurePrepared();
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Inventory), "RemoveItem",
                new[] { typeof(ItemDrop.ItemData), typeof(int) });
        }

        [HarmonyPrefix]
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int amount, ref bool __result)
        {
            if (item == null || amount <= 0 || __instance == null)
                return true;

            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || !flags.ModEnabled)
                return true;

            if (!PullKeyPatches.AllowByKey())
                return true;

            Player player = Player.m_localPlayer;
            if (player == null || __instance != player.GetInventory())
                return true;

            bool inventoryHasIt = __instance.ContainsItem(item);
            if (inventoryHasIt)
                return true;

            Inventory backpack = BackpackAmmoPatches.BackpackFor(player);
            if (backpack == null)
                return true;

            bool backpackHasIt = backpack.ContainsItem(item);
            if (!BackpackAmmoRules.ShouldSpendFromBackpack(inventoryHasIt, backpackHasIt))
                return true;

            string sharedName = item.m_shared != null ? item.m_shared.m_name : null;
            string label = BackpackAmmoPatches.PrefabLabel(item, sharedName);
            int before = string.IsNullOrEmpty(sharedName) ? 0 : backpack.CountItems(sharedName, -1, false);

            __result = backpack.RemoveItem(item, amount);

            if (__result)
            {
                int left = string.IsNullOrEmpty(sharedName) ? 0 : backpack.CountItems(sharedName, -1, false);
                BackpackAmmoPatches.Trace(flags, "Backpack ammo: spent " + (before - left) + "x '" + label +
                                                 "' from the equipped backpack (" + left + " left there).");
            }

            return false;
        }
    }

    /// <summary>
    /// Keeps a backpack round out of the equipment slots. <c>Attack.EquipAmmoItem</c> would otherwise
    /// call <c>EquipItem</c> on an item that lives in the backpack, producing an equipped item the
    /// player does not hold - a state Adventure Backpacks never creates and one this mod will not
    /// either. The shot does not need it: <c>UseAmmo</c> resolves ammunition through the lookup again
    /// when it fires.
    /// </summary>
    [HarmonyPatch]
    internal static class BackpackAmmoEquipPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return BackpackAmmoPatches.EnsurePrepared();
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Attack), "EquipAmmoItem",
                new[] { typeof(Humanoid), typeof(ItemDrop.ItemData) });
        }

        [HarmonyPrefix]
        private static bool Prefix(Humanoid __0, ItemDrop.ItemData __1, ref bool __result)
        {
            Player player = __0 as Player;
            if (player == null || player != Player.m_localPlayer)
                return true;

            if (__1 == null || __1.m_shared == null || string.IsNullOrEmpty(__1.m_shared.m_ammoType))
                return true;

            Inventory inventory = player.GetInventory();
            if (inventory == null)
                return true;

            ItemDrop.ItemData ammo = inventory.GetAmmoItem(__1.m_shared.m_ammoType);
            if (ammo == null || inventory.ContainsItem(ammo))
                return true;

            __result = true;
            return false;
        }
    }
}
