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
    /// The pull key: holding <c>PullItemsKey</c> while clicking a recipe pulls that recipe's
    /// ingredients into the player's inventory instead of crafting it, reports the outcome, and
    /// flashes the resource readout while storage covers the requirement. The same key in placement
    /// mode pulls the selected <i>building piece's</i> materials instead of placing the piece.
    /// <para>
    /// Four patch sites. <c>InventoryGui.OnCraftPressed</c> is prefixed: when the pull key is held the
    /// craft is never performed — <see cref="PullEngine.PullToPlayer"/> is run instead and the prefix
    /// returns false. This mirrors CraftFromContainers 4.0.7's <c>DoCrafting_Patch</c>, which also
    /// never lets the intercepted click craft. The second site, a prefix on <c>Player.UpdatePlacement</c>,
    /// is the build half of the same feature: an attack press while the pull key is held fetches the
    /// selected piece's materials and never places it, exactly as CraftFromContainers' own
    /// <c>UpdatePlacement_Patch</c> does. The third, a postfix on <c>InventoryGui.SetupRequirement</c>,
    /// is the crafting readout: the requirement's amount text is rewritten from the configured
    /// resource string and alternates between <see cref="PluginConfig.FlashColor"/> and
    /// <see cref="PluginConfig.UnFlashColor"/> while the requirement is covered by storage. The fourth,
    /// a prefix on <c>InventoryGui.Update</c>, refreshes the crafting panel when the prevent gate flips,
    /// so a change in which recipes storage makes available is visible at once.
    /// </para>
    /// <para>
    /// The build half exists because CraftFromContainers' <c>PullItemsKey</c> is documented as
    /// "holding down this key while crafting <b>or building</b>", and the crafting half alone would
    /// have silently dropped a retained feature. There is deliberately no build-path readout to mirror:
    /// CraftFromContainers flashes only <c>InventoryGui.SetupRequirement</c>, which the crafting panel
    /// owns, and the build HUD shows no per-requirement amounts at all.
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
        /// <c>Player.m_placementStatus</c>, the private placement status the game rewrites each frame.
        /// Resolved once, like the private <c>Container.CheckAccess</c> method the chest source uses.
        /// </summary>
        private static readonly System.Reflection.FieldInfo PlacementStatusField =
            AccessTools.Field(typeof(Player), "m_placementStatus");

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

            bool pulled = PullEngine.PullToPlayer(
                recipe.m_resources, qualityLevel, multiplier, player, flags, out bool requested);

            // Intercepted either way: the click was a pull, so vanilla must not craft as well.
            if (!requested)
                return false; // nothing to fetch — a no-op, with neither a success nor a failure to report

            if (pulled)
            {
                ShowMessage(player, flags.PulledMessage);
                Debug(flags, "Pull: pulled the ingredients for '" + recipe.m_item.m_itemData.m_shared.m_name + "'.");
            }
            else
            {
                ShowMessage(player, FailureReason(player, recipe.m_resources, qualityLevel, multiplier, flags));
            }

            return false;
        }

        /// <summary>
        /// Turns an attack press in placement mode into a pull of the selected building piece's
        /// materials, mirroring CraftFromContainers 4.0.7's <c>UpdatePlacement_Patch</c> (CFC
        /// <c>BepInExPlugin.cs</c> lines 1096-1125): the same guard line (1102), the same
        /// attack-or-gamepad-place trigger (1106), the same selected-piece, repair-piece, ghost and
        /// placement-status branches (1108-1116), and the same pull call with the same arguments
        /// (1119).
        /// <para>
        /// <b>The press never places the piece.</b> Whether the pull runs or is refused the prefix
        /// returns false, exactly as CFC's does, so a click that was meant to fetch materials cannot
        /// also place the piece — and a placement can therefore never be paid for with nothing. The
        /// pull itself is <see cref="PullEngine.PullToPlayer"/>, the very entry point the ctrl-click
        /// pull uses: it plans the shortfall, proves the destination can take every line, and refuses
        /// having moved nothing when it cannot, so no item can reach the ground.
        /// </para>
        /// <para>
        /// <b>The placement gate already knows about storage.</b> The status this prefix reads is the
        /// answer <c>Player.HaveRequirements(piece, CanBuild)</c> gave inside the same method, and
        /// <c>Patches.CraftPatches</c> already postfixes that check to count chests and drawers — the
        /// same storage-aware gate CraftFromContainers installs for its own placement status. The guard
        /// is read, never weakened, and this prefix runs <i>before</i> the method body that would call
        /// <c>TryPlacePiece</c>, so the gate's storage answer and the placement can never disagree.
        /// </para>
        /// <para>
        /// <b>The prevent gate.</b> <see cref="BuildPullRules.ShouldIntercept"/> carries
        /// <see cref="PullKeyRules.ShouldPull"/>, the same prevent-key decision every other pull site
        /// obeys, so holding the prevent key leaves the press to vanilla here too.
        /// </para>
        /// <para>
        /// <b>Feedback.</b> On success the configured <c>PulledMessage</c> shows. On a refusal during
        /// the pull the reason is named: the missing items, de-duplicated, exactly as the recipe pull
        /// reports them. CraftFromContainers shows the success message on the build path too but no
        /// reason for a refusal, and prints its success message once per requirement (its call sits
        /// inside the requirement loop, CFC line 1261); this mod prints it once.
        /// </para>
        /// </summary>
        [HarmonyPatch(typeof(Player), "UpdatePlacement")]
        [HarmonyPrefix]
        private static bool UpdatePlacementPrefix(
            Player __instance,
            bool takeInput,
            PieceTable ___m_buildPieces,
            GameObject ___m_placementGhost)
        {
            FeatureFlags flags = PluginConfig.Flags;
            if (flags == null || __instance == null || !object.ReferenceEquals(__instance, Player.m_localPlayer))
                return true;

            // CraftFromContainers' one guard line (1102): the mod is on, the prevent gate allows, the
            // pull key is held, the player is placing, input is live, and the piece picker is closed.
            if (!BuildPullRules.ShouldIntercept(
                    flags.ModEnabled,
                    KeyHeld(flags.PullItemsKey),
                    PreventKeyHeld(flags),
                    flags.SwitchPrevent,
                    __instance.InPlaceMode(),
                    takeInput,
                    Hud.IsPieceSelectionVisible()))
                return true;

            // The trigger — an attack press, or the gamepad place button (CFC 1106).
            if (!ZInput.GetButtonDown("Attack") && !ZInput.GetButtonDown("JoyPlace"))
                return true;

            Piece selectedPiece = ___m_buildPieces != null ? ___m_buildPieces.GetSelectedPiece() : null;
            BuildPullDecision decision = BuildPullRules.Decide(
                selectedPiece != null,
                selectedPiece != null && selectedPiece.m_repairPiece,
                ___m_placementGhost != null,
                BuildPullRules.IsPlacementValid(PlacementStatus(__instance)));

            if (decision == BuildPullDecision.LetVanillaHandle)
                return true;

            // Swallowed without a pull: no piece is selected, no ghost exists, or the ghost is not
            // placeable. Nothing moves and nothing is placed — CFC's own return for these cases.
            if (decision == BuildPullDecision.RefuseWithoutPull)
                return false;

            bool pulled = PullEngine.PullToPlayer(
                selectedPiece.m_resources, 0, 1, __instance, flags, out bool requested);

            // Intercepted either way: the press was a pull, so the piece is not placed.
            if (!requested)
                return false; // nothing to fetch — a no-op, with neither a success nor a failure to report

            if (pulled)
            {
                ShowMessage(__instance, flags.PulledMessage);
                Debug(flags, "Pull: pulled the materials for building piece '" + selectedPiece.name + "'.");
            }
            else
            {
                ShowMessage(__instance, FailureReason(__instance, selectedPiece.m_resources, 0, 1, flags));
            }

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
        /// Why a refused pull was refused, for the player-facing message: the shortfall plan is rebuilt
        /// (it is cheap — the sources are cached for the frame) so a storage shortfall can name only the
        /// items that are actually still missing; a plan that storage could cover but the inventory
        /// could not take reports the room failure instead. Shared by the recipe and the building-piece
        /// pull, which differ only in the requirements and the quality/multiplier they pass. Planned
        /// from the same shortfall <see cref="PullEngine.PullToPlayer"/> used, so the message can never
        /// name an item the player is already carrying.
        /// </summary>
        private static string FailureReason(
            Player player, Piece.Requirement[] requirements, int qualityLevel, int multiplier, FeatureFlags flags)
        {
            PullPlanResult plan = PullEngine.PlanFor(
                requirements, qualityLevel, multiplier, player, flags, out _);
            return plan.Feasible
                ? PullKeyRules.NoRoomMessage
                : PullKeyRules.MissingMessage(plan.MissingItemNames);
        }

        /// <summary>
        /// <c>Player.m_placementStatus</c> as a raw <see cref="int"/>, or -1 when it cannot be read.
        /// The field is private, so it is reached by reflection exactly as CraftFromContainers reaches
        /// it (CFC line 1115). Reading it here rather than declaring it in the patch signature keeps
        /// this patch independent of the nested <c>Player.PlacementStatus</c> enum's accessibility —
        /// only the numeric value is compared. The field resolution and the value-to-number step are
        /// both the pure <see cref="BuildPullRules.PlacementStatusFrom"/>, which has its own test for
        /// the renamed-field case; a missing field (a later game version) reads as -1, which
        /// <see cref="BuildPullRules.IsPlacementValid"/> treats as not valid, so an unknown layout fails
        /// closed: the press is swallowed with nothing pulled and nothing placed.
        /// </summary>
        private static int PlacementStatus(Player player)
        {
            if (player == null)
                return -1;

            // A null field means a later game version renamed it: PlacementStatusFrom reads that as the
            // -1 sentinel (fail closed), the same way it reads an unreadable value.
            return BuildPullRules.PlacementStatusFrom(
                PlacementStatusField != null,
                PlacementStatusField != null ? PlacementStatusField.GetValue(player) : null);
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
        /// <para>
        /// Internal rather than private because the station patches read the fill-all modifier with it
        /// (<c>StationPatches.FillAllHeld</c>), so every held modifier is read by one implementation.
        /// </para>
        /// </summary>
        internal static bool KeyHeld(string key)
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
