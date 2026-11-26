using System.Collections.Generic;
using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using UnityEngine;

namespace CraftFromDrawersAndChests.Storage
{
    /// <summary>
    /// Discovers the storage a pull may draw from: every tracked vanilla container and every Item
    /// Drawers drawer within range, filtered by <see cref="Eligibility.IsEligible"/>. Game-facing —
    /// the pure <c>Logic/</c> layer never sees this type.
    /// <para>
    /// The flow is CraftFromContainers 4.0.7's <c>GetNearbyContainers</c>: the raw containers are
    /// filtered by distance, chest-type settings, access, a non-null inventory, membership of a
    /// built <c>Piece</c> and the guard stone. Every one of those decisions lives in the pure
    /// <see cref="Eligibility.IsEligible"/> predicate; this class only observes each container's
    /// game state into a <see cref="StorageCandidate"/> and passes it <c>in</c>. The two
    /// container-only clauses are fed from <see cref="VanillaChestSource.IsInsidePiece"/> and
    /// <see cref="VanillaChestSource.PassesGuardStone"/>.
    /// </para>
    /// <para>
    /// CraftFromContainers also skipped a container another player had open. That exclusion is
    /// deliberately absent: an in-use chest stays a valid source, and <see cref="StorageCandidate"/>
    /// has no "in use" field at all.
    /// </para>
    /// </summary>
    public static class StorageRegistry
    {
        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        /// <summary>
        /// When each chest was last observed, keyed by its ZDO id, and when an observation pass last ran.
        /// The cadence is <see cref="StorageRefreshRules"/>'s; this is only the bookkeeping. Both are used
        /// exclusively by <c>Debug</c> logging — nothing here asks the network for anything.
        /// </summary>
        private static readonly Dictionary<ZDOID, float> lastObservedAt = new Dictionary<ZDOID, float>();
        private static float lastPassAt;

        /// <summary>The instance ids of the chests whose change hook is installed; Debug only.</summary>
        private static readonly HashSet<int> hookedChests = new HashSet<int>();

        /// <summary>
        /// A storage that holds what a requirement needs but is <b>locked</b> against this player, with
        /// the reason. <see cref="StorageRegistry.CollectBlocked" /> produces these for the crafting
        /// readout's hint; a pull must never be planned or executed against one.
        /// </summary>
        public readonly struct BlockedStorage
        {
            /// <summary>The blocked storage, read-only by convention: only <see cref="IStorageSource.Count" /> is used.</summary>
            public readonly IStorageSource Source;

            /// <summary>Why it is locked.</summary>
            public readonly StorageBlockReason Reason;

            public BlockedStorage(IStorageSource source, StorageBlockReason reason)
            {
                Source = source;
                Reason = reason;
            }
        }

        /// <summary>
        /// Every storage eligible to be a pull source for <paramref name="origin"/>, in the order
        /// chests then drawers. Never null: empty when nothing qualifies, when
        /// <paramref name="flags"/> is null, or when the world holds no chests or drawers at all.
        /// A tracked container whose object has since been destroyed is skipped rather than thrown on.
        /// </summary>
        /// <param name="origin">The pull origin (the player, or a station), in world coordinates.</param>
        /// <param name="flags">The resolved configuration snapshot; null yields no sources.</param>
        public static IReadOnlyList<IStorageSource> Collect(Vec3 origin, FeatureFlags flags)
        {
            return Collect(origin, flags, false);
        }

        /// <summary>
        /// Every in-range storage that <b>would</b> be eligible if its per-storage off switch were
        /// not set — the pullable list plus the switched-off storages. Diagnostics only: a caller
        /// must never plan or execute a pull against this list.
        /// <para>
        /// The debug readout is the one caller (<c>Patches.PullKeyPatches.LogReadout</c>). It has to
        /// be able to name a storage that holds the requirement while being switched off, because
        /// such a storage is otherwise indistinguishable from one that does not hold it at all: the
        /// switch excludes it from <see cref="Collect"/> before the readout ever sees it.
        /// </para>
        /// </summary>
        /// <param name="origin">The pull origin (the player, or a station), in world coordinates.</param>
        /// <param name="flags">The resolved configuration snapshot; null yields no sources.</param>
        public static IReadOnlyList<IStorageSource> CollectIncludingSwitchedOff(Vec3 origin, FeatureFlags flags)
        {
            return Collect(origin, flags, true);
        }

