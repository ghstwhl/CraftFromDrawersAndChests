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
    /// Removal is deliberately unfinished: chest items may only leave through
    /// <c>MultiUserChest.ContainerHandler.RemoveItemFromChest</c>, which Task 11 wires up behind
    /// <c>MultiUserChestBridge</c>. Until then <see cref="Take"/> throws.
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
        /// Not implemented yet. Removing from a chest must go through MultiUserChest's protocol
        /// (<c>ContainerHandler.RemoveItemFromChest</c>, wrapped by <c>MultiUserChestBridge</c>),
        /// which Task 11 adds; writing <c>Container.m_inventory</c> here would desync the chest
        /// against the player currently managing it. Nothing calls this yet.
        /// </summary>
        public int Take(string itemName, int amount, Inventory destination)
        {
            throw new NotImplementedException(
                "Taking items out of a vanilla chest is not implemented yet: it must be routed " +
                "through MultiUserChest (MultiUserChestBridge.RemoveFromChest), which Task 11 adds. " +
                "Container.m_inventory must never be written directly.");
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
