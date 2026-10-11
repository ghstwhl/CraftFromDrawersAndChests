using System;
using System.Reflection;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// Which of KG Item Drawers' two reflection contracts this build can use. <see cref="None"/>
    /// is the soft-dependency fallback: Item Drawers is absent (or its API was renamed again) and
    /// the mod runs chest-only without throwing.
    /// </summary>
    public enum DrawerContractKind
    {
        None,
        ItemDrawersApi,
        ClientSideV2,
    }

    /// <summary>
    /// The resolved Item Drawers contract: which API type is available and the cached
    /// <see cref="MethodInfo"/>s used to query it. Like the rest of <c>Logic/</c> this type is
    /// pure — it touches only <c>System.Reflection</c>, never UnityEngine, Valheim, BepInEx or
    /// Jötunn — so the resolution decision can be unit-tested with stub types. The game-facing
    /// wrapper (<c>Storage.DrawerApi</c>) supplies the real type lookup and turns the results into
    /// handles.
    /// </summary>
    /// <remarks>
    /// Verified against <c>kg_ItemDrawers</c> 1.4.0:
    /// <list type="bullet">
    /// <item><description><c>API.ItemDrawers_API</c> — static property <c>AllDrawers</c>
    /// (<c>List&lt;Drawer&gt;</c>) plus static <c>AllDrawersInRange(Vector3, float)</c>.</description></item>
    /// <item><description><c>API.ClientSideV2</c> — static <c>AllDrawers()</c> returning
    /// <c>List&lt;ZNetView&gt;</c>; the caller filters by distance.</description></item>
    /// </list>
    /// </remarks>
    public readonly struct DrawerContract
    {
        /// <summary>Assembly-qualified name of the preferred contract's type.</summary>
        public const string ItemDrawersApiTypeName = "API.ItemDrawers_API, kg_ItemDrawers";

        /// <summary>Assembly-qualified name of the fallback contract's type.</summary>
        public const string ClientSideV2TypeName = "API.ClientSideV2, kg_ItemDrawers";

        /// <summary>The all-drawers query's member name (a method on the fallback, a property on the preferred type).</summary>
        public const string AllDrawersName = "AllDrawers";

        /// <summary>The preferred contract's range query method name.</summary>
        public const string AllDrawersInRangeName = "AllDrawersInRange";

        /// <summary>
        /// Simple name of the range query's position parameter type (<c>UnityEngine.Vector3</c>).
        /// <c>Logic/</c> may not reference UnityEngine, so parameter types are matched by name.
        /// </summary>
        public const string Vector3ParameterTypeName = "Vector3";

        /// <summary>Simple name of <see cref="float"/> (<c>System.Single</c>), the range query's second parameter type.</summary>
        public const string SingleParameterTypeName = "Single";

        private static readonly string[] NoParameterTypes = new string[0];

        private static readonly string[] RangeParameterTypes =
            new[] { Vector3ParameterTypeName, SingleParameterTypeName };

        public readonly DrawerContractKind Kind;

        /// <summary>The cached all-drawers query getter/method; null when <see cref="Kind"/> is <see cref="DrawerContractKind.None"/>.</summary>
        public readonly MethodInfo AllDrawersQuery;

        /// <summary>The cached native range query, set only for <see cref="DrawerContractKind.ItemDrawersApi"/>.</summary>
        public readonly MethodInfo RangeQuery;

        public DrawerContract(DrawerContractKind kind, MethodInfo allDrawersQuery, MethodInfo rangeQuery)
        {
            Kind = kind;
            AllDrawersQuery = allDrawersQuery;
            RangeQuery = rangeQuery;
        }

        /// <summary>True when some contract resolved and queries may be attempted.</summary>
        public bool IsAvailable
        {
            get { return Kind != DrawerContractKind.None; }
        }

        /// <summary>
        /// Resolves the available contract, preferring <c>API.ItemDrawers_API</c> and falling back
        /// to <c>API.ClientSideV2</c>. <paramref name="typeLookup"/> is the injected type lookup
        /// (in the game it is <c>Type.GetType</c>; in tests a stub), so the decision is pure and
        /// testable. A missing type, a partial rename, a null lookup or a throwing lookup all
        /// yield <see cref="DrawerContractKind.None"/> rather than an exception — Review Focus 5.
        /// </summary>
        public static DrawerContract Resolve(Func<string, Type> typeLookup)
        {
            if (typeLookup == null)
                return default(DrawerContract);

            // Preferred: API.ItemDrawers_API — its native AllDrawersInRange(pos, range) is the
            // contract worth having. Without that method the type is not usable as preferred.
            Type api = Lookup(typeLookup, ItemDrawersApiTypeName);
            if (api != null)
            {
                MethodInfo range = FindStaticMethod(api, AllDrawersInRangeName, RangeParameterTypes);
                if (range != null)
                {
                    return new DrawerContract(
                        DrawerContractKind.ItemDrawersApi,
                        FindAllDrawersQuery(api),
                        range);
                }
            }

            // Fallback: API.ClientSideV2.AllDrawers() -> List<ZNetView>; we filter by distance.
            Type v2 = Lookup(typeLookup, ClientSideV2TypeName);
            if (v2 != null)
            {
                MethodInfo all = FindStaticMethod(v2, AllDrawersName, NoParameterTypes);
                if (all != null)
                    return new DrawerContract(DrawerContractKind.ClientSideV2, all, null);
            }

            return default(DrawerContract);
        }

        private static Type Lookup(Func<string, Type> typeLookup, string qualifiedName)
        {
            try
            {
                return typeLookup(qualifiedName);
            }
            catch
            {
                // A lookup that throws (e.g. a broken assembly probe) is treated as "not found".
                return null;
            }
        }

        /// <summary>
        /// Finds the public static method <paramref name="name"/> whose parameters match
        /// <paramref name="parameterTypeNames"/> by position. Matching the parameter types — not
        /// the arity alone — means a future same-arity overload cannot bind the wrong
        /// <see cref="MethodInfo"/> and then throw at <c>Invoke</c> time, which would silently
        /// degrade the whole drawer path to "no drawers". The names are simple type names so the
        /// pure layer needs no UnityEngine reference.
        /// </summary>
        private static MethodInfo FindStaticMethod(Type type, string name, string[] parameterTypeNames)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != name)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != parameterTypeNames.Length)
                    continue;

                bool match = true;
                for (int j = 0; j < parameters.Length; j++)
                {
                    if (parameters[j].ParameterType.Name != parameterTypeNames[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return method;
            }
            return null;
        }

        /// <summary>
        /// Finds the all-drawers query. The preferred type exposes <c>AllDrawers</c> as a static
        /// property, so fall back to its getter when no matching method exists.
        /// </summary>
        private static MethodInfo FindAllDrawersQuery(Type type)
        {
            MethodInfo method = FindStaticMethod(type, AllDrawersName, NoParameterTypes);
            if (method != null)
                return method;

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.Name == AllDrawersName && property.GetGetMethod() != null)
                    return property.GetGetMethod();
            }
            return null;
        }
    }
}