        private static IReadOnlyList<IStorageSource> Collect(Vec3 origin, FeatureFlags flags, bool includeSwitchedOff)
        {
            var sources = new List<IStorageSource>();
            if (flags == null)
                return sources;

            ChestTypeRules chestRules = ChestTypeRules.FromFlags(flags);
            CollectChests(origin, flags, chestRules, sources, includeSwitchedOff, null);
            CollectDrawers(origin, flags, chestRules, sources, includeSwitchedOff, null);
            return sources;
        }

        /// <summary>
        /// Every in-range storage that holds nothing back for a reason other than a lock — i.e. the
        /// storages the crafting readout should name when a requirement is uncovered: the player
        /// switched them off, or they refuse this player's access.
        /// <para>
        /// <b>Diagnostics only.</b> A caller must never plan or execute a pull against this list; the
        /// whole point is that these storages are <i>not</i> usable. It is separate from
        /// <see cref="CollectIncludingSwitchedOff" /> on purpose: that one answers "which storages are
        /// in range at all" for the debug log, while this one answers "which locked storages claim to
        /// hold my item" for the player-facing hint, and only this one carries the reason.
        /// </para>
        /// </summary>
        /// <param name="origin">The pull origin (the player, or a station), in world coordinates.</param>
        /// <param name="flags">The resolved configuration snapshot; null yields no entries.</param>
        public static IReadOnlyList<BlockedStorage> CollectBlocked(Vec3 origin, FeatureFlags flags)
        {
            var blocked = new List<BlockedStorage>();
            if (flags == null)
                return blocked;

            ChestTypeRules chestRules = ChestTypeRules.FromFlags(flags);
            var unused = new List<IStorageSource>();
            CollectChests(origin, flags, chestRules, unused, false, blocked);
            CollectDrawers(origin, flags, chestRules, unused, false, blocked);
            return blocked;
        }

        /// <summary>
        /// The one place the two collection modes differ: the pull path asks
        /// <see cref="Eligibility.IsEligible"/> (which honours the off switch), the diagnostic path
        /// asks <see cref="Eligibility.IsEligibleButForSwitch"/> (which does not). Every other
        /// exclusion is applied identically, so the diagnostic list is the pullable list plus the
        /// switched-off storages and nothing else.
        /// </summary>
        private static bool IsSource(
            in StorageCandidate candidate,
            Vec3 origin,
            FeatureFlags flags,
            ChestTypeRules chestRules,
            bool includeSwitchedOff)
        {
            return includeSwitchedOff
                ? Eligibility.IsEligibleButForSwitch(
                    candidate, origin, flags.MaxRange, flags.EnableChests, flags.EnableDrawers, chestRules)
                : Eligibility.IsEligible(
                    candidate, origin, flags.MaxRange, flags.EnableChests, flags.EnableDrawers, chestRules);
        }

        /// <summary>
        /// Forgets every tracked container, so a later <see cref="Collect"/> starts from an empty set.
        /// The patch re-adds containers as they awake, so this is for tearing a world down (a reload
        /// or a return to the main menu) rather than for routine use.
        /// </summary>
        public static void Clear()
        {
            ContainerTrackingPatches.Clear();
            lastObservedAt.Clear();
            hookedChests.Clear();
            lastPassAt = 0f;
        }

