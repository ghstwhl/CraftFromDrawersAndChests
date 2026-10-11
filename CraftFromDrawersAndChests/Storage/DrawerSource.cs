using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using UnityEngine;

namespace CraftFromDrawersAndChests.Storage
{
    /// <summary>
    /// Adapts a KG Item Drawers drawer (a <c>ZNetView</c>, not a <c>Container</c>) to
    /// <see cref="IStorageSource"/>. Values are read live from the drawer's ZDO; the only mutations
    /// are the supported removal path, <c>ClaimOwnership()</c> followed by the <c>ForceRemove</c>
    /// RPC, and delivering the removed items into the <c>destination</c> inventory.
    /// </summary>
    public sealed class DrawerSource : IStorageSource
    {
        /// <summary>The shared world off-switch key, hashed the same way the ZDO stores it.</summary>
        private static readonly int NoPullHash = ConfigModel.ZdoKey.GetStableHashCode();

        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        private readonly DrawerHandle _handle;

        public DrawerSource(DrawerHandle handle)
        {
            _handle = handle;
        }

        public StorageKind Kind
        {
            get { return StorageKind.Drawer; }
        }

        public Vec3 Position
        {
            get
            {
                Vector3 p = _handle.View.transform.position;
                return new Vec3(p.x, p.y, p.z);
            }
        }

        public string ObjectName
        {
            get
            {
                GameObject go = _handle.View != null ? _handle.View.gameObject : null;
                return go != null ? go.name : null;
            }
        }

        public bool ToggledOff
        {
            get
            {
                ZDO zdo = _handle.View != null ? _handle.View.GetZDO() : null;
                return zdo != null && zdo.GetBool(NoPullHash, false);
            }
        }

        public bool IsValid
        {
            get { return _handle.View != null && _handle.View.IsValid(); }
        }

        /// <summary>
        /// How many of the given prefab the drawer holds, read from its ZDO <c>"Prefab"</c> and
        /// <c>"Amount"</c>. See <see cref="IStorageSource.Count"/>: <paramref name="itemName"/> is a
        /// prefab name. A drawer reporting a prefab with a zero amount contributes nothing and never
        /// throws (Review Focus 4).
        /// </summary>
        public int Count(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || !IsValid)
                return 0;

            ZDO zdo = _handle.View.GetZDO();
            if (zdo == null)
                return 0;

            return DrawerStock.CountFor(
                zdo.GetString(DrawerApi.PrefabKey, string.Empty),
                zdo.GetInt(DrawerApi.AmountKey, 0),
                itemName);
        }

        /// <summary>
        /// Takes up to <paramref name="amount"/> of the given prefab out of the drawer and delivers
        /// them into <paramref name="destination"/>, returning how many were actually delivered.
        /// <para>
        /// Removal uses only the supported path: <c>ClaimOwnership()</c> then the <c>ForceRemove</c>
        /// RPC. Because <c>ForceRemove</c> only removes, the items are then materialised from
        /// <paramref name="itemName"/>'s prefab and added to <paramref name="destination"/> — the
        /// same delivery the chest path performs through
        /// <c>MultiUserChestBridge.RemoveFromChest(chest, item, destination, ...)</c>.
        /// </para>
        /// <para>
        /// Delivery is chunked one stack at a time. A single
        /// <c>Inventory.AddItem(GameObject, int)</c> places <b>at most one stack</b> (it sets
        /// <c>m_stack = Mathf.Min(amount, m_maxStackSize)</c> and adds a single <c>ItemData</c>), so
        /// handing it the whole amount would silently destroy everything beyond the first stack.
        /// The chunk sizes come from the pure <see cref="DrawerTake.NextChunk"/>, whose loop this
        /// method drives.
        /// </para>
        /// <para>
        /// Each chunk is capacity-checked (<c>CanAddItem</c>) <b>before</b> that chunk is removed,
        /// and the amount removed is exactly the amount then added, so the drawer never gives up
        /// more than the player received: <c>removed == delivered</c> at every step and at the end.
        /// With a null destination, an unresolvable prefab or no room for even the first chunk this
        /// removes nothing and returns 0 rather than destroying the items (the design's top
        /// anti-goal).
        /// </para>
        /// <para>
        /// Never <c>WithdrawItem_Request</c> — it spawns a world drop at the player and clears an
        /// empty drawer's fields — and never a direct write to the drawer's ZDO <c>"Amount"</c>.
        /// </para>
        /// </summary>
        /// <param name="itemName">The item's prefab name; see <see cref="Count"/>.</param>
        /// <param name="amount">The maximum number to take.</param>
        /// <param name="destination">
        /// Where the taken items are delivered. It must be non-null for anything to be taken: with a
        /// null destination the items would have nowhere to go, so this removes nothing and returns
        /// 0. The planner/pull path is responsible for providing a destination with room for the
        /// full requirement.
        /// </param>
        public int Take(string itemName, int amount, Inventory destination)
        {
            int want = DrawerTake.AmountToRemove(amount, Count(itemName), destination != null);
            if (want <= 0)
                return 0;

            // Resolve and capacity-check before removing anything: if the items cannot be delivered,
            // removing them would destroy them.
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(itemName) : null;
            ItemDrop itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            ItemDrop.ItemData itemData = itemDrop != null ? itemDrop.m_itemData : null;
            int maxStack = itemData != null && itemData.m_shared != null
                ? itemData.m_shared.m_maxStackSize
                : 0;

            if (prefab == null || maxStack <= 0)
            {
                Log.LogWarning("Drawer '" + ObjectName + "' reports prefab '" + itemName +
                               "' that has no usable ItemDrop; taking nothing.");
                return 0;
            }

            // Deliver one stack at a time. AddItem(GameObject, int) places at most m_maxStackSize
            // items, so each chunk is at most one stack; a single call for the whole amount would
            // lose everything beyond the first stack. Each chunk's removal is gated on its own
            // capacity check, so removed == delivered at every step.
            int delivered = 0;
            int chunk;
            while ((chunk = DrawerTake.NextChunk(want, delivered, maxStack)) > 0)
            {
                if (!destination.CanAddItem(prefab, chunk))
                {
                    Log.LogWarning("No room in the destination inventory for the next " + chunk +
                                   "x '" + itemName + "'; delivered " + delivered + " of " + want + ".");
                    break;
                }

                _handle.View.ClaimOwnership();
                _handle.View.InvokeRPC(DrawerApi.ForceRemoveRpcName, chunk);

                // CanAddItem(prefab, chunk) is true and chunk <= maxStack, so this places exactly
                // chunk; the return is still checked so an API surprise is never swallowed.
                if (!destination.AddItem(prefab, chunk))
                {
                    Log.LogError("A drawer gave up " + chunk + "x '" + itemName +
                                 "' but the destination refused them.");
                    break;
                }

                delivered += chunk;
            }

            return delivered;
        }
    }
}
