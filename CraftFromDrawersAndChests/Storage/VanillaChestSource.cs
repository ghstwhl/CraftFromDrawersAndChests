using System;
using System.Collections.Generic;
using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using HarmonyLib;
using UnityEngine;

namespace CraftFromDrawersAndChests.Storage
{
    /// <summary>
    /// Adapts a vanilla Valheim <see cref="Container"/> — a wood chest, a private chest, a black
    /// metal or reinforced chest, a ship's hold or a cart's box — to <see cref="IStorageSource"/>.
    /// Values are read live from the container's inventory and its ZDO; nothing here mutates the
    /// container.
    /// <para>
    /// Removal never writes the container's inventory: chest items leave only through
    /// <c>MultiUserChest.ContainerHandler.RemoveItemFromChest</c>, wrapped by
    /// <see cref="MultiUserChestBridge.RemoveFromChest"/>. This class reads the inventory to find
    /// the stack to take and hands it to the bridge.
    /// </para>
    /// <para>
    /// Only public Valheim members are used (<c>GetInventory()</c>, <c>Inventory.GetAllItems()</c>,
    /// <c>m_checkGuardStone</c>, <c>m_wagon</c>, <c>m_rootObjectOverride</c>); the one private
    /// member needed, <c>Container.CheckAccess(long)</c>, is reached by reflection exactly as
    /// CraftFromContainers did. Private fields are never read directly, so the class does not
    /// depend on a publicized game assembly at runtime.
    /// </para>
    /// </summary>
    public sealed class VanillaChestSource : IStorageSource
    {
        /// <summary>The shared world off-switch key, hashed the same way the ZDO stores it.</summary>
        private static readonly int NoPullHash = ConfigModel.ZdoKey.GetStableHashCode();

        /// <summary><c>Container.CheckAccess(long)</c>, the private access check, cached once.</summary>
        private static readonly System.Reflection.MethodInfo CheckAccessMethod =
            AccessTools.Method(typeof(Container), "CheckAccess");

        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        private static bool accessWarningLogged;

        private readonly Container _container;

        public VanillaChestSource(Container container)
        {
            _container = container;
        }

        public StorageKind Kind
        {
            get { return StorageKind.Chest; }
        }

        public Vec3 Position
        {
            get
            {
                Vector3 p = _container.transform.position;
                return new Vec3(p.x, p.y, p.z);
            }
        }

        public string ObjectName
        {
            get
            {
                GameObject go = _container != null ? _container.gameObject : null;
                return go != null ? go.name : null;
            }
        }

        /// <summary>
        /// The per-storage off switch, read from the container's own ZDO under
        /// <see cref="ConfigModel.ZdoKey"/> — the same key and hashing <c>DrawerSource</c> uses.
        /// A container with no reachable ZDO is not switched off.
        /// </summary>
        public bool ToggledOff
        {
            get
            {
                ZDO zdo = Zdo;
                return zdo != null && zdo.GetBool(NoPullHash, false);
            }
        }

        public bool IsValid
        {
            get { return _container != null && _container.gameObject != null; }
        }

        /// <summary>
        /// Whether <see cref="Take"/> from this chest can be served synchronously, i.e. whether its
        /// removal would be applied locally and its count known on return. False means another player
        /// owns the chest: a removal would be routed, and the request has already left by the time
        /// <see cref="Take"/> returns, so <see cref="Take"/> would report
        /// <see cref="ChestTake.Deferred"/>. A caller that needs a definite outcome (an all-or-nothing
        /// pull) must check this and skip the source rather than call <see cref="Take"/>. True for an
        /// unowned chest (it is claimed and becomes ours) and for a chest we already own. Mirrors the
        /// read-only, game-facing shape of <see cref="IStorageSource"/>'s members.
        /// </summary>
        public bool CanTakeSynchronously
        {
            get { return MultiUserChestBridge.CanRemoveSynchronously(_container); }
        }

