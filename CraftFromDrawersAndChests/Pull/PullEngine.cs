using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using CraftFromDrawersAndChests.Storage;
using UnityEngine;

namespace CraftFromDrawersAndChests.Pull
{
    /// <summary>
    /// Turns crafting and building requirements into pulls from storage, and executes the resulting
    /// plan. Game-facing: it reads the player, the crafting station, the game clock and the storage
    /// sources, so it cannot live in the pure <c>Logic/</c> layer. The decisions themselves — which
    /// sources are eligible (<see cref="StorageRegistry"/>), how a requirement maps to the planner's
    /// request and how a plan's lines are executed — are the only things here; the all-or-nothing
    /// planning lives in <see cref="PullPlan"/>.
    /// <para>
    /// <b>Prefab names, not localized names.</b> Every identifier crossing into <c>Logic</c> is an
    /// item's <b>prefab</b> name (<c>"Wood"</c>), never its localized <c>m_shared.m_name</c>
    /// (<c>"$item_wood"</c>): a drawer's ZDO stores a prefab name, the chest source matches on
    /// <c>item.m_dropPrefab.name</c> and the planner keys storage by the same string (Ruling 1). The
    /// prefab name of a requirement is the GameObject name behind its <c>m_resItem</c>.
    /// </para>
    /// <para>
    /// <b>Shortfall.</b> A pull for crafting or building supplies only what the player is <i>short</i>
    /// of, not the whole requirement: a player who already carries the item must not have it taken
    /// from storage in its place, and planning the full requirement would make an otherwise-craftable
    /// recipe infeasible whenever storage held less than the total (see
    /// <see cref="BuildRequests(Piece.Requirement[], int, int, Func{string, int})"/>).
    /// </para>
    /// </summary>
    public static class PullEngine
    {
        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        // The eligible-source list is collected at most once per frame: the building HUD
        // (Hud.UpdatePieceBuildStatus) re-checks every buildable piece each frame, and the crafting
        // panel re-checks the selected recipe each frame, so without this a single frame would rescan
        // every tracked container and query Item Drawers once per piece and per recipe. The same
        // per-frame cache is what CraftFromContainers kept. It is also what keeps a plan's SourceIds
        // valid for Execute: Plan and Execute are handed the very same list within the frame.
        private static IReadOnlyList<IStorageSource> cachedSources;
        private static Vec3 cachedOrigin;
        private static FeatureFlags cachedFlags;
        private static int cachedFrame = -1;

        /// <summary>
        /// The eligible storage for <paramref name="origin"/>, collected at most once per frame. See
        /// <see cref="StorageRegistry.Collect"/> for what each source is; the cache keys on the exact
        /// origin, the exact flags instance and the frame number, so a new frame or a different
        /// origin re-collects and the list is never observed stale across frames.
        /// </summary>
        public static IReadOnlyList<IStorageSource> Collect(Vec3 origin, FeatureFlags flags)
        {
            int frame = Time.frameCount;
            if (cachedSources != null
                && frame == cachedFrame
                && ReferenceEquals(cachedFlags, flags)
                && cachedOrigin.X == origin.X
                && cachedOrigin.Y == origin.Y
                && cachedOrigin.Z == origin.Z)
            {
                return cachedSources;
            }

            cachedSources = StorageRegistry.Collect(origin, flags);
            cachedOrigin = origin;
            cachedFlags = flags;
            cachedFrame = frame;
            return cachedSources;
        }

        /// <summary>The prefab name behind a requirement's item, or null when it has no resolvable item.</summary>
        /// <param name="requirement">A recipe or piece requirement; may be null.</param>
        public static string PrefabName(Piece.Requirement requirement)
        {
            if (requirement == null || requirement.m_resItem == null)
                return null;

            GameObject go = requirement.m_resItem.gameObject;
            return go != null ? go.name : null;
        }

