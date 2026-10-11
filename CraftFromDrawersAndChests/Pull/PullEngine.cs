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
        /// Turns requirements into the planner's requests, each asking storage for the full amount the
        /// requirement consumes (<c>GetAmount(qualityLevel) * multiplier</c>). Requirements with no
        /// resolvable item or a non-positive amount are dropped; an upgrader resource keeps its
        /// <see cref="PullRequest.IsUpgraderResource"/> flag so the planner can skip it when the
        /// player is not at an upgrader.
        /// </summary>
        public static PullRequest[] BuildRequests(Piece.Requirement[] requirements, int qualityLevel, int multiplier)
        {
            return BuildRequests(requirements, qualityLevel, multiplier, null);
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
        /// Plans a pull for <paramref name="requirements"/> against the storage eligible for
        /// <paramref name="origin"/>, asking storage for the full requirement. The caller supplies
        /// nothing the player already holds, so this is the "pull the whole recipe" entry point; the
        /// crafting and building paths instead build shortfall requests and call
        /// <see cref="Plan(IReadOnlyList{PullRequest}, IReadOnlyList{IStorageSource}, Vec3, FeatureFlags, bool)"/>.
        /// The upgrader flag is derived from the local player's current crafting station.
        /// </summary>
        public static PullPlanResult PlanFor(
            Piece.Requirement[] requirements, int qualityLevel, int multiplier, Vec3 origin, FeatureFlags flags)
        {
            IReadOnlyList<IStorageSource> sources = Collect(origin, flags);
            PullRequest[] requests = BuildRequests(requirements, qualityLevel, multiplier);
            return Plan(requests, sources, origin, flags, AtUpgrader(Player.m_localPlayer));
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
        /// <paramref name="player"/>'s inventory. Returns true only when <b>every</b> line was fully
        /// secured; the first line that cannot be is a failure and the walk stops, leaving whatever
        /// was already taken in the player's inventory.
        /// <para>
        /// <see cref="IStorageSource.Take"/> has three answer shapes and all three are honoured. A
        /// value <c>&gt;= 0</c> is the count actually taken, which may be fewer than the line asked
        /// for (a drawer hands over one stack per delivery; a chest may hold less than was counted), so
        /// anything short of the line's amount fails. <see cref="ChestTake.Deferred"/> (<c>-1</c>) is
        /// <b>not</b> a count: it means the removal was routed to another player and its outcome is not
        /// knowable here, so it too fails — a caller must never treat it as a number. Plans are built
        /// only from synchronously-servable sources (see <see cref="BuildStocks"/>), so a deferred
        /// answer here means a chest changed hands mid-pull; failing is the all-or-nothing response.
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
            IReadOnlyList<PullLine> lines = plan.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                PullLine line = lines[i];

                IStorageSource source;
                if (!byId.TryGetValue(line.SourceId, out source) || source == null || !source.IsValid)
                {
                    Log.LogWarning("Pull aborted: the source for " + line.Amount + "x '" + line.ItemName +
                                   "' is gone before it could be taken.");
                    return false;
                }

                int taken = source.Take(line.ItemName, line.Amount, destination);

                if (taken == ChestTake.Deferred)
                {
                    Log.LogWarning("Pull aborted: " + line.Amount + "x '" + line.ItemName +
                                   "' was routed to another player and cannot be secured synchronously.");
                    return false;
                }

                if (taken < line.Amount)
                {
                    Log.LogWarning("Pull aborted: the source supplied " + taken + " of " + line.Amount +
                                   "x '" + line.ItemName + "'.");
                    return false;
                }
            }
            return true;
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
