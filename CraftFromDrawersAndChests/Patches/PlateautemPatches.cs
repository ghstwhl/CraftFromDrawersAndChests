using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using CraftFromDrawersAndChests.Logic;
using HarmonyLib;
using UnityEngine;

namespace CraftFromDrawersAndChests.Patches
{
    /// <summary>
    /// Feeds a Plateautem — erkle64's terrain-flattening totem (<c>Plateautem.Plateautem</c>) — from
    /// nearby storage, and lets an equipped Adventure Backpacks backpack pay for it.
    /// <para>
    /// <b>Soft dependency, resolved at runtime.</b> The totem lives in another mod's assembly, so the
    /// type is looked up by name and its members are read by reflection; <see cref="Prepare"/> reports
    /// whether they are all present, and HarmonyX then skips this class entirely when Plateautem is not
    /// installed or has changed shape. Nothing here is compiled against either mod.
    /// </para>
    /// <para>
    /// <b>What a press actually consumes.</b> The totem has its own fuel selection and counts the
    /// selected item in the player's own inventory only (<c>TakeFuelItemFromUser</c> calls
    /// <c>inventory.CountItems(sharedName, -1, true)</c> and messages
    /// <c>$piece_plateautem_noFuel</c> when it is zero). So the mod asks
    /// <see cref="PlateautemFuelRules.PrefabToSupply"/> which prefab this press wants and pulls one of
    /// it, exactly as the fireplace and smelter fuel paths do.
    /// </para>
    /// <para>
    /// <b>Why the Adventure Backpacks scope is opened around the interaction.</b> AB folds an equipped
    /// backpack into <c>Inventory.CountItems</c> — <i>and</i> debits it in <c>Inventory.RemoveItem</c> —
    /// only while its <c>CraftingContext</c> is open, which it opens for its own crafting methods and
    /// not for a third-party totem. Without the scope a backpack full of wood is invisible to the totem
    /// and the player is told they have no fuel while carrying plenty (reported from play 2026-11-22,
    /// reproduced by moving wood between the backpack and the main inventory). AB publishes
    /// <c>Enter()</c>/<c>Exit()</c> for exactly this, so the prefix opens the scope and the finalizer
    /// closes it, whatever the totem's own code does in between. Both the count and the debit then see
    /// the same pool, which is the invariant this mod holds everywhere else: "held" must mean the
    /// consume step will find it.
    /// </para>
    /// <para>
    /// The storage pull is gated on the mod's own <c>Pull fuel</c> setting, like every other fuel path.
    /// The backpack scope is not: it draws nothing itself, it only lets the totem see what AB already
    /// considers available to the player.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class PlateautemPatches
    {
        /// <summary>The totem's plugin GUID, used to decide whether the target exists at all.</summary>
        private const string PlateautemGuid = "com.erkle64.Plateautem";

        /// <summary>The totem component's full type name, resolved at runtime (soft dependency).</summary>
        private const string PlateautemTypeName = "Plateautem.Plateautem";

        /// <summary>Adventure Backpacks' public crafting-scope type, resolved at runtime (soft dependency).</summary>
        private const string BackpackScopeTypeName = "AdventureBackpacks.Features.CraftingContext";

        private static Type plateautemType;
        private static MethodBase interactMethod;
        private static FieldInfo fuelItemsField;
        private static FieldInfo currentFuelIndexField;
        private static FieldInfo fuelPrefabNameField;
        private static FieldInfo fuelValueField;
        private static FieldInfo configMaximumFuelField;
        private static FieldInfo configMaximumStoneField;
        private static PropertyInfo currentStoneStoredProperty;
        private static MethodInfo getTotalFuelStoredMethod;
        private static bool capacityResolved;
        private static MethodInfo backpackEnter;
        private static MethodInfo backpackExit;

        /// <summary>
        /// Resolves everything this patch needs, and reports whether the totem is present in a shape we
        /// understand. A missing field disables the patch with a warning instead of throwing inside a
        /// prefix on another mod's method.
        /// </summary>
        [HarmonyPrepare]
        private static bool Prepare()
        {
            if (!Chainloader.PluginInfos.ContainsKey(PlateautemGuid))
                return false;

            plateautemType = AccessTools.TypeByName(PlateautemTypeName);
            if (plateautemType == null)
                return false;

            interactMethod = AccessTools.Method(
                plateautemType, "Interact", new[] { typeof(Humanoid), typeof(bool), typeof(bool) });
            fuelItemsField = AccessTools.Field(plateautemType, "fuelItems");
            currentFuelIndexField = AccessTools.Field(plateautemType, "currentFuelItemIndex");

            Type fuelItemType = fuelItemsField != null && fuelItemsField.FieldType.IsGenericType
                ? fuelItemsField.FieldType.GetGenericArguments()[0]
                : null;
            fuelPrefabNameField = fuelItemType != null ? AccessTools.Field(fuelItemType, "prefabName") : null;
            fuelValueField = fuelItemType != null ? AccessTools.Field(fuelItemType, "fuelValue") : null;

            if (interactMethod == null || fuelItemsField == null || currentFuelIndexField == null ||
                fuelPrefabNameField == null)
            {
                LogWarning(ModInfo.ModName + ": Plateautem was found but its fuel members could not be " +
                           "resolved, so the totem is not fed from storage by this mod. " +
                           "(expected Interact(Humanoid,bool,bool), fuelItems[].prefabName and " +
                           "currentFuelItemIndex)");
                return false;
            }

            // Optional: Adventure Backpacks' crafting scope. Absent is fine, the totem is still fed.
            Type backpackContext = AccessTools.TypeByName(BackpackScopeTypeName);
            if (backpackContext != null)
            {
                backpackEnter = AccessTools.Method(backpackContext, "Enter");
                backpackExit = AccessTools.Method(backpackContext, "Exit");
            }

            // Optional: the totem's own capacity, needed only to fill it on a held press. Without these a
            // single press still works (it takes one item), so this degrades rather than disables.
            configMaximumFuelField = AccessTools.Field(plateautemType, "configMaximumFuel");
            configMaximumStoneField = AccessTools.Field(plateautemType, "configMaximumStone");
            currentStoneStoredProperty = AccessTools.Property(plateautemType, "currentStoneStored");
            getTotalFuelStoredMethod = AccessTools.Method(plateautemType, "GetTotalFuelStored", Type.EmptyTypes);
            capacityResolved = configMaximumFuelField != null && configMaximumStoneField != null &&
                               currentStoneStoredProperty != null && getTotalFuelStoredMethod != null &&
                               fuelValueField != null;

            if (!capacityResolved)
            {
                LogWarning(ModInfo.ModName + ": the Plateautem's capacity members could not be resolved, " +
                           "so holding the interaction key will not fill it from storage (a single press " +
                           "still works). (expected configMaximumFuel, configMaximumStone, " +
                           "currentStoneStored, GetTotalFuelStored and fuelItems[].fuelValue)");
            }

            return true;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return interactMethod;
        }

        /// <summary>
        /// Opens Adventure Backpacks' crafting scope around the totem and pulls the selected fuel.
        /// <para>
        /// The state parameter <b>must</b> be named <c>__state</c>: HarmonyX recognises only that name
        /// (its <c>HarmonyManipulator.StateVar</c>) and treats any other <c>__name</c> parameter as an
        /// indexed argument, which fails the whole patch with "Parameter __name does not contain a valid
        /// index" (1.0.17 shipped with <c>__openedBackpack</c> and failed to apply for exactly that
        /// reason).
        /// </para>
        /// </summary>
        [HarmonyPrefix]
        private static void Prefix(object __instance, Humanoid user, bool hold, out bool __state)
        {
            __state = false;

            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || !flags.ModEnabled)
                return;

            if (!PullKeyPatches.AllowByKey())
                return;

            Player player = user as Player;
            var totem = __instance as Component;
            if (player == null || totem == null)
                return;

            // Pull before opening the backpack scope: the fuel belongs in the player's own inventory
            // first, otherwise Adventure Backpacks' AddItem handling could redirect it into the pack.
            // The totem refuses to take fuel while a piece is being placed, so nothing is pulled then.
            if (flags.PullFuel && !player.InPlaceMode())
                PullSelectedFuel(player, __instance, totem.transform.position, flags, hold);

            if (backpackEnter == null || backpackExit == null)
                return;

            try
            {
                backpackEnter.Invoke(null, null);
                __state = true;
            }
            catch (Exception e)
            {
                LogWarning(ModInfo.ModName + ": Adventure Backpacks' crafting scope could not be opened " +
                           "for the Plateautem, so fuel in a backpack will not count: " + e.Message);
            }
        }

