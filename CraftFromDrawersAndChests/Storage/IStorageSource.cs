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
        /// storage's supported mutation path and returns how many were actually taken.
        /// <para>
        /// The contract is: a value <c>&gt;= 0</c> is the number actually taken (which may be fewer
        /// than requested, or zero), and <see cref="ChestTake.Deferred"/> (<c>-1</c>) means the
        /// removal was <b>routed</b> to another player and the count is not knowable synchronously. A
        /// deferred result is not a failure and must <b>not</b> be retried: the items are already
        /// leaving the storage and will arrive later, so a retry would duplicate them. A caller that
        /// needs a definite outcome must decline such a source before calling it (for a chest, via
        /// <c>VanillaChestSource.CanTakeSynchronously</c>).
        /// </para>
        /// <para>
        /// A source fulfils a request in one call, so when a removal inside that call is routed the
        /// source returns the sentinel <b>whatever</b> was already delivered before it. The loop asks
        /// the routed removal for the whole un-returned remainder of the ask, but MultiUserChest clamps
        /// the routed amount to <c>min(request, located stack, destination free space)</c>, so the
        /// request covers at most that remainder — <c>delivered + routed == requested</c> is <b>not</b>
        /// an unconditional invariant and can under-cover when the located stack or the destination is
        /// short. A caller's behaviour is unaffected: a short positive count still means "ask again for
        /// the remainder" and a sentinel still means "this line is settled" — never a shortfall to
        /// retry, because the request is already in flight and a retry would duplicate it. The covered
        /// amount is the clamped one, so the sentinel means "the rest is on its way", not "the whole
        /// remainder is on its way".
        /// </para>
        /// </summary>
        /// <param name="itemName">The item's prefab name; see <see cref="Count"/>.</param>
        /// <param name="amount">The maximum number to take.</param>
        /// <param name="destination">
        /// Where the taken items are delivered. Every implementation must move the items into it: a
        /// source that cannot deliver (a null destination, an unresolvable prefab, or no room) takes
        /// nothing and returns 0 rather than destroying the items.
        /// </param>
        int Take(string itemName, int amount, Inventory destination);
    }
}
