using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using UnityEngine;

namespace CraftFromDrawersAndChests.Storage
{
    /// <summary>
    /// One drawer as seen by the rest of the mod: its live <see cref="ZNetView"/> plus the
    /// prefab/amount/quality reported when it was queried. The wrapper is a snapshot; the
    /// authoritative values are read from the ZDO by <see cref="DrawerSource"/>.
    /// </summary>
    public readonly struct DrawerHandle
    {
        public readonly ZNetView View;
        public readonly string Prefab;
        public readonly int Amount;
        public readonly int Quality;

        public DrawerHandle(ZNetView view, string prefab, int amount, int quality)
        {
            View = view;
            Prefab = prefab;
            Amount = amount;
            Quality = quality;
        }
    }

    /// <summary>
    /// The reflection front door to KG Item Drawers. It has no compile-time dependency on the mod
    /// (which is a soft dependency): the contract is resolved once into
    /// <see cref="Logic.DrawerContract"/>, and the two API shapes are driven through the cached
    /// <see cref="MethodInfo"/>s.
    /// <para>
    /// When no contract resolves — Item Drawers is absent, or its API type was renamed again —
    /// <see cref="IsAvailable"/> is false and every query returns empty. Nothing here throws
    /// (Review Focus 5): the mod runs chest-only.
    /// </para>
    /// </summary>
    public static class DrawerApi
    {
        // The drawer protocol's ZDO keys and RPC name, straight from kg_ItemDrawers 1.4.0. Single
        // source of truth for these literals across Storage/.
        public const string PrefabKey = "Prefab";
        public const string AmountKey = "Amount";
        public const string QualityKey = "Quality";

        /// <summary>The drawer component type name, matched by <c>GameObject.GetComponent(string)</c>.</summary>
        public const string DrawerComponentTypeName = "DrawerComponent";

        /// <summary>The one supported removal RPC; never <c>WithdrawItem_Request</c>.</summary>
        public const string ForceRemoveRpcName = "ForceRemove";

        /// <summary>
        /// The drawer's own add RPC, used only to refund a chunk whose delivery failed after it had
        /// been removed. Its handler is <c>RPC_AddItem(sender, prefab, amount, quality)</c>; it
        /// ignores the request unless this peer owns the drawer, so ownership is claimed first.
        /// </summary>
        public const string AddItemRequestRpcName = "AddItem_Request";

        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        private static readonly DrawerHandle[] NoHandles = new DrawerHandle[0];

        // Declaration order matters: the static initialisers below run top to bottom, so Contract
        // must resolve before the Drawer-type reflection below derives from it.
        private static readonly DrawerContract Contract = DrawerContract.Resolve(Type.GetType);
        private static readonly Type DrawerViewType = ResolveDrawerViewType();
        private static readonly FieldInfo DrawerViewField = FindZNetViewField(DrawerViewType);
        private static readonly FieldInfo DrawerPrefabField = FindField(DrawerViewType, PrefabKey);
        private static readonly FieldInfo DrawerAmountField = FindField(DrawerViewType, AmountKey);
        private static readonly FieldInfo DrawerQualityField = FindField(DrawerViewType, QualityKey);

        /// <summary>True when an Item Drawers contract resolved; false means drawers are unsupported this session.</summary>
        public static bool IsAvailable
        {
            get { return Contract.IsAvailable; }
        }

        /// <summary>
        /// Every drawer within <paramref name="range"/> of <paramref name="origin"/>. Uses the
        /// preferred contract's native range query, or the fallback's full list filtered here.
        /// Returns empty when Item Drawers is unavailable or a query fails.
        /// </summary>
        public static IReadOnlyList<DrawerHandle> AllInRange(Vec3 origin, float range)
        {
            if (!Contract.IsAvailable)
                return NoHandles;

            try
            {
                if (Contract.Kind == DrawerContractKind.ItemDrawersApi)
                    return FromItemDrawersApi(new Vector3(origin.X, origin.Y, origin.Z), range);

                return FromClientSideV2(origin, range);
            }
            catch (Exception e)
            {
                // A defensive net: a shape change inside the drawer mod must degrade to "no
                // drawers", never take the crafting path down with it.
                Log.LogWarning("Item Drawers query failed; treating as no drawers in range. " + e);
                return NoHandles;
            }
        }

