using System;
using CraftFromDrawersAndChests.Logic;
using UnityEngine;

namespace CraftFromDrawersAndChests.Storage
{
    /// <summary>
    /// Adapts a KG Item Drawers drawer (a <c>ZNetView</c>, not a <c>Container</c>) to
    /// <see cref="IStorageSource"/>. Values are read live from the drawer's ZDO; the only mutation
    /// is the supported removal path, <c>ClaimOwnership()</c> followed by the <c>ForceRemove</c>
    /// RPC.
    /// </summary>
    public sealed class DrawerSource : IStorageSource
    {
        /// <summary>The shared world off-switch key, hashed the same way the ZDO stores it.</summary>
        private static readonly int NoPullHash = ConfigModel.ZdoKey.GetStableHashCode();

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
        /// Takes up to <paramref name="amount"/> of the given prefab out of the drawer. Removal is
        /// all-or-nothing per call and uses only the supported path: <c>ClaimOwnership()</c> then the
        /// <c>ForceRemove</c> RPC.
        /// <para>
        /// Never <c>WithdrawItem_Request</c> — it spawns a world drop at the player and clears an
        /// empty drawer's fields — and never a direct write to the drawer's ZDO <c>"Amount"</c>.
        /// </para>
        /// </summary>
        /// <param name="destination">
        /// Unused for drawers. <c>ForceRemove</c> only removes items from the drawer; the caller adds
        /// them to the player afterwards. The parameter exists to satisfy <see cref="IStorageSource"/>.
        /// </param>
        public int Take(string itemName, int amount, Inventory destination)
        {
            if (amount <= 0)
                return 0;

            int take = Math.Min(amount, Count(itemName));
            if (take <= 0)
                return 0;

            _handle.View.ClaimOwnership();
            _handle.View.InvokeRPC(DrawerApi.ForceRemoveRpcName, take);
            return take;
        }
    }
}
