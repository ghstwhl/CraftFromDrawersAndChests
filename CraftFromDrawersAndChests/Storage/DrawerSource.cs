using System.Collections.Generic;
using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using UnityEngine;

namespace CraftFromDrawersAndChests.Storage
{
    /// <summary>
    /// Adapts a KG Item Drawers drawer (a <c>ZNetView</c>, not a <c>Container</c>) to
    /// <see cref="IStorageSource"/>. Values are read live from the drawer's ZDO. The only mutations
    /// are the drawer's own RPCs — <c>ClaimOwnership()</c> followed by <c>ForceRemove</c> to take
    /// items out, <c>AddItem_Request</c> to put a chunk back when its delivery fails, and adding the
    /// removed items to the <c>destination</c> inventory.
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
        /// The drawer's raw ZDO values as they are actually stored — the prefab string it holds its
        /// item under, its amount, and its per-storage off switch — for the debug log only
        /// (<c>Patches.PullKeyPatches</c>). A drawer that stores an item under a <i>different</i>
        /// prefab string than a requirement names is indistinguishable from one that is out of range
        /// or switched off, and neither half of that comparison is visible in the game's own UI; this
        /// is the one place the two can be seen together. Returns false when the drawer is gone.
        /// </summary>
        public bool TryReadRaw(out string prefab, out int amount, out bool toggledOff)
        {
            prefab = null;
            amount = 0;
            toggledOff = false;

            if (!IsValid)
                return false;

            ZDO zdo = _handle.View.GetZDO();
            if (zdo == null)
                return false;

            prefab = zdo.GetString(DrawerApi.PrefabKey, string.Empty);
            amount = zdo.GetInt(DrawerApi.AmountKey, 0);
            toggledOff = zdo.GetBool(NoPullHash, false);
            return true;
        }

        /// <summary>
        /// Takes up to <paramref name="amount"/> of the given prefab out of the drawer and delivers
        /// them into <paramref name="destination"/>, returning how many were actually delivered.
        /// <para>
        /// Removal uses only the supported path: <c>ClaimOwnership()</c> then the <c>ForceRemove</c>
        /// RPC. Because <c>ForceRemove</c> only removes, the removed items are materialised from
        /// <paramref name="itemName"/>'s prefab — at the drawer's own stored quality and the current
        /// world level — and added to <paramref name="destination"/> with
        /// <c>Inventory.AddItem(ItemData)</c>.
        /// </para>
        /// <para>
        /// Delivery is chunked one stack at a time (<see cref="DrawerTake.NextChunk"/>): a single
        /// <c>AddItem(ItemData)</c> call places <b>at most one stack</b>, so handing it the whole
        /// amount would silently destroy everything beyond the first stack.
        /// </para>
        /// <para>
        /// A chunk is removed only once its delivery is <b>known</b> to be accepted. The gate is
        /// <see cref="DrawerTake.CanDeliver"/>, fed by <see cref="DrawerTake.MatchingFreeStackSpace"/>
        /// and the destination's empty-slot count, and it mirrors the very predicate
        /// <c>Inventory.AddItem(ItemData)</c> matches on: name, <b>quality</b> and world level must
        /// all match and the count must fit the matching free stack space plus the empty slots. It
        /// deliberately does not use <c>CanAddItem</c>: that method's <c>FindFreeStackSpace</c>
        /// ignores quality (and the <c>GameObject</c> overload reads the prefab's default world
        /// level), so a same-name partial stack of a different quality with no empty slot makes it
        /// true while <c>AddItem</c> refuses the very same chunk.
        /// </para>
        /// <para>
        /// If a delivery is refused anyway, the chunk is put straight back into the drawer through
        /// its own <c>AddItem_Request</c> RPC, so the failed chunk leaves the drawer exactly as it
        /// was. The drawer therefore never gives up more than the player received, at every step and
        /// at the end: <c>removed == delivered</c>. With a null destination, an unresolvable prefab
        /// or no room for even the first chunk this removes nothing and returns 0 rather than
        /// destroying the items (the design's top anti-goal).
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

            // Resolve the prefab before touching the drawer: without a usable item to materialise
            // there is nothing that could be delivered.
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(itemName) : null;
            ItemDrop itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            ItemDrop.ItemData blueprint = itemDrop != null ? itemDrop.m_itemData : null;
            if (blueprint == null || blueprint.m_shared == null || blueprint.m_shared.m_maxStackSize <= 0)
            {
                Log.LogWarning("Drawer '" + ObjectName + "' reports prefab '" + itemName +
                               "' that has no usable ItemDrop; taking nothing.");
                return 0;
            }