        /// <summary>
        /// Turns requirements into the planner's requests, each reduced to the part the player does
        /// <b>not</b> already hold — the shortfall below <c>GetAmount(qualityLevel) * multiplier</c>.
        /// The held amount is read from <paramref name="inventory"/> with the same three-part item
        /// identity the destination model matches on (shared name, <b>quality</b>, world level; see
        /// <see cref="DrawerTake.MatchingAmount"/>), so a stack counts towards a requirement only when
        /// it really is the item the requirement names — the two halves of the availability question
        /// cannot disagree about which stacks are the item. A requirement the player already covers
        /// produces no request at all.
        /// <para>
        /// This is the entry point the crafting and building pulls (and their requirement gates) share.
        /// It deliberately diverges from CraftFromContainers, which asks storage for the whole
        /// requirement with no held-subtraction (CFC line 1175, <c>GetAmount(qualityLevel) * multiple</c>)
        /// and therefore over-pulls; asking only for the shortfall is the safe direction and matches
        /// this project's own gate, whose whole purpose is that a "yes" means the pull can deliver what
        /// the action needs.
        /// </para>
        /// </summary>
        /// <param name="requirements">The recipe's or piece's requirements; may be null.</param>
        /// <param name="qualityLevel">The quality the requirement is evaluated at (0 or 1 for building).</param>
        /// <param name="multiplier">How many of the recipe are being made (1 for building).</param>
        /// <param name="inventory">The player's inventory the held counts are read from; may be null, which reads as "holds none".</param>
        public static PullRequest[] BuildShortfallRequests(
            Piece.Requirement[] requirements, int qualityLevel, int multiplier, Inventory inventory)
        {
            IReadOnlyDictionary<string, int> held = HeldCounts(requirements, inventory);
            return BuildRequests(requirements, qualityLevel, multiplier,
                name => held.TryGetValue(name, out int amount) ? amount : 0);
        }

        /// <summary>
        /// How many of each requirement's item the inventory already holds, keyed by the requirement's
        /// prefab name (the identifier the requests use). A held stack counts only when it is the same
        /// item as the requirement names — shared name, quality and world level — the one predicate
        /// <see cref="DrawerTake.MatchingAmount"/> exposes, so what is counted as held here is exactly
        /// what would be counted as room for the delivery of that same item. A requirement with no
        /// resolvable item contributes no entry (it produces no request either), and two requirements
        /// that share a prefab name share the first entry — the planner keys its stock by that name too.
        /// The inventory is reduced to the pure stack view at most once, and only if there is a
        /// requirement whose identity can be read.
        /// </summary>
        private static IReadOnlyDictionary<string, int> HeldCounts(
            Piece.Requirement[] requirements, Inventory inventory)
        {
            var held = new Dictionary<string, int>(StringComparer.Ordinal);
            if (requirements == null || inventory == null)
                return held;

            List<DrawerTake.InventoryStack> stacks = null;
            for (int i = 0; i < requirements.Length; i++)
            {
                Piece.Requirement requirement = requirements[i];
                if (requirement == null)
                    continue;

                string prefab = PrefabName(requirement);
                if (string.IsNullOrEmpty(prefab) || held.ContainsKey(prefab))
                    continue;

                ItemDrop.ItemData data = ItemDataOf(requirement);
                if (data == null || data.m_shared == null || string.IsNullOrEmpty(data.m_shared.m_name))
                    continue;

                if (stacks == null)
                    stacks = InventoryStacks(inventory);

                held[prefab] = DrawerTake.MatchingAmount(
                    data.m_shared.m_name, data.m_quality, data.m_worldLevel, stacks);
            }
            return held;
        }

        /// <summary>
        /// Turns requirements into the planner's requests, each reduced to the part the player does
        /// not already hold. <paramref name="held"/> maps a prefab name to how many the player
        /// currently carries; a requirement whose shortfall is zero produces no request, so the
        /// player's own materials are consumed first and storage is asked only for the remainder.
        /// Pass null to request the full requirement (the "pull the whole recipe" case).
        /// </summary>
        /// <param name="requirements">The recipe's or piece's requirements; may be null.</param>
        /// <param name="qualityLevel">The quality the requirement is evaluated at (0 or 1 for building).</param>
        /// <param name="multiplier">How many of the recipe are being made (1 for building).</param>
        /// <param name="held">Prefab name to amount the player already holds; null means "holds none".</param>
        public static PullRequest[] BuildRequests(
            Piece.Requirement[] requirements, int qualityLevel, int multiplier, Func<string, int> held)
        {
            if (requirements == null)
                return Array.Empty<PullRequest>();

            var requests = new List<PullRequest>(requirements.Length);
            for (int i = 0; i < requirements.Length; i++)
            {
                Piece.Requirement requirement = requirements[i];
                if (requirement == null)
                    continue;

                // The prefab name is the only identifier the storage layer and the planner share
                // (Ruling 1); a requirement whose item cannot be resolved is skipped, never guessed at.
                string prefab = PrefabName(requirement);
                if (string.IsNullOrEmpty(prefab))
                    continue;

                int needed = requirement.GetAmount(qualityLevel) * multiplier;
                if (needed <= 0)
                    continue;

                int alreadyHeld = held != null ? held(prefab) : 0;
                int amount = PullMath.Shortfall(needed, alreadyHeld);
                if (amount <= 0)
                    continue;

                requests.Add(new PullRequest(prefab, prefab, amount, requirement.m_upgraderResource));
            }
            return requests.ToArray();
        }