        /// <summary>
        /// How many of the given prefab the container holds. Storage is keyed by <b>prefab name</b>
        /// (<c>"Wood"</c>), not by <c>m_shared.m_name</c> (<c>"$item_wood"</c>): every other part of
        /// this mod — the drawer's ZDO, the planner's <c>StorageStock</c> and <c>PullRequest</c> —
        /// carries a prefab name. Each stack is matched on <c>item.m_dropPrefab.name</c>, and
        /// <c>Inventory.CountItems</c> is deliberately not used because it matches the localized
        /// shared name instead (Ruling 1).
        /// </summary>
        /// <param name="itemName">The item's prefab name; see <see cref="Count"/>.</param>
        public int Count(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || !IsValid)
                return 0;

            Inventory inventory = _container.GetInventory();
            if (inventory == null)
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

                if (string.Equals(item.m_dropPrefab.name, itemName, StringComparison.Ordinal))
                    total += item.m_stack;
            }
            return total;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> of the given prefab from the chest and delivers
        /// them into <paramref name="destination"/>, returning how many were actually removed.
        /// <para>
        /// The stack is located the same way <see cref="Count"/> counts it — matched on
        /// <c>item.m_dropPrefab.name</c>, since storage is keyed by prefab name (Ruling 1). Removal
        /// itself goes only through <see cref="MultiUserChestBridge.RemoveFromChest"/>: the
        /// container's inventory is read here to find the stack but never written, because writing it
        /// would desync the chest against the player MultiUserChest has managing it (Ruling 5).
        /// </para>
        /// <para>
        /// <b>Fulfils across stacks.</b> <see cref="Count"/> sums <i>every</i> matching stack, so
        /// <see cref="Take"/> must be able to hand over exactly that much, or the availability gate
        /// would promise more than a single removal can deliver — the disagreement that let a chest
        /// holding 2 × 30 pass a 50-item gate, deliver 30, and (with the consume skipped) hand out a
        /// free crafted item. One MultiUserChest removal is a one-slot operation, so this takes the
        /// largest matching stack, delivers it, and repeats until the request is met or the chest runs
        /// out. Each iteration either delivers a positive number or stops, so the loop always
        /// terminates.
        /// </para>
        /// <para>
        /// The return value is <c>&gt;= 0</c> — the number actually removed, which may be fewer than
        /// requested, or zero — or <see cref="ChestTake.Deferred"/> (<c>-1</c>) when a removal was
        /// <b>routed</b> to another player and the count is not knowable synchronously. The sentinel is
        /// only returned when <b>nothing at all</b> was delivered: once some items are already in hand,
        /// the definite count is returned instead, and the routed remainder arrives asynchronously
        /// (the caller treats the line as satisfied either way). <c>-1</c> is not a failure and must
        /// not be retried, because the items do still leave the chest and will arrive later, so a
        /// retry would duplicate them. A caller that needs a definite count must check
        /// <see cref="CanTakeSynchronously"/> first.
        /// </para>
        /// </summary>
        public int Take(string itemName, int amount, Inventory destination)
        {
            if (string.IsNullOrEmpty(itemName) || amount <= 0 || destination == null || !IsValid)
                return 0;

            Inventory inventory = _container.GetInventory();
            if (inventory == null)
                return 0;

            int delivered = 0;
            while (delivered < amount)
            {
                // Re-locate the largest matching stack each pass: the previous removal may have
                // emptied the stack entirely (a full stack is gone) or only partly drained it.
                ItemDrop.ItemData item = FindLargestStack(inventory, itemName);
                if (item == null)
                    break; // no matching stack left: a definite shortfall, not a deferred result

                int taken = MultiUserChestBridge.RemoveFromChest(_container, item, destination, amount - delivered);

                if (taken == ChestTake.Deferred)
                {
                    // Routed to another player. Report the sentinel only when nothing was delivered;
                    // otherwise the items already in hand make the definite count the honest answer.
                    return delivered > 0 ? delivered : ChestTake.Deferred;
                }

                if (taken <= 0)
                    break; // no progress (the destination has no room, or the chest holds none)

                delivered += taken;
            }

            return delivered;
        }