            int maxStack = blueprint.m_shared.m_maxStackSize;
            string sharedName = blueprint.m_shared.m_name;
            int quality = CurrentQuality();
            // AddItem(GameObject, int) stores Game.m_worldLevel truncated to a byte; mirror that
            // exactly so the gate and the delivery agree on the world level they match on.
            int worldLevel = (byte)Game.m_worldLevel;

            // Deliver one stack at a time. Each chunk's removal is gated on the same predicate the
            // delivery uses, so a chunk is never removed unless it is known to be accepted.
            int delivered = 0;
            int chunk;
            while ((chunk = DrawerTake.NextChunk(want, delivered, maxStack)) > 0)
            {
                int freeSpace = DrawerTake.MatchingFreeStackSpace(
                    sharedName, quality, worldLevel, DestinationStacks(destination));
                int emptySlots = DestinationEmptySlots(destination);

                if (!DrawerTake.CanDeliver(chunk, maxStack, freeSpace, emptySlots))
                {
                    Log.LogWarning("No room in the destination inventory for the next " + chunk +
                                   "x '" + itemName + "'; delivered " + delivered + " of " + want + ".");
                    break;
                }

                _handle.View.ClaimOwnership();
                _handle.View.InvokeRPC(DrawerApi.ForceRemoveRpcName, chunk);

                // The item is materialised at the drawer's quality and the world level the gate
                // matched on, so AddItem(ItemData) merges into exactly the stacks the gate counted.
                ItemDrop.ItemData toAdd = blueprint.Clone();
                toAdd.m_dropPrefab = prefab;
                toAdd.m_stack = chunk;
                toAdd.m_quality = quality;
                toAdd.m_worldLevel = (byte)worldLevel;

                if (!destination.AddItem(toAdd))
                {
                    // Unreachable while the gate mirrors AddItem's own predicate; kept as a last
                    // line of defence so a refused chunk can never silently destroy the items.
                    Log.LogError("A drawer gave up " + chunk + "x '" + itemName +
                                 "' but the destination refused them; refunding them to the drawer.");
                    Refund(itemName, chunk, quality);
                    break;
                }

                delivered += chunk;
            }

            return delivered;
        }

        /// <summary>The drawer's stored quality, read live from its ZDO; never below 1.</summary>
        private int CurrentQuality()
        {
            ZDO zdo = _handle.View != null ? _handle.View.GetZDO() : null;
            int quality = zdo != null ? zdo.GetInt(DrawerApi.QualityKey, 1) : 1;
            return quality < 1 ? 1 : quality;
        }

        /// <summary>The destination's stacks, reduced to the pure view the delivery gate matches on.</summary>
        private static List<DrawerTake.InventoryStack> DestinationStacks(Inventory destination)
        {
            var stacks = new List<DrawerTake.InventoryStack>();
            if (destination == null || destination.m_inventory == null)
                return stacks;

            for (int i = 0; i < destination.m_inventory.Count; i++)
            {
                ItemDrop.ItemData item = destination.m_inventory[i];
                if (item == null || item.m_shared == null)
                    continue;

                stacks.Add(new DrawerTake.InventoryStack(
                    item.m_shared.m_name,
                    item.m_quality,
                    item.m_worldLevel,
                    item.m_stack,
                    item.m_shared.m_maxStackSize));
            }
            return stacks;
        }

        /// <summary>The destination's empty slot count, the way <c>Inventory</c> itself computes it.</summary>
        private static int DestinationEmptySlots(Inventory destination)
        {
            if (destination == null || destination.m_inventory == null)
                return 0;

            return destination.m_width * destination.m_height - destination.m_inventory.Count;
        }

        /// <summary>
        /// Puts a chunk that could not be delivered back into the drawer through the drawer's own add
        /// RPC (<c>AddItem_Request</c>), so a failed chunk is a no-op. Ownership is claimed first
        /// because the handler ignores the request unless this peer owns the drawer.
        /// </summary>
        private void Refund(string prefabName, int amount, int quality)
        {
            _handle.View.ClaimOwnership();
            _handle.View.InvokeRPC(DrawerApi.AddItemRequestRpcName, prefabName, amount, quality);
        }
    }
}
