using System.Collections.Generic;
using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using CraftFromDrawersAndChests.Pull;
using CraftFromDrawersAndChests.Storage;
using HarmonyLib;
using UnityEngine;

namespace CraftFromDrawersAndChests.Patches
{
    /// <summary>
    /// Makes crafting and building draw their materials from chests and drawers as well as the
    /// player's inventory, and keeps the all-or-nothing rule.
    /// <para>
    /// Three patch sites. Two are postfixes on the game's requirement checks, so the panel's "do I
    /// have the materials" answer — and therefore whether the recipe is offered at all — counts
    /// storage: <c>Player.HaveRequirementItems</c> for crafting and <c>Player.HaveRequirements(Piece,
    /// Player.RequirementMode)</c> for building. The third is a prefix on <c>Player.ConsumeResources</c>,
    /// the single method the game consumes a craft's or a piece's materials through: it pulls the
    /// shortfall into the player's inventory first and then lets vanilla consume it from there, which
    /// is CraftFromContainers' model kept.
    /// </para>
    /// <para>
    /// <b>All-or-nothing.</b> The plan is computed whole before anything moves, and only the shortfall
    /// below what the player carries is asked of storage. If the plan is infeasible, or if a line
    /// cannot be fully secured (a source exhausted before the amount is met), the prefix returns false
    /// so vanilla consumes <b>nothing</b>: an item already pulled sits in the player's inventory and
    /// is recoverable, whereas an item half-consumed is not. This is the design's top anti-goal.
    /// <see cref="PullRunner"/> owns the per-line reaction: a delivery routed to another player counts
    /// as secured (the items have already left the storage), and a short-but-progressing delivery is
    /// retried before a line is ever declared failed, so the deterministic case that used to hand out
    /// a free crafted item is closed.
    /// </para>
    /// </summary>
    internal static class CraftPatches
    {
        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        /// <summary>
        /// The crafting recipe's material check. A postfix that only ever turns a <c>false</c> into a
        /// <c>true</c>: the station, its level, the recipe's knowledge and its DLC were all checked by
        /// <c>Player.HaveRequirements(Recipe, ...)</c> before it called here, so the one thing left to
        /// add is storage. <paramref name="discover"/> is left alone — learning a recipe must not
        /// depend on what is in a chest.
        /// <para>
        /// A <c>requireOnlyOneIngredient</c> recipe is deliberately left unanswered. Vanilla's answer
        /// for one already means "any single ingredient is present", and the craft consumes the one
        /// ingredient the <i>player's own inventory</i> provides — <c>InventoryGui.DoCrafting</c> takes
        /// the single requirement item chosen by <c>Recipe.GetAmount</c> (via
        /// <c>Player.GetFirstRequiredItem</c>) and removes it with <c>Inventory.RemoveItem</c>, never
        /// through <c>ConsumeResources</c>, so the pull path this mod installs never runs for such a
        /// recipe. Offering it on the strength of storage would therefore show the player a craftable
        /// recipe that <c>DoCrafting</c> then refuses. Deferring to vanilla keeps the panel honest.
        /// </para>
        /// </summary>
        [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
        [HarmonyPostfix]
        private static void HaveRequirementItemsPostfix(
            Player __instance, Recipe piece, bool discover, int qualityLevel, int amount, ref bool __result)
        {
            if (__result || discover || piece == null || __instance == null || !Enabled())
                return;

            // See the remarks: a single-ingredient recipe cannot be supplied from storage, so vanilla's
            // own any-one answer is the only one consistent with what DoCrafting will do.
            if (piece.m_requireOnlyOneIngredient)
                return;

            __result = CanReach(__instance, piece.m_resources, qualityLevel, amount);
        }

        /// <summary>
        /// The building piece's requirement check. Only the two material-availability modes are
        /// augmented: <c>CanBuild</c> asks "does the player have the materials", which storage may
        /// answer, and <c>CanAlmostBuild</c> asks "has the player any of each material", which decides
        /// whether the piece is offered in the build menu. <c>IsKnown</c> is about which materials the
        /// player has ever picked up, so storage must not make an unknown material known.
        /// <para>
        /// Vanilla checks the crafting station and the piece's DLC <b>inside</b> this method, before
        /// its material loop, and this postfix runs only when vanilla returned <c>false</c> — so that
        /// <c>false</c> might be the station gate, not a material shortfall. The gate is re-checked
        /// before storage is allowed to answer, so materials in a chest can never stand in for a
        /// station the player is not at.
        /// </para>
        /// </summary>
        [HarmonyPatch(typeof(Player), "HaveRequirements", new System.Type[] { typeof(Piece), typeof(Player.RequirementMode) })]
        [HarmonyPostfix]
        private static void HaveRequirementsPiecePostfix(
            Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            if (__result || piece == null || __instance == null || !Enabled())
                return;

            if (mode != Player.RequirementMode.CanBuild && mode != Player.RequirementMode.CanAlmostBuild)
                return;

            if (!StationAndDlcAllow(__instance, piece, mode))
                return;

            __result = mode == Player.RequirementMode.CanAlmostBuild
                ? HasAnyOfEach(__instance, piece.m_resources)
                : CanReach(__instance, piece.m_resources, 0, 1);
        }

        /// <summary>
        /// The consume path. Pulls what the player lacks into their inventory, then lets vanilla
        /// consume it. Returns false — skipping the consumption entirely — whenever the whole
        /// requirement cannot be secured, so a partial pull is never followed by a partial eat.
        /// </summary>
        [HarmonyPatch(typeof(Player), "ConsumeResources")]
        [HarmonyPrefix]
        private static bool ConsumeResourcesPrefix(
            Player __instance, Piece.Requirement[] requirements, int qualityLevel, int multiplier)
        {
            if (__instance == null || requirements == null || !Enabled())
                return true;

            IReadOnlyList<IStorageSource> sources;
            PullPlanResult plan;
            BuildPlan(__instance, requirements, qualityLevel, multiplier, out sources, out plan);

            if (!plan.Feasible)
            {
                Log.LogWarning("Not consuming: the materials are no longer within range (" +
                               string.Join(", ", plan.MissingItemNames) + ").");
                return false;
            }

            if (plan.Lines.Count > 0 && !PullEngine.Execute(plan, sources, __instance))
            {
                Log.LogWarning("Not consuming: a storage pull could not be completed, so the " +
                               "materials already moved are left in the inventory rather than eaten.");
                return false;
            }

            // The player now holds everything the recipe needs, so vanilla's own consume finds it.
            return true;
        }

        /// <summary>Whether the mod should act at all: the configuration snapshot must exist and be enabled.</summary>
        private static bool Enabled()
        {
            FeatureFlags flags = PluginConfig.Flags;
            return flags != null && flags.ModEnabled;
        }

        /// <summary>
        /// Whether storage can cover the part of <paramref name="resources"/> the player does not
        /// already hold. This is the requirement check's answer: the planner reports infeasible
        /// exactly when a shortfall cannot be met, so nothing more is needed here.
        /// </summary>
        private static bool CanReach(
            Player player, Piece.Requirement[] resources, int qualityLevel, int multiplier)
        {
            IReadOnlyList<IStorageSource> sources;
            PullPlanResult plan;
            BuildPlan(player, resources, qualityLevel, multiplier, out sources, out plan);
            return plan.Feasible;
        }

        /// <summary>
        /// Builds the plan for <paramref name="resources"/>: collects the eligible sources (once per
        /// frame) and asks the planner for the shortfall below what the player already carries. The
        /// collected sources are returned too, so the caller can hand the very same list to
        /// <see cref="PullEngine.Execute"/> and have its plan lines resolve.
        /// </summary>
        private static void BuildPlan(
            Player player,
            Piece.Requirement[] resources,
            int qualityLevel,
            int multiplier,
            out IReadOnlyList<IStorageSource> sources,
            out PullPlanResult plan)
        {
            FeatureFlags flags = PluginConfig.Flags;
            Vec3 origin = PullEngine.OriginOf(player);
            sources = PullEngine.Collect(origin, flags);

            Inventory inventory = player.GetInventory();
            PullRequest[] requests = PullEngine.BuildRequests(
                resources, qualityLevel, multiplier, name => PullEngine.CountHeld(inventory, name));

            plan = PullEngine.Plan(requests, sources, origin, flags, PullEngine.AtUpgrader(player));
        }

        /// <summary>
        /// Whether the player holds at least one of every required material, between their inventory
        /// and storage — the <c>CanAlmostBuild</c> question. A material the player already carries one
        /// of short-circuits; otherwise any source holding any of it is enough. A source that cannot
        /// be taken from synchronously (a chest another player owns) is not counted, so this agrees
        /// with what the pull could actually do.
        /// </summary>
        private static bool HasAnyOfEach(Player player, Piece.Requirement[] resources)
        {
            if (resources == null)
                return true;

            FeatureFlags flags = PluginConfig.Flags;
            Vec3 origin = PullEngine.OriginOf(player);
            IReadOnlyList<IStorageSource> sources = PullEngine.Collect(origin, flags);
            Inventory inventory = player.GetInventory();

            for (int i = 0; i < resources.Length; i++)
            {
                Piece.Requirement requirement = resources[i];
                if (requirement == null || requirement.m_resItem == null || requirement.m_amount <= 0)
                    continue;

                string prefab = PullEngine.PrefabName(requirement);
                if (string.IsNullOrEmpty(prefab))
                    continue;

                if (PullEngine.CountHeld(inventory, prefab) > 0)
                    continue;

                if (AnySourceHolds(sources, prefab))
                    continue;

                return false;
            }
            return true;
        }

        /// <summary>Whether any eligible source the pull could take from synchronously holds any of <paramref name="prefab"/>.</summary>
        private static bool AnySourceHolds(IReadOnlyList<IStorageSource> sources, string prefab)
        {
            for (int i = 0; i < sources.Count; i++)
            {
                IStorageSource source = sources[i];
                if (source == null || !source.IsValid || !PullEngine.CanServeSynchronously(source))
                    continue;

                if (source.Count(prefab) > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Re-checks the crafting-station and DLC gates vanilla applies inside
        /// <c>Player.HaveRequirements(Piece, mode)</c> before its material loop. Returns true when
        /// those gates would have let the method reach its material loop — the only case in which
        /// storage is allowed to answer. <c>CanAlmostBuild</c> (and <c>IsKnown</c>) require the station
        /// to be <i>known</i>; <c>CanBuild</c> requires it to be <i>in range</i>, unless the world's
        /// "no workbench needed" global key is set.
        /// </summary>
        private static bool StationAndDlcAllow(Player player, Piece piece, Player.RequirementMode mode)
        {
            CraftingStation station = piece.m_craftingStation;
            if (station != null)
            {
                if (mode == Player.RequirementMode.CanAlmostBuild || mode == Player.RequirementMode.IsKnown)
                {
                    if (player.m_knownStations == null || !player.m_knownStations.ContainsKey(station.m_name))
                        return false;
                }
                else if (CraftingStation.HaveBuildStationInRange(station.m_name, player.transform.position) == null
                         && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench))
                {
                    return false;
                }
            }

            string dlc = piece.m_dlc;
            if (!string.IsNullOrEmpty(dlc) && DLCMan.instance != null && !DLCMan.instance.IsDLCInstalled(dlc))
                return false;

            return true;
        }
    }
}
