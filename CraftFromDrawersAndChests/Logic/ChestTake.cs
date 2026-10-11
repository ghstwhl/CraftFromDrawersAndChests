namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure decisions behind removing items from a vanilla chest through MultiUserChest, consumed
    /// by the game-facing <c>Storage.MultiUserChestBridge</c> and <c>Storage.VanillaChestSource.Take</c>.
    /// <para>
    /// MultiUserChest's <c>ContainerHandler.RemoveItemFromChest</c> answers a removal in one of three
    /// ways, and which one it is decides both whether the returned count can be trusted and whether a
    /// request has already been sent:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Applied inline</b> — this client owns the chest, so the removal and the
    /// delivery happen synchronously inside the call and it returns no request. The count is
    /// definite.</description></item>
    /// <item><description><b>Declined</b> — nothing can be removed (the chest holds none of the item,
    /// or the destination has no room). It returns a request whose drag amount is zero, so the count
    /// is a definite zero.</description></item>
    /// <item><description><b>Routed</b> — another player owns the chest, so the request is sent to
    /// their client and applied asynchronously. There is no synchronous count, and the request is
    /// already in flight, so <see cref="Result"/> must report <see cref="Deferred"/> rather than
    /// zero: a caller that read zero as "the chest was empty" would retry, and the item — which does
    /// still move — would arrive twice.</description></item>
    /// </list>
    /// Lives in <c>Logic/</c> so these decisions are unit-testable without a live chest or inventory.
    /// </summary>
    public static class ChestTake
    {
        /// <summary>
        /// The value a chest removal returns when it was routed to another player and the outcome is
        /// not knowable synchronously. It is deliberately distinct from <c>0</c>, which means "a
        /// definite zero items moved". A caller must treat <see cref="Deferred"/> as "something may
        /// still arrive; do not retry", never as "the chest was empty".
        /// </summary>
        public const int Deferred = -1;

        /// <summary>
        /// Whether a chest can be served synchronously. A chest nobody owns has nobody to route a
        /// request to, so it is claimed and becomes ours — a local, synchronous removal — and a chest
        /// we already own is local too. A chest owned by another player cannot be: the request would
        /// be routed, and by the time the removal call returns it has already been sent, so such a
        /// chest must be declined <b>before</b> anything is sent.
        /// </summary>
        /// <param name="hasOwner">Whether the chest's <c>ZNetView</c> currently has an owner.</param>
        /// <param name="isOwner">Whether this client is that owner.</param>
        /// <returns>True when a removal from the chest is applied locally and synchronously.</returns>
        public static bool CanServeSynchronously(bool hasOwner, bool isOwner)
        {
            return !hasOwner || isOwner;
        }

        /// <summary>
        /// Whether MultiUserChest <b>routed</b> the request to the owning client instead of applying
        /// it locally. A non-null request carrying a positive drag amount was sent; a null request
        /// means it was applied inline, and a non-null request with a non-positive drag amount means
        /// it was declined locally.
        /// </summary>
        /// <param name="requestIsNull">Whether MultiUserChest returned no request (its inline path).</param>
        /// <param name="requestDragAmount">
        /// The request's drag amount; only meaningful when <paramref name="requestIsNull"/> is false.
        /// </param>
        public static bool WasRouted(bool requestIsNull, int requestDragAmount)
        {
            return !requestIsNull && requestDragAmount > 0;
        }

        /// <summary>
        /// The value a chest removal must return for a given outcome: <see cref="Deferred"/> when the
        /// request was routed (the item is already in flight and the count is not knowable yet),
        /// otherwise the number actually delivered. A delivered delta is clamped at zero so a
        /// miscounted or negative delta can never masquerade as the <see cref="Deferred"/> sentinel.
        /// </summary>
        /// <param name="routed">Whether the removal was routed to another player.</param>
        /// <param name="delivered">The number of items that reached the destination, as measured.</param>
        public static int Result(bool routed, int delivered)
        {
            if (routed)
                return Deferred;

            return delivered < 0 ? 0 : delivered;
        }

        /// <summary>
        /// Whether a removal that was <b>not</b> routed is worth warning about: it delivered nothing,
        /// so the chest held none of the item or the destination had no room. A routed request is
        /// excluded — its items are expected, just later — so this never fires for one.
        /// </summary>
        /// <param name="routed">Whether the removal was routed to another player.</param>
        /// <param name="delivered">The number of items that reached the destination, as measured.</param>
        public static bool DeliveredNothing(bool routed, int delivered)
        {
            return !routed && delivered <= 0;
        }
    }
}