        [HarmonyFinalizer]
        private static void Finalizer(bool __state)
        {
            if (!__state || backpackExit == null)
                return;

            try
            {
                backpackExit.Invoke(null, null);
            }
            catch (Exception e)
            {
                LogWarning(ModInfo.ModName + ": Adventure Backpacks' crafting scope could not be closed " +
                           "after the Plateautem interaction: " + e.Message);
            }
        }

        /// <summary>
        /// Pulls the items the totem's current selection will consume: one for a single press, and the
        /// whole remaining capacity for a held press (its <c>takeAll</c> path), so holding E fills the
        /// totem in one go instead of one item per repeat.
        /// </summary>
        private static void PullSelectedFuel(
            Player player, object instance, Vector3 origin, FeatureFlags flags, bool hold)
        {
            IList entries = FuelEntries();
            int selectedIndex = SelectedFuelIndex(instance);
            string prefabName = PlateautemFuelRules.PrefabToSupply(selectedIndex, FuelPrefabNames(entries));
            if (string.IsNullOrEmpty(prefabName))
            {
                Debug(flags, "Plateautem: no fuel item is selected, so nothing was pulled.");
                return;
            }

            ItemDrop fuel = ItemFor(prefabName);
            if (fuel == null)
            {
                Debug(flags, "Plateautem: no item prefab '" + prefabName + "'; nothing pulled.");
                return;
            }

            int units = UnitsForPress(instance, selectedIndex, entries, hold);
            if (units <= 0)
            {
                Debug(flags, "Plateautem: it has no room for more '" + prefabName + "'; nothing pulled.");
                return;
            }

            Debug(flags, "Plateautem: pulling up to " + units + "x '" + prefabName + "' (" +
                         (hold ? "held" : "single") + " press).");

            StationPatches.PullFromStorage(player, origin, prefabName, fuel, flags, units, hold);
        }

