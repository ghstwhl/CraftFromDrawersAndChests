using System.Collections.Generic;
using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using CraftFromDrawersAndChests.Pull;
using CraftFromDrawersAndChests.Storage;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CraftFromDrawersAndChests.Patches
{
    /// <summary>
    /// The ctrl-click pull: holding <c>PullItemsKey</c> while clicking a recipe pulls that recipe's
    /// ingredients into the player's inventory instead of crafting it, reports the outcome, and
    /// flashes the resource readout while storage covers the requirement.
    /// <para>
    /// Three patch sites. <c>InventoryGui.OnCraftPressed</c> is prefixed: when the pull key is held the
    /// craft is never performed — <see cref="PullEngine.PullToPlayer"/> is run instead and the prefix
    /// returns false. This mirrors CraftFromContainers 4.0.7's <c>DoCrafting_Patch</c>, which also
    /// never lets the intercepted click craft. The second site, a postfix on
    /// <c>InventoryGui.SetupRequirement</c>, is the readout: the requirement's amount text is
    /// rewritten from the configured resource string and alternates between
    /// <see cref="PluginConfig.FlashColor"/> and <see cref="PluginConfig.UnFlashColor"/> while the
    /// requirement is covered by storage. The third, a prefix on <c>InventoryGui.Update</c>,
    /// refreshes the crafting panel when the prevent gate flips, so a change in which recipes storage
    /// makes available is visible at once.
    /// </para>
    /// <para>
    /// <b>The prevent gate.</b> Every site here obeys <see cref="AllowByKey"/>, and so do the
    /// storage-aware availability and consume gates in <c>Patches.CraftPatches</c>: the single
    /// decision is the port of CraftFromContainers' <c>AllowByKey()</c> — with the
    /// <c>"Switch prevent"</c> setting off (the default) holding the prevent key suppresses the mod,
    /// and with it on the mod only acts while the prevent key is held. When the gate refuses, the
    /// click is vanilla, the readout keeps its own text and colour, and crafting falls back to the
    /// player's own inventory with no pull at all.
    /// </para>
    /// </summary>
    internal static class PullKeyPatches
    {
        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        /// <summary>The child of a requirement's element that carries its amount, as the game builds it.</summary>
        private const string ResAmountChild = "res_amount";

        /// <summary>The prevent-gate state the last <c>InventoryGui.Update</c> prefix observed, so a flip can be noticed.</summary>
        private static bool lastAllowed = true;

        /// <summary>
        /// Turns a recipe click into a pull while the pull key is held. The pull key is a
        /// <i>held</i> key (<c>Input.GetKey</c>), matching CraftFromContainers, so a player can click
        /// repeatedly without re-pressing. When the pull runs — or is refused — the prefix returns
        /// false so vanilla never also crafts; when the pull key or the prevent gate says the click is
        /// vanilla, it returns true untouched.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
        [HarmonyPrefix]
        private static bool OnCraftPressedPrefix(
            InventoryGui.RecipeDataPair ___m_selectedRecipe,
            ItemDrop.ItemData ___m_craftUpgradeItem,
            bool ___m_multiCrafting,
            int ___m_multiCraftAmount)
        {
            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || !flags.ModEnabled)
                return true;

            Player player = Player.m_localPlayer;
            if (player == null)
                return true;

            Recipe recipe = ___m_selectedRecipe.Recipe;
            if (recipe == null || recipe.m_item == null)
                return true;

            if (!PullKeyRules.ShouldPull(KeyHeld(flags.PullItemsKey), PreventKeyHeld(flags), flags.SwitchPrevent))
                return true;

            // The craft's quality level, derived exactly as InventoryGui derives it (an upgrade is the
            // item being upgraded at quality + 1, otherwise 1). An impossible upgrade is left to
            // vanilla to refuse.
            int qualityLevel = ___m_craftUpgradeItem != null ? ___m_craftUpgradeItem.m_quality + 1 : 1;
            if (qualityLevel > recipe.m_item.m_itemData.m_shared.m_maxQuality)
                return true;

            int multiplier = ___m_multiCrafting && ___m_multiCraftAmount > 0 ? ___m_multiCraftAmount : 1;

            bool pulled = PullEngine.PullToPlayer(recipe.m_resources, qualityLevel, multiplier, player, flags);
            if (pulled)
            {
                ShowMessage(player, flags.PulledMessage);
                Debug(flags, "Pull: pulled the ingredients for '" + recipe.m_item.m_itemData.m_shared.m_name + "'.");
            }
            else
            {
                ShowMessage(player, FailureReason(player, recipe, qualityLevel, multiplier, flags));
            }

            // Intercepted: the click was a pull, so vanilla must not craft as well.
            return false;
        }

        /// <summary>
        /// Rewrites a requirement's amount text to the configured resource string and flashes it
        /// while storage covers it, so the player can see that the recipe is reachable from chests
        /// and drawers rather than only from their own inventory. The available amount counts the
        /// player's inventory first and only then the eligible sources, and only sources a pull could
        /// actually take from synchronously; storage is scanned only when the player is short, as
        /// CraftFromContainers does. The colour alternates between the flash colours only when
        /// storage is what closes the gap — a recipe the player already covers keeps vanilla's colour
        /// — but the readout text is written either way.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), "SetupRequirement")]
        [HarmonyPostfix]
        private static void SetupRequirementPostfix(
            Transform elementRoot, Piece.Requirement req, Player player, int quality, int craftMultiplier)
        {
            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || !flags.ModEnabled)
                return;

            if (!PullKeyRules.AllowByKey(flags.SwitchPrevent, PreventKeyHeld(flags)))
                return;

            if (req == null || req.m_resItem == null || elementRoot == null || player == null)
                return;

            string prefab = PullEngine.PrefabName(req);
            if (string.IsNullOrEmpty(prefab))
                return;

            int amount = req.GetAmount(quality) * craftMultiplier;
            if (amount <= 0)
                return;

            Inventory inventory = player.GetInventory();
            if (inventory == null)
                return;

            // The player's own count first; storage is added only when the player is short, exactly as
            // CraftFromContainers does, so a recipe the player can already pay for is not rescanned.
            int available = PullEngine.CountHeld(inventory, prefab);
            bool playerShort = available < amount;
            if (playerShort)
            {
                Vec3 origin = PullEngine.OriginOf(player);
                IReadOnlyList<IStorageSource> sources = PullEngine.Collect(origin, flags);
                for (int i = 0; i < sources.Count; i++)
                {
                    IStorageSource source = sources[i];
                    if (source == null || !source.IsValid || !PullEngine.CanServeSynchronously(source))
                        continue;

                    available += source.Count(prefab);
                }
            }

            Transform amountTransform = elementRoot.Find(ResAmountChild);
            if (amountTransform == null)
                return;

            TMP_Text text = amountTransform.GetComponent<TMP_Text>();
            if (text == null)
                return;

            // Flash only when storage is what closes the gap; a recipe the player already covers keeps
            // vanilla's colour. CFC overwrites the readout's text either way.
            if (playerShort && available >= amount)
                text.color = Mathf.Sin(Time.time * 10f) > 0f ? PluginConfig.FlashColor : PluginConfig.UnFlashColor;

            text.text = PullKeyRules.ResourceAmountText(flags.ResourceString, available, amount);
        }

        /// <summary>
        /// Refreshes the crafting panel when the prevent gate flips, so a change in which recipes
        /// storage makes available is visible at once. <c>InventoryGui.Update</c> re-checks only the
        /// selected recipe each frame; the recipe <i>list</i> is rebuilt solely by
        /// <c>UpdateCraftingPanel</c>, so without this the list would keep the answer it was built
        /// with until the player reselected. Ported from CraftFromContainers' <c>InventoryGui_Update_Patch</c>.
        /// The method is private in the shipped game, so it is invoked by reflection.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), "Update")]
        [HarmonyPrefix]
        private static void InventoryGuiUpdatePrefix(InventoryGui __instance, Animator ___m_animator)
        {
            bool allowed = AllowByKey();
            bool changed = allowed != lastAllowed;
            lastAllowed = allowed;

            if (!changed || __instance == null || ___m_animator == null || !___m_animator.GetBool("visible"))
                return;

            if (Player.m_localPlayer == null)
                return;

            System.Reflection.MethodInfo updatePanel =
                AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel", new[] { typeof(bool) });
            if (updatePanel == null)
                return;

            updatePanel.Invoke(__instance, new object[] { false });
        }

        /// <summary>
        /// Why a refused pull was refused, for the player-facing message: the plan is rebuilt (it is
        /// cheap — the sources are cached for the frame) so a storage shortfall can name the missing
        /// items, de-duplicated; a plan that storage could cover but the inventory could not take
        /// reports the room failure instead.
        /// </summary>
        private static string FailureReason(
            Player player, Recipe recipe, int qualityLevel, int multiplier, FeatureFlags flags)
        {
            Vec3 origin = PullEngine.OriginOf(player);
            PullPlanResult plan = PullEngine.PlanFor(recipe.m_resources, qualityLevel, multiplier, origin, flags);
            return plan.Feasible
                ? PullKeyRules.NoRoomMessage
                : PullKeyRules.MissingMessage(plan.MissingItemNames);
        }

        /// <summary>Whether the configured prevent key is held; an unbound key reads as not held.</summary>
        private static bool PreventKeyHeld(FeatureFlags flags)
        {
            return flags != null && KeyHeld(flags.PreventModKey);
        }

        /// <summary>
        /// Whether this mod may act at all this frame, from the prevent key. The game-facing read of
        /// <see cref="PullKeyRules.AllowByKey"/>: a missing or disabled snapshot refuses. Shared by
        /// the pull patches here and the storage-aware availability and consume gates in
        /// <c>Patches.CraftPatches</c>, so a single answer governs whether storage is substituted and
        /// whether the shortfall is pulled — the two can never disagree and hand out a free craft.
        /// </summary>
        internal static bool AllowByKey()
        {
            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || !flags.ModEnabled)
                return false;

            return PullKeyRules.AllowByKey(flags.SwitchPrevent, KeyHeld(flags.PreventModKey));
        }

        /// <summary>
        /// Whether the configured Unity key name is held this frame. An empty or whitespace value must
        /// never reach <c>Input</c>, which throws on it, and an unrecognised name throws too, so the
        /// call is guarded and a bad value simply reads as "not held" — the same treatment the toggle
        /// hotkey gives its key. The configured value is lower-cased before the lookup, because
        /// <c>Input.GetKey</c> matches its key names case-sensitively and a user who types
        /// <c>"Left Ctrl"</c> would otherwise get a silently dead key.
        /// </summary>
        private static bool KeyHeld(string key)
        {
            if (!ToggleRules.IsKeyConfigured(key))
                return false;

            try
            {
                return Input.GetKey(key.ToLowerInvariant());
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Shows a HUD message; an empty message (the feature disabled) shows nothing.</summary>
        private static void ShowMessage(Player player, string message)
        {
            if (player == null || string.IsNullOrEmpty(message))
                return;

            player.Message(MessageHud.MessageType.Center, message, 0, null);
        }

        /// <summary>Logs a line only when debug logging is on, so the default is quiet.</summary>
        private static void Debug(FeatureFlags flags, string message)
        {
            if (flags != null && flags.IsDebug)
                Log.LogInfo(message);
        }
    }
}
