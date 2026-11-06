using System.Collections.Generic;
using CraftFromDrawersAndChests.Logic;

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
        }

        private static void CollectChests(
            Vec3 origin, FeatureFlags flags, ChestTypeRules chestRules, List<IStorageSource> sources,
            bool includeSwitchedOff, List<BlockedStorage> blocked)
        {
            IReadOnlyList<Container> tracked = ContainerTrackingPatches.Tracked;
            for (int i = 0; i < tracked.Count; i++)
            {
                Container container = tracked[i];

                // Unity's null check is true for a destroyed object, which can still be listed until
                // its OnDestroyed postfix runs.
                if (container == null)
                    continue;

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
    }
}
