namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// What the chest layer does with a container-open request, in the order the branches are taken.
    /// </summary>
    public enum ChestOpenAction
    {
        /// <summary>Let the game's own <c>Container.RPC_RequestOpen</c> answer; this layer stays out.</summary>
        Vanilla,

        /// <summary>Refuse: the requester has no access to this container.</summary>
        DenyAccess,

        /// <summary>
        /// Grant access without moving ownership, because a player has already claimed the container. The
        /// requester's moves are routed to that owner by MultiUserChest, exactly as they are for a container
        /// another player is using.
        /// </summary>
        GrantInUse,

        /// <summary>
        /// Grant access and move ownership to the requester, after pushing the current data. Only chosen for
        /// a container nobody has claimed — in practice the server handing a fresh container to its first
        /// user.
        /// </summary>
        HandOver
    }

    /// <summary>
    /// The pure decision behind the absorbed chest layer's container-open prefix (upstream
    /// MultiUserChest 0.6.2, <c>ContainerPatch.ContainerRPC_RequestOpenPatch</c>).
    /// <para>
    /// Upstream answered an open request in four ways, and which one it picked decided whether the
    /// requester ended up owning the container. It wrote that as a chain of early returns inside the
    /// patch, where nothing could check it; here it is a truth table.
    /// </para>
    /// <para>
    /// <b>Custody, not the in-use flag.</b> Upstream moved ownership to the requester whenever the
    /// container was not flagged in use. Because a container owned by another player is excluded from
    /// this mod's availability arithmetic (<see cref="ChestTake.CanServeSynchronously"/>), that made
    /// merely opening a chest change who could count it: a player who closed a chest and then had
    /// another player open it lost that storage mid-craft (reported from play 2026-11-28, log evidence
    /// in the changelog entry). The rule here is therefore: <b>a container a player has claimed keeps
    /// that owner</b>; only a container nobody has claimed — the server acting as custodian, its ZDO
    /// owner still zero — is handed to the requester.
    /// </para>
    /// </summary>
    public static class ChestOpenRules
    {
        /// <summary>
        /// Whether the container counts as already in use by <i>someone else</i>. The requester's own open
        /// does not count.
        /// <para>
        /// Since 2.1.0 this no longer selects the branch — custody does (see
        /// <see cref="Decide"/>) — but it is still reported on the diagnostic line, because "was the
        /// container flagged in use at the time" is what distinguishes a sharing player from a player who
        /// closed the UI.
        /// </para>
        /// </summary>
        public static bool IsInUse(bool containerInUse, bool wagonInUse, bool requesterIsLocal)
        {
            return (containerInUse || wagonInUse) && !requesterIsLocal;
        }

        /// <summary>
        /// Chooses the branch for an open request. Inputs mirror the upstream guards:
        /// <paramref name="ignored"/> is <c>Container.IgnoreInventory()</c>, <paramref name="isOwner"/> is
        /// the receiving instance's <c>ZNetView.IsOwner()</c>, <paramref name="hasAccess"/> is
        /// <c>Container.CheckAccess(playerId)</c>, and <paramref name="containerClaimed"/> is "the ZDO has
        /// an explicit owner" (<c>ZDO.GetOwner() != 0</c>).
        /// </summary>
        /// <remarks>
        /// <paramref name="containerClaimed"/> is false only on the instance that is acting as custodian of
        /// a container nobody has taken — in practice the server. A client is the owner only because a
        /// player claimed the container, so from a client's point of view it is always claimed and the
        /// answer is <see cref="ChestOpenAction.GrantInUse"/>.
        /// </remarks>
        public static ChestOpenAction Decide(
            bool ignored, bool isOwner, bool hasAccess, bool containerClaimed)
        {
            // Upstream: `if (IgnoreInventory() || !m_nview.IsOwner()) return true;` - the game answers.
            if (ignored || !isOwner)
                return ChestOpenAction.Vanilla;

            if (!hasAccess)
                return ChestOpenAction.DenyAccess;

            // A claimed container keeps its owner. The requester still gets to open it: the reply is the
            // same one upstream sent from its in-use branch, and MultiUserChest routes the requester's own
            // moves to the owner.
            if (containerClaimed)
                return ChestOpenAction.GrantInUse;

            return ChestOpenAction.HandOver;
        }
    }
}