        /// <summary>
        /// True when <paramref name="view"/> is a drawer. Matches the drawer component by name so it
        /// needs no compile-time type, and is safe on a destroyed object (Unity's null check).
        /// </summary>
        public static bool IsDrawer(ZNetView view)
        {
            if (view == null)
                return false;

            GameObject go = view.gameObject;
            return go != null && go.GetComponent(DrawerComponentTypeName) != null;
        }

        private static IReadOnlyList<DrawerHandle> FromItemDrawersApi(Vector3 position, float range)
        {
            var raw = Contract.RangeQuery.Invoke(null, new object[] { position, range }) as IEnumerable;
            if (raw == null)
                return NoHandles;

            var handles = new List<DrawerHandle>();
            foreach (object drawer in raw)
            {
                if (drawer == null)
                    continue;

                ZNetView view = ReadView(drawer);
                if (view == null)
                    continue;

                handles.Add(new DrawerHandle(
                    view,
                    ReadString(DrawerPrefabField, drawer),
                    ReadInt(DrawerAmountField, drawer),
                    ReadInt(DrawerQualityField, drawer)));
            }
            return handles;
        }

        private static IReadOnlyList<DrawerHandle> FromClientSideV2(Vec3 origin, float range)
        {
            var raw = Contract.AllDrawersQuery.Invoke(null, null) as IEnumerable;
            if (raw == null)
                return NoHandles;

            var handles = new List<DrawerHandle>();
            foreach (object item in raw)
            {
                var view = item as ZNetView;
                if (view == null || !view.IsValid())
                    continue;

                Vector3 p = view.transform.position;

                // The same range rule the chest path uses (Eligibility.IsWithinRange), so a
                // non-positive range cannot mean unlimited for chests and nothing for drawers.
                if (!Eligibility.IsWithinRange(range, new Vec3(p.x, p.y, p.z).SqrDistanceTo(origin)))
                    continue;

                ZDO zdo = view.GetZDO();
                if (zdo == null)
                    continue;

                handles.Add(new DrawerHandle(
                    view,
                    zdo.GetString(PrefabKey, string.Empty),
                    zdo.GetInt(AmountKey, 0),
                    zdo.GetInt(QualityKey, 1)));
            }
            return handles;
        }

        /// <summary>The preferred contract's <c>Drawer</c> element type, from the range query's return type.</summary>
        private static Type ResolveDrawerViewType()
        {
            MethodInfo range = Contract.RangeQuery;
            if (range == null)
                return null;

            Type returnType = range.ReturnType;
            if (returnType.IsArray)
                return returnType.GetElementType();
            if (returnType.IsGenericType)
                return returnType.GetGenericArguments()[0];
            return null;
        }

        /// <summary>
        /// Locates the drawer's <see cref="ZNetView"/> field. The real <c>Drawer</c> keeps it in a
        /// private field, so find it by type rather than by a name that could change.
        /// </summary>
        private static FieldInfo FindZNetViewField(Type drawerViewType)
        {
            if (drawerViewType == null)
                return null;

            FieldInfo[] fields = drawerViewType.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType == typeof(ZNetView))
                    return fields[i];
            }
            return null;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            return type != null ? type.GetField(name) : null;
        }

        private static ZNetView ReadView(object drawer)
        {
            return DrawerViewField != null ? DrawerViewField.GetValue(drawer) as ZNetView : null;
        }

        private static string ReadString(FieldInfo field, object instance)
        {
            return field != null ? field.GetValue(instance) as string : null;
        }

        private static int ReadInt(FieldInfo field, object instance)
        {
            object value = field != null ? field.GetValue(instance) : null;
            return value is int ? (int)value : 0;
        }
    }
}
