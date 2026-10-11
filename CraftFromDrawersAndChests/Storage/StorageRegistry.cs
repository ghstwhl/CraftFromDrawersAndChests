using System.Collections.Generic;
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
    /// filtered by distance, chest-type settings, access, a non-null inventory and the guard stone.
    /// Five of those live in <see cref="Eligibility"/> — distance, the chest-type rules, access, the
    /// inventory and the per-storage switch. The two container-only checks that
    /// <see cref="StorageCandidate"/> deliberately carries no fields for — the container sits inside
    /// a <c>Piece</c>, and the guard stone grants access at its position — are applied here through
    /// <see cref="VanillaChestSource"/>'s helpers, since Task 3's candidate shape is frozen.
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
        /// Every storage eligible to be a pull source for <paramref name="origin"/>, in the order
        /// chests then drawers. Never null: empty when nothing qualifies, when
        /// <paramref name="flags"/> is null, or when the world holds no chests or drawers at all.
        /// A tracked container whose object has since been destroyed is skipped rather than thrown on.
        /// </summary>
        /// <param name="origin">The pull origin (the player, or a station), in world coordinates.</param>
        /// <param name="flags">The resolved configuration snapshot; null yields no sources.</param>
        public static IReadOnlyList<IStorageSource> Collect(Vec3 origin, FeatureFlags flags)
        {
            var sources = new List<IStorageSource>();
            if (flags == null)
                return sources;

            ChestTypeRules chestRules = BuildChestRules(flags);
            CollectChests(origin, flags, chestRules, sources);
            CollectDrawers(origin, flags, chestRules, sources);
            return sources;
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
            Vec3 origin, FeatureFlags flags, ChestTypeRules chestRules, List<IStorageSource> sources)
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

                // The two container-only filters Eligibility cannot see (it has no candidate fields
                // for them): a loose container is not part of a built Piece, and a guard-stone
                // container is skipped where PrivateArea denies access.
                if (!source.IsValid || !source.IsInsidePiece || !source.PassesGuardStone)
                    continue;

                var candidate = new StorageCandidate(
                    StorageKind.Chest,
                    source.Position,
                    source.ObjectName,
                    source.ParentIsShip,
                    source.ParentIsWagon,
                    source.ToggledOff,
                    source.HasAccess,
                    source.HasInventory);

                if (Eligibility.IsEligible(
                        candidate, origin, flags.MaxRange, flags.EnableChests, flags.EnableDrawers, chestRules))
                {
                    sources.Add(source);
                }
            }
        }

        private static void CollectDrawers(
            Vec3 origin, FeatureFlags flags, ChestTypeRules chestRules, List<IStorageSource> sources)
        {
            IReadOnlyList<DrawerHandle> drawers = DrawerApi.AllInRange(origin, flags.MaxRange);
            for (int i = 0; i < drawers.Count; i++)
            {
                var source = new DrawerSource(drawers[i]);
                if (!source.IsValid)
                    continue;

                // A drawer is a ZNetView, not a Container: it has no chest type, access check or
                // Container inventory, and Eligibility ignores those fields for a drawer anyway.
                var candidate = new StorageCandidate(
                    StorageKind.Drawer,
                    source.Position,
                    source.ObjectName,
                    false, // parentIsShip
                    false, // parentIsWagon
                    source.ToggledOff,
                    false, // hasAccess
                    false); // hasInventory

                if (Eligibility.IsEligible(
                        candidate, origin, flags.MaxRange, flags.EnableChests, flags.EnableDrawers, chestRules))
                {
                    sources.Add(source);
                }
            }
        }

        /// <summary>
        /// Builds the chest-type rules from the six ignore settings, so the pure predicate never has
        /// to know about <see cref="FeatureFlags"/>.
        /// </summary>
        private static ChestTypeRules BuildChestRules(FeatureFlags flags)
        {
            return new ChestTypeRules
            {
                IgnoreShipContainers = flags.IgnoreShipContainers,
                IgnoreWagonContainers = flags.IgnoreWagonContainers,
                IgnoreWoodChests = flags.IgnoreWoodChests,
                IgnorePrivateChests = flags.IgnorePrivateChests,
                IgnoreBlackMetalChests = flags.IgnoreBlackMetalChests,
                IgnoreReinforcedChests = flags.IgnoreReinforcedChests,
            };
        }
    }
}
