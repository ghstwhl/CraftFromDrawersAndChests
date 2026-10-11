using CraftFromDrawersAndChests.Logic;

namespace CraftFromDrawersAndChests.Storage
{
    /// <summary>
    /// One storage the mod can pull from, whichever kind it is. A chest is a vanilla
    /// <c>Valheim.Container</c>; a drawer is a KG Item Drawers <c>ZNetView</c> and is not a
    /// <c>Container</c>. The pull engine only sees this interface, so the two kinds are
    /// interchangeable to it.
    /// <para>
    /// The "storage kind" of the value type is re-exported through <see cref="Kind"/> so the
    /// decision logic can stay keyed on it. This type is game-facing; the pure <c>Logic/</c> layer
    /// never sees it.
    /// </para>
    /// </summary>
    public interface IStorageSource
    {
        /// <summary>Whether this is a chest or a drawer.</summary>
        StorageKind Kind { get; }

        /// <summary>The source's world position, as the pure <see cref="Vec3"/> the range maths uses.</summary>
        Vec3 Position { get; }

        /// <summary>The GameObject's name, used for chest-type exclusion rules; may be null.</summary>
        string ObjectName { get; }

        /// <summary>
        /// The per-storage off switch, a shared world property stored on the source's own ZDO. Off
        /// means excluded from pulling for everyone.
        /// </summary>
        bool ToggledOff { get; }

        /// <summary>False once the underlying game object has been destroyed.</summary>
        bool IsValid { get; }

        /// <summary>
        /// How many of <paramref name="itemName"/> this storage holds.
        /// </summary>
        /// <param name="itemName">
        /// The item's <b>prefab name</b> (e.g. <c>"Wood"</c>), not its localized
        /// <c>m_shared.m_name</c> (e.g. <c>"$item_wood"</c>). A drawer's ZDO stores a prefab name
        /// under <c>"Prefab"</c>, and the other storage mods key by prefab too, so this parameter is
        /// compared against the ZDO prefab directly.
        /// </param>
        int Count(string itemName);

        /// <summary>
        /// Removes up to <paramref name="amount"/> of <paramref name="itemName"/> through the
        /// storage's supported mutation path and returns how many were actually taken (which may be
        /// fewer than requested, or zero).
        /// </summary>
        /// <param name="itemName">The item's prefab name; see <see cref="Count"/>.</param>
        /// <param name="amount">The maximum number to take.</param>
        /// <param name="destination">Where the items should end up, for paths that move them directly.</param>
        int Take(string itemName, int amount, Inventory destination);
    }
}