        /// <summary>
        /// The largest stack of the given prefab in the container, matched exactly as
        /// <see cref="Count"/> matches: on <c>item.m_dropPrefab.name</c>, never the localized
        /// <c>m_shared.m_name</c>. <see cref="Take"/> locates a stack through this each pass and
        /// removes from it, so choosing the largest keeps the common case — a single chest stack big
        /// enough for the whole request — to one bridge call; a request larger than one stack still
        /// succeeds, only across several calls.
        /// </summary>
        private static ItemDrop.ItemData FindLargestStack(Inventory inventory, string itemName)
        {
            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            if (items == null)
                return null;

            ItemDrop.ItemData best = null;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData item = items[i];
                if (item == null || item.m_dropPrefab == null)
                    continue;

                if (!string.Equals(item.m_dropPrefab.name, itemName, StringComparison.Ordinal))
                    continue;

                if (best == null || item.m_stack > best.m_stack)
                    best = item;
            }
            return best;
        }

        /// <summary>True when the container exposes an inventory; a container without one is skipped.</summary>
        internal bool HasInventory
        {
            get { return _container != null && _container.GetInventory() != null; }
        }

        /// <summary>
        /// <c>Container.CheckAccess</c> for the local player. False with no local player, or when the
        /// private method could not be reflected — failing closed is the safe direction: it excludes a
        /// chest rather than pulling from storage the player may not be allowed to use.
        /// </summary>
        internal bool HasAccess
        {
            get
            {
                if (_container == null)
                    return false;

                Player player = Player.m_localPlayer;
                if (player == null)
                    return false;

                if (CheckAccessMethod == null)
                {
                    if (!accessWarningLogged)
                    {
                        accessWarningLogged = true;
                        Log.LogWarning("Container.CheckAccess could not be reflected; no chest is treated as accessible.");
                    }
                    return false;
                }

                object result = CheckAccessMethod.Invoke(_container, new object[] { player.GetPlayerID() });
                return result is bool && (bool)result;
            }
        }

        /// <summary>
        /// True when the container's parent carries a <c>Ship</c>, the flag behind the
        /// "ignore ship containers" rule. Matches CraftFromContainers' parent lookup.
        /// </summary>
        internal bool ParentIsShip
        {
            get
            {
                if (_container == null)
                    return false;

                Transform parent = _container.transform.parent;
                return parent != null && parent.GetComponent<Ship>() != null;
            }
        }

        /// <summary>The flag behind the "ignore wagon containers" rule; CraftFromContainers' <c>m_wagon</c>.</summary>
        internal bool ParentIsWagon
        {
            get { return _container != null && _container.m_wagon != null; }
        }

        /// <summary>True when the container is part of a built <c>Piece</c>; loose containers are skipped.</summary>
        internal bool IsInsidePiece
        {
            get { return _container != null && _container.GetComponentInParent<Piece>() != null; }
        }

        /// <summary>
        /// The guard-stone check CraftFromContainers applied: a container with guard-stone privacy is
        /// only eligible where <c>PrivateArea</c> grants access.
        /// </summary>
        internal bool PassesGuardStone
        {
            get
            {
                if (_container == null)
                    return false;

                return !_container.m_checkGuardStone
                    || PrivateArea.CheckAccess(_container.transform.position, 0f, false, false);
            }
        }

        /// <summary>
        /// The container's live <see cref="ZNetView"/>, resolved the way <c>Container.Awake</c>
        /// resolves its own: the root-object override when one is set, otherwise the ZNetView on the
        /// container's GameObject. Shared with <see cref="ContainerTrackingPatches"/> so both read
        /// the same ZDO.
        /// </summary>
        internal static ZNetView ViewOf(Container container)
        {
            if (container == null)
                return null;

            if (container.m_rootObjectOverride != null)
                return container.m_rootObjectOverride.GetComponent<ZNetView>();

            return container.GetComponent<ZNetView>();
        }

        private ZDO Zdo
        {
            get
            {
                ZNetView view = ViewOf(_container);
                return view != null ? view.GetZDO() : null;
            }
        }
    }
}