        private static void CollectChests(
            Vec3 origin, FeatureFlags flags, ChestTypeRules chestRules, List<IStorageSource> sources,
            bool includeSwitchedOff, List<BlockedStorage> blocked)
        {
            IReadOnlyList<Container> tracked = ContainerTrackingPatches.Tracked;

            // One observation pass at most every couple of seconds, however many callers collect this
            // frame, and only with Debug on. It observes; it never changes what this mod does.
            float now = Time.time;
            bool observe = flags != null && flags.IsDebug &&
                           StorageRefreshRules.IsDue(now, lastPassAt, StorageRefreshRules.PassIntervalSeconds);
            if (observe)
                lastPassAt = now;

            for (int i = 0; i < tracked.Count; i++)
            {
                Container container = tracked[i];

                // Unity's null check is true for a destroyed object, which can still be listed until
                // its OnDestroyed postfix runs.
                if (container == null)
                    continue;

                if (observe)
                {
                    HookInventoryChanges(container);
                    ObserveChest(container, now);
                }

                var source = new VanillaChestSource(container);
                if (!source.IsValid)
                    continue;

                var candidate = new StorageCandidate(
                    StorageKind.Chest,
                    source.Position,
                    source.ObjectName,
                    source.ParentIsShip,
                    source.ParentIsWagon,
                    source.ToggledOff,
                    source.HasAccess,
                    source.HasInventory,
                    source.IsInsidePiece,
                    source.PassesGuardStone);

                if (IsSource(candidate, origin, flags, chestRules, includeSwitchedOff))
                {
                    sources.Add(source);
                }
                else if (blocked != null &&
                         Eligibility.TryBlockReason(
                             candidate, origin, flags.MaxRange, flags.EnableChests, flags.EnableDrawers,
                             chestRules, out StorageBlockReason reason))
                {
                    blocked.Add(new BlockedStorage(source, reason));
                }
            }
        }

        private static void CollectDrawers(
            Vec3 origin, FeatureFlags flags, ChestTypeRules chestRules, List<IStorageSource> sources,
            bool includeSwitchedOff, List<BlockedStorage> blocked)
        {
            IReadOnlyList<DrawerHandle> drawers = DrawerApi.AllInRange(origin, flags.MaxRange);
            for (int i = 0; i < drawers.Count; i++)
            {
                var source = new DrawerSource(drawers[i]);
                if (!source.IsValid)
                    continue;

                // A drawer is a ZNetView, not a Container: it has no chest type, Piece parent,
                // guard stone, access check or Container inventory, and Eligibility ignores those
                // fields for a drawer anyway.
                var candidate = new StorageCandidate(
                    StorageKind.Drawer,
                    source.Position,
                    source.ObjectName,
                    false, // parentIsShip
                    false, // parentIsWagon
                    source.ToggledOff,
                    false, // hasAccess
                    false, // hasInventory
                    false, // isInsidePiece: a drawer is not a Piece child
                    false); // passesGuardStone: a drawer has no guard stone

                if (IsSource(candidate, origin, flags, chestRules, includeSwitchedOff))
                {
                    sources.Add(source);
                }
                else if (blocked != null &&
                         Eligibility.TryBlockReason(
                             candidate, origin, flags.MaxRange, flags.EnableChests, flags.EnableDrawers,
                             chestRules, out StorageBlockReason reason))
                {
                    blocked.Add(new BlockedStorage(source, reason));
                }
            }
        }

        /// <summary>
        /// Debug-only observation of one chest: a throttled snapshot of what this client's copy holds
        /// and what the chest's ZDO claims. <b>Observes only</b> — no request, no ownership change, no
        /// inventory write — so the build behaves exactly as it does with <c>Debug</c> off.
        /// <para>
        /// A chest's contents are a local <c>Inventory</c> loaded from its ZDO, and only the ZDO's owner
        /// writes the chest back, so "player B counts 0 for a chest player A filled" can mean three
        /// different things: B never received the data, B's copy was replaced by a stale one, or B's copy
        /// is right and the ZDO is wrong. The snapshot separates them — local item count, local ZDO
        /// revision, the size of the ZDO's serialized <c>items</c> var, and the owner id — so the cause is
        /// read from a log instead of inferred.
        /// </para>
        /// </summary>
        private static void ObserveChest(Container container, float now)
        {
            ZNetView view = VanillaChestSource.ViewOf(container);
            if (view == null || !view.IsValid())
                return;

            ZDO zdo = view.GetZDO();
            if (zdo == null)
                return;

            float last = lastObservedAt.TryGetValue(zdo.m_uid, out float previous) ? previous : 0f;
            if (!StorageRefreshRules.IsDue(now, last, StorageRefreshRules.ChestIntervalSeconds))
                return;

            lastObservedAt[zdo.m_uid] = now;

            long owner = zdo.GetOwner();
            Log.LogInfo(
                "Chest '" + container.name + "': items " + ItemCount(container) +
                ", revision " + zdo.DataRevision + ", zdoItems " + ItemsByteCount(zdo) + " bytes, owner " +
                owner + ", weAreOwner " + (owner == ZNet.GetUID()) + ".");

            PruneObservationBookkeeping(now);
        }