        /// <summary>
        /// Plans a pull for <paramref name="requirements"/> against the storage eligible for the
        /// player's position, asking storage only for the shortfall below what the player already
        /// carries (<see cref="BuildShortfallRequests"/>) — the very notion the crafting and building
        /// requirement gates use, so a pull can only demand what a "yes" already promised it could
        /// deliver. The collected sources are returned so the caller can hand the very same list to
        /// <see cref="Execute"/> and have its plan lines resolve. The upgrader flag is derived from the
        /// player's current crafting station.
        /// </summary>
        /// <param name="requirements">The recipe's or piece's requirements; may be null.</param>
        /// <param name="qualityLevel">The quality the requirement is evaluated at (0 or 1 for building).</param>
        /// <param name="multiplier">How many of the recipe are being made (1 for building).</param>
        /// <param name="player">The player whose held materials reduce the request, and whose current station gates upgrader resources; null reads as holding none and at no station.</param>
        /// <param name="flags">The resolved configuration snapshot.</param>
        /// <param name="sources">The eligible storage the plan was built against, for <see cref="Execute"/>.</param>
        public static PullPlanResult PlanFor(
            Piece.Requirement[] requirements, int qualityLevel, int multiplier,
            Player player, FeatureFlags flags, out IReadOnlyList<IStorageSource> sources)
        {
            Vec3 origin = OriginOf(player);
            sources = Collect(origin, flags);
            Inventory inventory = player != null ? player.GetInventory() : null;
            PullRequest[] requests = BuildShortfallRequests(requirements, qualityLevel, multiplier, inventory);
            return Plan(requests, sources, origin, flags, AtUpgrader(player));
        }

        /// <summary>
        /// Plans <paramref name="requests"/> against an already-collected source list, so the same
        /// list can then be handed to <see cref="Execute"/> and its line SourceIds resolve. The
        /// <paramref name="allowUpgraderResources"/> flag is passed straight to
        /// <see cref="PullPlan.Plan"/>: false skips upgrader requirements entirely rather than
        /// failing on them, which is what a player not at an upgrader needs.
        /// </summary>
        public static PullPlanResult Plan(
            IReadOnlyList<PullRequest> requests,
            IReadOnlyList<IStorageSource> sources,
            Vec3 origin,
            FeatureFlags flags,
            bool allowUpgraderResources)
        {
            IReadOnlyList<StorageStock> stocks = BuildStocks(sources, requests, origin);
            bool leaveOne = flags != null && flags.LeaveOne;
            bool byDistance = flags != null && flags.PullByDistance;
            return PullPlan.Plan(requests, stocks, leaveOne, byDistance, allowUpgraderResources);
        }

        /// <summary>
        /// Executes every line of a feasible plan, taking each line's amount from its source into
        /// <paramref name="player"/>'s inventory. Returns true only when <b>every</b> line was
        /// satisfied; the first line that cannot be is a failure and the walk stops, leaving whatever
        /// was already taken in the player's inventory.
        /// <para>
        /// The sequencing — the three <see cref="IStorageSource.Take"/> answer shapes, the retry of a
        /// short-but-progressing delivery, and the all-or-nothing outcome — lives in the pure
        /// <see cref="PullRunner"/>; this method only supplies the game-facing decide-and-take step
        /// (resolve the line's source, call <see cref="IStorageSource.Take"/>) and the logger. See
        /// <see cref="PullRunner.Run"/> for how each shape is handled, in particular that
        /// <see cref="ChestTake.Deferred"/> counts as <b>satisfied</b> (the items have already left the
        /// storage) rather than failed.
        /// </para>
        /// </summary>
        public static bool Execute(PullPlanResult plan, IReadOnlyList<IStorageSource> sources, Player player)
        {
            // An infeasible plan secured nothing by definition, and Lines is empty for one; report
            // failure rather than the vacuous success an empty walk would give.
            if (plan == null || !plan.Feasible)
                return false;

            Inventory destination = player != null ? player.GetInventory() : null;
            if (destination == null)
                return false;

            IReadOnlyDictionary<string, IStorageSource> byId = ById(sources);
            return PullRunner.Run(
                plan.Lines,
                (line, amount) =>
                {
                    IStorageSource source;
                    if (!byId.TryGetValue(line.SourceId, out source) || source == null || !source.IsValid)
                    {
                        Log.LogWarning("Pull: the source for " + line.Amount + "x '" + line.ItemName +
                                       "' is gone before it could be taken; treating it as exhausted.");
                        return 0;
                    }
                    return source.Take(line.ItemName, amount, destination);
                },
                message => Log.LogWarning(message));
        }