        /// <summary>
        /// How many units this interaction should supply: 1 for a single press, the totem's whole
        /// remaining room for a hold (its own <c>takeAll</c> arithmetic), and 1 when the capacity could
        /// not be resolved — never a guess at a quantity it might not take.
        /// </summary>
        private static int UnitsForPress(object instance, int selectedIndex, IList entries, bool hold)
        {
            if (!hold)
                return 1;

            if (!capacityResolved)
                return 1;

            try
            {
                int room = selectedIndex < 0
                    ? PlateautemFuelRules.UnitsInRoom(MaximumStone(), StoredStone(instance), 1f)
                    : PlateautemFuelRules.UnitsInRoom(MaximumFuel(), TotalFuelStored(instance), FuelValue(entries, selectedIndex));

                return PlateautemFuelRules.UnitsToRequest(true, room);
            }
            catch (Exception e)
            {
                LogWarning(ModInfo.ModName + ": the Plateautem's remaining capacity could not be read, so only " +
                           "one item was pulled for this press: " + e.Message);
                return 1;
            }
        }

        private static int MaximumFuel()
        {
            return ((ConfigEntry<int>)configMaximumFuelField.GetValue(null)).Value;
        }

        private static int MaximumStone()
        {
            return ((ConfigEntry<int>)configMaximumStoneField.GetValue(null)).Value;
        }

        private static float StoredStone(object instance)
        {
            return (float)currentStoneStoredProperty.GetValue(instance, null);
        }

        private static float TotalFuelStored(object instance)
        {
            return (float)getTotalFuelStoredMethod.Invoke(instance, null);
        }

        private static float FuelValue(IList entries, int index)
        {
            if (entries == null || index < 0 || index >= entries.Count)
                return 0f;

            object value = fuelValueField.GetValue(entries[index]);
            return value is float fuelValue ? fuelValue : 0f;
        }

        /// <summary>The totem's <c>currentFuelItemIndex</c>: -1 is its stone mode.</summary>
        private static int SelectedFuelIndex(object instance)
        {
            object value = currentFuelIndexField.GetValue(instance);
            return value is int index ? index : -1;
        }

        /// <summary>The totem's configured fuel entries, as the boxed structs its list holds.</summary>
        private static IList FuelEntries()
        {
            return fuelItemsField.GetValue(null) as IList;
        }

        /// <summary>The prefab name of each configured fuel entry, in the totem's own order.</summary>
        private static List<string> FuelPrefabNames(IList entries)
        {
            var names = new List<string>();
            if (entries == null)
                return names;

            foreach (object entry in entries)
            {
                if (entry == null)
                {
                    names.Add(null);
                    continue;
                }

                names.Add(fuelPrefabNameField.GetValue(entry) as string);
            }

            return names;
        }

        /// <summary>The item prefab behind a name, as the game resolves it (the totem's own item).</summary>
        private static ItemDrop ItemFor(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName) || ZNetScene.instance == null)
                return null;

            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            return prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        }

        /// <summary>Logs a line only when debug logging is on, so the default is quiet.</summary>
        private static void Debug(FeatureFlags flags, string message)
        {
            if (flags != null && flags.IsDebug)
                Plugin.Log.LogInfo(message);
        }

        /// <summary>Logs a resolution problem once, at warning level, so it is visible without debug.</summary>
        private static void LogWarning(string message)
        {
            Plugin.Log.LogWarning(message);
        }
    }
}
