using System;
using System.Collections.Generic;
using HarmonyLib;

namespace CraftFromDrawersAndChests.Storage
{
    /// <summary>
    /// Keeps the raw set of vanilla containers the mod may pull from: a container joins the set
    /// when it awakes and leaves it when it is destroyed. <see cref="StorageRegistry.Collect"/>
    /// then filters this set down to the sources eligible for a pull.
    /// <para>
    /// Ported from CraftFromContainers 4.0.7's <c>Container_Awake_Patch</c>,
    /// <c>Container_OnDestroyed_Patch</c> and the <c>AddContainer</c> predicate that checked a
    /// container before adding it. One deliberate difference: CraftFromContainers re-ran the
    /// predicate from a coroutine a frame later, while this postfix evaluates it immediately — which
    /// is equivalent now that <c>Container.Awake</c> has run to completion. A container that
    /// early-returned from <c>Awake</c> (no ZDO) never created an inventory, so the predicate's
    /// <c>GetInventory() != null</c> test rejects it exactly as the delayed check did.
    /// </para>
    /// <para>
    /// The predicate is CraftFromContainers' verbatim: the container must expose an inventory and a
    /// live ZDO, and either its GameObject name must start with <c>piece_</c> or <c>Container</c>, or
    /// its ZDO must carry a non-zero <c>creator</c>. The name test admits built pieces and
    /// mod-named containers; the <c>creator</c> test admits placed containers whose name does not
    /// follow that convention while still excluding purely dynamic spawns.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Container))]
    internal static class ContainerTrackingPatches
    {
        /// <summary>The name prefixes CraftFromContainers' predicate accepts (a built piece, or a mod-named container).</summary>
        private const string BuiltPiecePrefix = "piece_";
        private const string ContainerNamePrefix = "Container";

        /// <summary>
        /// The Valheim ZDO key that records a container's creator. It is a game key, not one of this
        /// mod's, so it is deliberately not a <c>ConfigModel</c> constant; it is hashed with the same
        /// <c>GetStableHashCode</c> the ZDO uses, exactly as the baseline did.
        /// </summary>
        private static readonly int CreatorHash = "creator".GetStableHashCode();

        private static readonly List<Container> TrackedContainers = new List<Container>();

        /// <summary>
        /// The live set of tracked containers. Internal: only <see cref="StorageRegistry"/> reads it,
        /// and callers must not mutate it. A container whose Unity object has already been destroyed
        /// can still be listed until its <c>OnDestroyed</c> postfix runs, so the registry re-checks
        /// validity per entry rather than trusting the set.
        /// </summary>
        internal static IReadOnlyList<Container> Tracked
        {
            get { return TrackedContainers; }
        }

        /// <summary>Forgets every tracked container; see <see cref="StorageRegistry.Clear"/>.</summary>
        internal static void Clear()
        {
            TrackedContainers.Clear();
        }

        /// <summary>
        /// Tracks a container once <c>Container.Awake</c> has finished building it — Awake is what
        /// creates the inventory the predicate inspects.
        /// </summary>
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix(Container __instance)
        {
            if (ShouldTrack(__instance, VanillaChestSource.ViewOf(__instance)))
                AddUnique(__instance);
        }

        /// <summary>Drops a destroyed container from the tracked set.</summary>
        [HarmonyPatch("OnDestroyed")]
        [HarmonyPostfix]
        private static void OnDestroyedPostfix(Container __instance)
        {
            TrackedContainers.Remove(__instance);
        }

        /// <summary>
        /// CraftFromContainers' tracking predicate: an inventory, a live ZDO, and either a
        /// <c>piece_</c>/<c>Container</c> name or a non-zero <c>creator</c>. A null container, a
        /// missing ZNetView or a missing ZDO is rejected, never thrown on.
        /// </summary>
        /// <param name="container">The candidate container; may be null or already destroyed.</param>
        /// <param name="view">
        /// The container's live ZNetView, resolved by <see cref="VanillaChestSource.ViewOf"/> the same
        /// way <c>Container.Awake</c> resolves <c>m_nview</c>; may be null.
        /// </param>
        internal static bool ShouldTrack(Container container, ZNetView view)
        {
            if (container == null || container.gameObject == null)
                return false;

            if (container.GetInventory() == null)
                return false;

            if (view == null)
                return false;

            ZDO zdo = view.GetZDO();
            if (zdo == null)
                return false;

            string name = container.name;
            if (name != null &&
                (name.StartsWith(BuiltPiecePrefix, StringComparison.Ordinal) ||
                 name.StartsWith(ContainerNamePrefix, StringComparison.Ordinal)))
            {
                return true;
            }

            return zdo.GetLong(CreatorHash, 0L) != 0L;
        }

        /// <summary>
        /// Adds a container at most once. A single <c>Awake</c> fires once per instance, but the guard
        /// keeps the set free of duplicates if a mod ever re-runs it, which would otherwise make
        /// <see cref="StorageRegistry.Collect"/> report the same chest twice.
        /// </summary>
        private static void AddUnique(Container container)
        {
            if (!TrackedContainers.Contains(container))
                TrackedContainers.Add(container);
        }
    }
}