        /// <summary>
        /// Pulls a recipe's or a building piece's materials into <paramref name="player"/>'s own
        /// inventory — the "pull that instead of doing it" behaviour. Storage is asked only for the
        /// <b>shortfall</b> below what the player already carries (see <see cref="PlanFor"/>): the very
        /// notion the requirement gates use, so a pull can never demand an amount the gate did not
        /// already promise storage could deliver. Asking for the whole requirement instead — as
        /// CraftFromContainers does — would over-pull (taking an item the player already holds) and, on
        /// this codebase's shortfall-based gate, could refuse a pull for materials the player is holding.
        /// <para>
        /// Returns true when the pull completed <b>or</b> when there was nothing to fetch (the player
        /// already holds every requirement, or there are no requirements at all), in which case
        /// <paramref name="requested"/> is false so the caller reports nothing — a no-op pull has
        /// neither a success nor a failure to announce. It returns false, having consumed nothing and
        /// dropped nothing, when storage cannot cover the shortfall <b>or</b> when the player's
        /// inventory cannot accept everything the pull would deliver. The destination check is what
        /// keeps items off the ground — the design's top anti-goal (Review Focus 2): a pull that moved
        /// items into a full inventory would leave the remainder on the floor, so the whole pull is
        /// declined before a single item moves. The check reuses the very destination model the
        /// crafting path uses (<see cref="DestinationAllows"/>), so the two cannot disagree.
        /// </para>
        /// </summary>
        /// <param name="requirements">The recipe's or piece's requirements; may be null, which has nothing to fetch.</param>
        /// <param name="qualityLevel">The quality the requirement is evaluated at (an upgrade is &gt; 1, building is 0).</param>
        /// <param name="multiplier">How many are being pulled (1 unless multicrafting).</param>
        /// <param name="player">The player to deliver into; null refuses.</param>
        /// <param name="flags">The resolved configuration snapshot; null refuses.</param>
        /// <param name="requested">
        /// Whether the pull had anything to fetch. False means a no-op pull: nothing moved and there is
        /// nothing to report, so the caller shows neither the pulled message nor a refusal reason.
        /// </param>
        public static bool PullToPlayer(
            Piece.Requirement[] requirements, int qualityLevel, int multiplier, Player player, FeatureFlags flags,
            out bool requested)
        {
            requested = false;
            if (player == null || flags == null)
                return false;

            // PlanFor is the single planning entry point: it collects the eligible sources once and
            // asks storage only for the shortfall below what the player carries.
            PullPlanResult plan = PlanFor(
                requirements, qualityLevel, multiplier, player, flags, out IReadOnlyList<IStorageSource> sources);

            // Nothing to fetch: no requirements at all, or the player already holds every one of them.
            // The pull is a no-op, so report nothing rather than a misleading success or room failure.
            if (plan.Feasible && plan.Lines.Count == 0)
                return true;

            requested = true;

            if (!plan.Feasible)
                return false;

            // No crafted output is reserved here: the pull is the whole action, nothing is crafted.
            if (!DestinationAllows(player, requirements, plan, null))
                return false;

            return Execute(plan, sources, player);
        }

