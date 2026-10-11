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
    /// <b>Storage yes is not enough — the destination must have room too.</b> A requirement check
    /// answers yes only when storage covers the shortfall <i>and</i> the player's inventory can accept
    /// what the pull would deliver (<see cref="DestinationCapacity"/>). Vanilla creates the crafted
    /// item or places the piece <i>before</i> it consumes the materials
    /// (<c>InventoryGui.DoCrafting</c> and <c>Player.UpdatePlacement</c>), and the pull moves its lines
    /// into the inventory before vanilla eats them, so a destination that cannot take the shortfall
    /// makes the pull fail — and skipping consumption then would hand out an unpaid-for craft or
    /// placement. The crafting gate also charges the room the crafted output itself takes, because
    /// vanilla adds it before consuming. Storage availability alone would be the same defect as the
    /// original one, only moved from the source to the destination.
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

            __result = CanReach(__instance, piece.m_resources, qualityLevel, amount, CraftedOutput(piece, qualityLevel, amount));
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
                : CanReach(__instance, piece.m_resources, 0, 1, null);
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
        /// Whether the requirement can actually be paid for: storage must cover the part of
        /// <paramref name="resources"/> the player does not already hold <b>and</b> the player's
        /// inventory must be able to accept what the pull would deliver into it. Both are needed
        /// because the pull is not instantaneous: vanilla creates the crafted item or places the piece
        /// <i>before</i> it consumes the materials, so a gate that says yes to a pull the destination
        /// cannot take hands the player a free craft or placement.
        /// <para>
        /// <paramref name="reserved"/> is the crafted <i>output</i> on the crafting path. Vanilla adds
        /// it to the inventory before consuming the materials, so it competes with the pull for the
        /// same room; listing it first makes the destination check account for it. The building path
        /// has no such output and passes null. The building path's pieces are placed into the world,
        /// not the inventory, so only the pulled shortfall is charged against the destination there.
        /// </para>
        /// </summary>
        private static bool CanReach(
            Player player,
            Piece.Requirement[] resources,
            int qualityLevel,
            int multiplier,
            DestinationCapacity.Delivery? reserved)
        {
            IReadOnlyList<IStorageSource> sources;
            PullPlanResult plan;
            BuildPlan(player, resources, qualityLevel, multiplier, out sources, out plan);
            return plan.Feasible && DestinationAllows(player, resources, plan, reserved);
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
        /// Whether the player's own inventory can accept everything the pull will deliver into it —
        /// R7's second half of the gate. The pull moves its lines into the player's inventory before
        /// vanilla consumes them, so a destination that cannot take them makes the pull fail, and a
        /// failed pull leaves the already-created crafted item or placed piece unpaid for.
        /// <para>
        /// The decision itself is shared with the ctrl-click pull
        /// (<see cref="PullEngine.DestinationAllows"/>), so the crafting, building and pull paths
        /// cannot drift into a parallel free-space rule; this method only supplies the crafted
        /// output that must be charged ahead of the shortfall.
        /// </para>
        /// </summary>
        private static bool DestinationAllows(
            Player player,
            Piece.Requirement[] resources,
            PullPlanResult plan,
            DestinationCapacity.Delivery? reserved)
        {
            return PullEngine.DestinationAllows(player, resources, plan, reserved);
        }

        /// <summary>
        /// The crafted output as a destination delivery, so the crafting gate charges its room before
        /// the pulled shortfall. A normal craft adds <c>Recipe.m_item</c> at the craft's quality level
        /// and the current world level, but <b>not</b> in the bare amount <c>Recipe.GetAmount</c> reports:
        /// <c>InventoryGui.DoCrafting</c> then adds a crafting-skill bonus on top — when the current
        /// station has a crafting skill and the output stacks higher than one it rolls a number of times
        /// and every successful roll adds the running bonus. So this reserves
        /// <see cref="DestinationCapacity.ReservedOutputAmount"/>: the base amount plus the maximum that
        /// bonus can add (an upper bound, so an over-reservation only declines a marginal craft whereas
        /// an under-reservation would hand out a free item). Null (no reservation) when the recipe cannot
        /// describe its output, which leaves the shortfall check alone rather than inventing a number.
        /// <para>
        /// An upgrade reserves <b>nothing</b>: that path removes the item being upgraded and re-adds it
        /// at the same position, so it needs no new room, and vanilla skips its own output guard there.
        /// The upgrade is recognised from the craft context the requirement check was made at
        /// (<see cref="DestinationCapacity.IsUpgrade"/>), which is <c>OnCraftPressed</c>'s own condition
        /// one step earlier: <c>InventoryGui.m_craftUpgradeItem</c> is only armed when the button is
        /// pressed, so reading it here would miss the first-press upgrade and reserve room for output
        /// that is re-added in place.
        /// </para>
        /// </summary>
        private static DestinationCapacity.Delivery? CraftedOutput(Recipe piece, int qualityLevel, int amount)
        {
            if (piece == null || piece.m_item == null)
                return null;

            ItemDrop.ItemData data = piece.m_item.m_itemData;
            if (data == null || data.m_shared == null || data.m_shared.m_maxStackSize <= 0)
                return null;

            int baseAmount = piece.GetAmount(qualityLevel, out int _, out ItemDrop.ItemData _, amount);

            // The upgrade condition is the craft context the caller passed to the requirement check, not
            // InventoryGui.m_craftUpgradeItem: that field is armed only in OnCraftPressed, i.e. after the
            // panel gate has already decided whether the craft button is clickable, so on a first press
            // it is still null. Every craft path derives the quality level it passes from the item being
            // upgraded (upgradeItem.m_quality + 1, else 1), so the call-site value carries the answer.
            bool isUpgrade = DestinationCapacity.IsUpgrade(qualityLevel);

            // InventoryGui.instance still owns the live per-roll bonus value; the panel and DoCrafting
            // run against the same instance, so reading it here is what the game itself will do. A null
            // instance means no craft is in flight, so no bonus can be added.
            InventoryGui gui = InventoryGui.instance;
            int perRollBonus = gui != null ? gui.m_craftBonusAmount : 0;

            int produced = DestinationCapacity.ReservedOutputAmount(baseAmount, perRollBonus, amount, isUpgrade);
            if (produced <= 0)
                return null;

            return new DestinationCapacity.Delivery(
                data.m_shared.m_name, qualityLevel, (byte)Game.m_worldLevel, produced, data.m_shared.m_maxStackSize);
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