        /// <summary>
        /// Installs, once per chest, a hook that logs every change to the chest's <b>local</b> contents
        /// together with the ZDO state at that moment. This exists because "the count dropped" cannot be
        /// attributed from the outside: a reload can come from the game's one-second timer, from a ZDO
        /// pushed by another peer, or from this client's own interaction. Logging the count, revision,
        /// <c>s_items</c> size and owner at the moment it changes names the culprit in a single test run.
        /// Debug only; with Debug off no handler is installed.
        /// </summary>
        private static void HookInventoryChanges(Container container)
        {
            if (!hookedChests.Add(container.GetInstanceID()))
                return;

            Inventory inventory = container.GetInventory();
            if (inventory == null)
                return;

            string name = container.name;
            var state = new ChestChangeState { LastCount = inventory.NrOfItems() };

            inventory.m_onChanged += () =>
            {
                if (container == null)
                    return;

                int count = ItemCount(container);
                if (count == state.LastCount)
                    return;

                ZNetView view = VanillaChestSource.ViewOf(container);
                ZDO zdo = view != null ? view.GetZDO() : null;
                long owner = zdo != null ? zdo.GetOwner() : 0L;

                Log.LogInfo(
                    "Chest '" + name + "' contents changed: items " + state.LastCount + " -> " + count +
                    ", revision " + (zdo != null ? zdo.DataRevision : 0u) +
                    ", zdoItems " + ItemsByteCount(zdo) + " bytes, owner " + owner +
                    ", weAreOwner " + (owner == ZNet.GetUID()) + ".");

                state.LastCount = count;
            };
        }

        /// <summary>The mutable box the change hook keeps its last observed item count in.</summary>
        private sealed class ChestChangeState
        {
            public int LastCount;
        }

        /// <summary>The container's local item count, or -1 when it has no inventory.</summary>
        private static int ItemCount(Container container)
        {
            Inventory inventory = container != null ? container.GetInventory() : null;
            return inventory != null ? inventory.NrOfItems() : -1;
        }

        /// <summary>
        /// How many bytes the chest's serialized inventory occupies in the <b>local</b> ZDO, or -1 when
        /// there is no ZDO. Zero means the ZDO claims the chest is empty — which is the interesting case
        /// when the client's inventory says otherwise.
        /// </summary>
        private static int ItemsByteCount(ZDO zdo)
        {
            if (zdo == null)
                return -1;

            byte[] items = zdo.GetByteArray(ZDOVars.s_items, (byte[])null);
            return items != null ? items.Length : 0;
        }

        /// <summary>
        /// Drops throttle entries old enough that they can no longer be due, so the bookkeeping cannot
        /// grow with every chest a long session has ever seen.
        /// </summary>
        private static void PruneObservationBookkeeping(float now)
        {
            if (lastObservedAt.Count < 256)
                return;

            var stale = new List<ZDOID>();
            foreach (KeyValuePair<ZDOID, float> entry in lastObservedAt)
            {
                if (now - entry.Value >= StorageRefreshRules.ChestIntervalSeconds)
                    stale.Add(entry.Key);
            }

            for (int i = 0; i < stale.Count; i++)
                lastObservedAt.Remove(stale[i]);
        }
    }
}