        /// <summary>
        /// Whether the player's own inventory can accept everything a plan would deliver into it —
        /// the destination half of the availability gate, shared verbatim by the crafting path
        /// (<c>Patches.CraftPatches</c>) and the ctrl-click pull (<see cref="PullToPlayer"/>) so the
        /// two cannot diverge into a "parallel rule".
        /// <para>
        /// The inventory is reduced to the pure <see cref="DrawerTake.InventoryStack"/> view and its
        /// empty-slot count, and each plan line becomes a <see cref="DestinationCapacity.Delivery"/>
        /// built from the item the requirement names; <see cref="DestinationCapacity.CanAccept"/>
        /// then applies the very predicate the drawer path removes behind. A line whose item cannot
        /// be described is declined rather than assumed deliverable, so an unprovable case fails
        /// closed. <paramref name="reserved"/> is charged first — the crafting path's crafted output,
        /// which vanilla adds before consuming; the pull passes null.
        /// </para>
        /// </summary>
        /// <param name="player">The delivery destination's owner; null refuses.</param>
        /// <param name="requirements">The recipe's or piece's requirements, used to describe each line.</param>
        /// <param name="plan">The plan whose lines are to be delivered; null refuses.</param>
        /// <param name="reserved">A delivery to charge ahead of the lines (the crafted output), or null.</param>
        public static bool DestinationAllows(
            Player player,
            Piece.Requirement[] requirements,
            PullPlanResult plan,
            DestinationCapacity.Delivery? reserved)
        {
            if (plan == null)
                return false;

            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null)
                return false;

            var deliveries = new List<DestinationCapacity.Delivery>(plan.Lines.Count + 1);

            // Order matters on the crafting path: the crafted output is placed before the pull runs.
            if (reserved.HasValue)
                deliveries.Add(reserved.Value);

            for (int i = 0; i < plan.Lines.Count; i++)
            {
                PullLine line = plan.Lines[i];
                if (line.Amount <= 0)
                    continue;

                ItemDrop.ItemData data = ItemDataOf(FindRequirement(requirements, line.ItemName));
                if (data == null || data.m_shared == null || data.m_shared.m_maxStackSize <= 0)
                    return false;

                deliveries.Add(new DestinationCapacity.Delivery(
                    data.m_shared.m_name, data.m_quality, data.m_worldLevel, line.Amount, data.m_shared.m_maxStackSize));
            }

            return DestinationCapacity.CanAccept(
                InventoryStacks(inventory), DestinationEmptySlots(inventory), deliveries);
        }

        /// <summary>The requirement whose item carries the given prefab name, or null when none does.</summary>
        private static Piece.Requirement FindRequirement(Piece.Requirement[] resources, string prefab)
        {
            if (resources == null || string.IsNullOrEmpty(prefab))
                return null;

            for (int i = 0; i < resources.Length; i++)
            {
                Piece.Requirement requirement = resources[i];
                if (requirement == null)
                    continue;

                if (string.Equals(PrefabName(requirement), prefab, StringComparison.Ordinal))
                    return requirement;
            }
            return null;
        }

        /// <summary>The item definition a requirement names, or null when it has no resolvable item.</summary>
        private static ItemDrop.ItemData ItemDataOf(Piece.Requirement requirement)
        {
            if (requirement == null || requirement.m_resItem == null)
                return null;

            return requirement.m_resItem.m_itemData;
        }

        /// <summary>
        /// An inventory's stacks as the pure view the merge predicate matches on, read exactly the way
        /// <c>Storage.DrawerSource</c> reads them: shared name, quality, world level, current size and
        /// stack size. Used for both halves of the availability question — as a delivery's destination
        /// (<see cref="DestinationAllows"/>) and as the player's own stock that reduces a request
        /// (<see cref="HeldCounts"/>) — so both match on the same three-part identity. Read-only: the
        /// inventory is never written here.
        /// </summary>
        private static List<DrawerTake.InventoryStack> InventoryStacks(Inventory inventory)
        {
            var stacks = new List<DrawerTake.InventoryStack>();
            if (inventory == null || inventory.m_inventory == null)
                return stacks;

            for (int i = 0; i < inventory.m_inventory.Count; i++)
            {
                ItemDrop.ItemData item = inventory.m_inventory[i];
                if (item == null || item.m_shared == null)
                    continue;

                stacks.Add(new DrawerTake.InventoryStack(
                    item.m_shared.m_name, item.m_quality, item.m_worldLevel, item.m_stack, item.m_shared.m_maxStackSize));
            }
            return stacks;
        }

        /// <summary>The destination's empty slot count, the way <c>Inventory</c> itself computes it.</summary>
        private static int DestinationEmptySlots(Inventory inventory)
        {
            if (inventory == null || inventory.m_inventory == null)
                return 0;

            return inventory.m_width * inventory.m_height - inventory.m_inventory.Count;
        }

        /// <summary>The player's world position as the pure <see cref="Vec3"/> the range maths uses.</summary>
        public static Vec3 OriginOf(Player player)
        {
            if (player == null)
                return new Vec3(0f, 0f, 0f);

            Vector3 p = player.transform.position;
            return new Vec3(p.x, p.y, p.z);
        }

