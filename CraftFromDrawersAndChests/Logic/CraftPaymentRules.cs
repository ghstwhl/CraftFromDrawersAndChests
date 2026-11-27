namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// What may happen to a craft or a placement now that storage can pay for it: proceed if the cost can
    /// be taken immediately, fetch and hold if part of it has to come from a chest another player owns,
    /// or refuse.
    /// </summary>
    public enum CraftPaymentAction
    {
        /// <summary>Everything the recipe or piece needs is either held or takeable right now; vanilla may go ahead.</summary>
        Proceed,

        /// <summary>
        /// Part of the cost must be pulled from a chest another player owns, so the removal is routed and
        /// the items arrive a moment later. The craft must be <b>held</b> until they do: vanilla creates the
        /// item and consumes the materials in the same call, so proceeding now would leave part of the cost
        /// unpaid.
        /// </summary>
        FetchAndHold,

        /// <summary>Nothing in range can pay for it, or the inventory cannot take what the pull would deliver.</summary>
        Refuse
    }

    /// <summary>What to do with a craft that is waiting for a routed fetch to land.</summary>
    public enum PendingCraftOutcome
    {
        /// <summary>The items are still in flight; keep holding.</summary>
        Wait,

        /// <summary>The player now holds the whole cost; the craft may run.</summary>
        Complete,

        /// <summary>They never arrived; give up and say so rather than hold the panel forever.</summary>
        Abandon
    }

    /// <summary>
    /// The payment decisions behind "both players can craft from the same chest". Pure, so the sequencing
    /// can be tested without a game.
    /// <para>
    /// <b>Why a hold is needed at all.</b> A removal from a chest owned by another player is routed over the
    /// network: the items are not in the player's inventory when <c>Take</c> returns, they arrive with the
    /// response. Vanilla creates the crafted item and consumes the materials in one call, so letting it run
    /// while a line is still in flight consumes less than the recipe costs — a free craft. The hold moves the
    /// craft to the moment the cost is actually in hand.
    /// </para>
    /// </summary>
    public static class CraftPaymentRules
    {
        /// <summary>
        /// How long a held craft waits for its routed fetch before giving up. Long enough for a round trip
        /// and a slow frame, short enough that a failed request does not leave the player waiting.
        /// </summary>
        public const float PendingTimeoutSeconds = 3f;

        /// <summary>
        /// Chooses what to do with the craft or placement that is about to happen.
        /// </summary>
        /// <param name="planFeasible">Whether storage within range can cover the shortfall at all.</param>
        /// <param name="destinationAllows">Whether the player's inventory can accept what the pull would deliver.</param>
        /// <param name="linesNeedingFetch">How many plan lines must be served by a chest another player owns.</param>
        public static CraftPaymentAction Decide(bool planFeasible, bool destinationAllows, int linesNeedingFetch)
        {
            if (!planFeasible || !destinationAllows)
                return CraftPaymentAction.Refuse;

            if (linesNeedingFetch > 0)
                return CraftPaymentAction.FetchAndHold;

            return CraftPaymentAction.Proceed;
        }

        /// <summary>
        /// Whether a held craft may run, must keep waiting, or has waited too long.
        /// </summary>
        /// <param name="playerHoldsEverything">Whether the player's own inventory now covers the whole cost.</param>
        /// <param name="secondsWaited">How long the craft has been held.</param>
        public static PendingCraftOutcome EvaluatePending(bool playerHoldsEverything, float secondsWaited)
        {
            if (playerHoldsEverything)
                return PendingCraftOutcome.Complete;

            if (secondsWaited >= PendingTimeoutSeconds)
                return PendingCraftOutcome.Abandon;

            return PendingCraftOutcome.Wait;
        }
    }
}
