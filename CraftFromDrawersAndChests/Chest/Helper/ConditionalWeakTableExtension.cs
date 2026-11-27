using System.Runtime.CompilerServices;

namespace CraftFromDrawersAndChests.Chest {
    public static class ConditionalWeakTableExtension {
        public static void TryAdd<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> table, TKey key, ConditionalWeakTable<TKey, TValue>.CreateValueCallback createCallback) where TKey : class where TValue : class {
            table.GetValue(key, createCallback);
        }
    }
}