        /// <summary>
        /// Whether the player is currently at an upgrader station, the one condition under which an
        /// upgrader resource (an Idol) may be pulled. A player at no station, or at a non-upgrader
        /// station, must not pull them — vanilla does not consume them there, so pulling them would
        /// silently eat Idols.
        /// </summary>
        public static bool AtUpgrader(Player player)
        {
            if (player == null)
                return false;

            CraftingStation station = player.GetCurrentCraftingStation();
            return station != null && station.m_upgrader;
        }

        /// <summary>
        /// How many of a prefab the inventory holds, counted by <c>item.m_dropPrefab.name</c> — the
        /// same prefab keying <see cref="IStorageSource.Count"/> uses (Ruling 1). A stack whose drop
        /// prefab is unavailable (materialised directly into the inventory) is not counted.
        /// </summary>
        public static int CountHeld(Inventory inventory, string prefabName)
        {
            if (inventory == null || string.IsNullOrEmpty(prefabName))
                return 0;

            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            if (items == null)
                return 0;

            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData item = items[i];
                if (item == null || item.m_dropPrefab == null)
                    continue;

                if (string.Equals(item.m_dropPrefab.name, prefabName, StringComparison.Ordinal))
                    total += item.m_stack;
            }
            return total;
        }

        /// <summary>
        /// One <see cref="StorageStock"/> per (source, requested item name) pair, with the source's
        /// index as its opaque <see cref="StorageStock.SourceId"/> so <see cref="Execute"/> can map a
        /// line back through the same list. A chest another player owns is skipped here rather than
        /// planned against (the design's second hard rule): asking it would route the removal, whose
        /// outcome is not knowable synchronously, so it must be declined <b>before</b> a plan is built.
        /// A destroyed or invalid source is skipped too.
        /// </summary>
        private static IReadOnlyList<StorageStock> BuildStocks(
            IReadOnlyList<IStorageSource> sources, IReadOnlyList<PullRequest> requests, Vec3 origin)
        {
            var stocks = new List<StorageStock>();
            if (sources == null || sources.Count == 0 || requests == null || requests.Count == 0)
                return stocks;

            string[] names = DistinctNames(requests);
            if (names.Length == 0)
                return stocks;

            for (int i = 0; i < sources.Count; i++)
            {
                IStorageSource source = sources[i];
                if (source == null || !source.IsValid)
                    continue;

                if (!CanServeSynchronously(source))
                    continue;

                float distance = source.Position.DistanceTo(origin);
                string id = SourceId(i);
                for (int n = 0; n < names.Length; n++)
                {
                    int amount = source.Count(names[n]);
                    if (amount > 0)
                        stocks.Add(new StorageStock(id, names[n], amount, distance));
                }
            }
            return stocks;
        }

        /// <summary>
        /// Whether a removal from this source is applied locally and its count known on return. Only
        /// a vanilla chest can fail this — when another player owns it, its removal would be routed
        /// and the request already sent by the time <see cref="IStorageSource.Take"/> returns. A
        /// drawer is always local. Public so the requirement checks can agree with the planner about
        /// which sources count as available.
        /// </summary>
        public static bool CanServeSynchronously(IStorageSource source)
        {
            VanillaChestSource chest = source as VanillaChestSource;
            return chest == null || chest.CanTakeSynchronously;
        }

        /// <summary>The distinct, non-empty item names the requests ask for, in first-seen order.</summary>
        private static string[] DistinctNames(IReadOnlyList<PullRequest> requests)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var names = new List<string>();
            for (int i = 0; i < requests.Count; i++)
            {
                string name = requests[i].ItemName;
                if (!string.IsNullOrEmpty(name) && seen.Add(name))
                    names.Add(name);
            }
            return names.ToArray();
        }

        /// <summary>The opaque source key a plan line carries; the source's index in the collected list.</summary>
        private static string SourceId(int index)
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Maps every collected source's index back to the source, for <see cref="Execute"/>.</summary>
        private static IReadOnlyDictionary<string, IStorageSource> ById(IReadOnlyList<IStorageSource> sources)
        {
            var map = new Dictionary<string, IStorageSource>(StringComparer.Ordinal);
            if (sources != null)
            {
                for (int i = 0; i < sources.Count; i++)
                    map[SourceId(i)] = sources[i];
            }
            return map;
        }
    }
}
