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
        /// Removal is all-or-nothing per call and uses only the supported path:
        /// <c>ClaimOwnership()</c> then the <c>ForceRemove</c> RPC. Because <c>ForceRemove</c> only
        /// removes, the items are then materialised from <paramref name="itemName"/>'s prefab and
        /// added to <paramref name="destination"/> — the same delivery the chest path performs
        /// through <c>MultiUserChestBridge.RemoveFromChest(chest, item, destination, ...)</c>.
        /// </para>
        /// <para>
        /// Nothing is removed unless it can be delivered: with a null destination, an unresolvable
        /// prefab or no room in the destination, this removes nothing and returns 0 rather than
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
            int take = DrawerTake.AmountToRemove(amount, Count(itemName), destination != null);
            if (take <= 0)
                return 0;

            // Resolve and capacity-check before removing anything: if the items cannot be delivered,
            // removing them would destroy them.
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(itemName) : null;
            if (prefab == null)
            {
                Log.LogWarning("Drawer '" + ObjectName + "' reports prefab '" + itemName +
                               "' that has no ItemDrop; taking nothing.");
                return 0;
            }

            if (!destination.CanAddItem(prefab, take))
            {
                Log.LogWarning("No room in the destination inventory for " + take + "x '" + itemName +
                               "'; taking nothing from the drawer.");
                return 0;
            }

            _handle.View.ClaimOwnership();
            _handle.View.InvokeRPC(DrawerApi.ForceRemoveRpcName, take);

            // AddItem(GameObject, int) clones the prefab's ItemData, sets its stack and adds it
            // stack-aware; the capacity check above makes this succeed for the full amount.
            if (!destination.AddItem(prefab, take))
            {
                // Unreachable after the capacity check, but never silently swallowed.
                Log.LogError("A drawer gave up " + take + "x '" + itemName +
                             "' but the destination refused them.");
                return 0;
            }

            return take;
        }
    }
}
