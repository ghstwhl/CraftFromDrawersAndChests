using System;
using System.Collections.Generic;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// One crafting/building requirement to satisfy from storage. Pure logic: it must never
    /// reference UnityEngine, Valheim, BepInEx or Jotunn.
    /// </summary>
    public readonly struct PullRequest
    {
        /// <summary>
        /// The requirement's own prefab name. Kept for callers that need it;
        /// <see cref="PullPlan.Plan"/> matches on <see cref="ItemName"/> and ignores this.
        /// </summary>
        public readonly string Prefab;

        /// <summary>The prefab name to match against <see cref="StorageStock.ItemName"/>.</summary>
        public readonly string ItemName;

        /// <summary>How many are required.</summary>
        public readonly int Amount;

        /// <summary>Whether this requirement may only be pulled when the player is at an upgrader.</summary>
        public readonly bool IsUpgraderResource;

        public PullRequest(string prefab, string itemName, int amount, bool isUpgraderResource)
        {
            Prefab = prefab;
            ItemName = itemName;
            Amount = amount;
            IsUpgraderResource = isUpgraderResource;
        }
    }

    /// <summary>
    /// How many of one item name a single source holds. One entry per (source, item name) pair.
    /// Pure logic, so it must never reference UnityEngine, Valheim, BepInEx or Jotunn.
    /// </summary>
    public readonly struct StorageStock
    {
        /// <summary>Opaque to <c>Logic</c>: the caller maps it back to an <c>IStorageSource</c>.</summary>
        public readonly string SourceId;

        /// <summary>The held item's prefab name (see <see cref="PullRequest.ItemName"/>).</summary>
        public readonly string ItemName;

        /// <summary>How many the source holds of this name.</summary>
        public readonly int Amount;

        /// <summary>Distance from the pull origin, used to order lines when <c>byDistance</c> is set.</summary>
        public readonly float Distance;

        public StorageStock(string sourceId, string itemName, int amount, float distance)
        {
            SourceId = sourceId;
            ItemName = itemName;
            Amount = amount;
            Distance = distance;
        }
    }

    /// <summary>
    /// One "take this many of this item from this source" instruction. It is a pure value; the
    /// caller executes it against the real storage. Pure logic, so it must never reference
    /// UnityEngine, Valheim, BepInEx or Jotunn.
    /// </summary>
    public readonly struct PullLine
    {
        public readonly string SourceId;
        public readonly string ItemName;
        public readonly int Amount;

        public PullLine(string sourceId, string itemName, int amount)
        {
            SourceId = sourceId;
            ItemName = itemName;
            Amount = amount;
        }
    }

    /// <summary>
    /// The outcome of a plan. When <see cref="Feasible"/> is true, <see cref="Lines"/> is the
    /// complete set of removals to perform. When it is false the plan is all-or-nothing: nothing
    /// may be consumed, so <see cref="Lines"/> is empty and <see cref="MissingItemNames"/> names
    /// every requirement that could not be filled, in request order. Neither list is ever null.
    /// <para>
    /// A <b>fill</b> plan (see <see cref="PullPlan.Plan"/>'s <c>fillUpTo</c>) is never infeasible for
    /// a shortfall: a request storage can only partly cover yields lines for the part it <i>can</i>
    /// cover, and <see cref="MissingItemNames"/> stays empty, because a fill asks for a maximum
    /// rather than a fixed amount.
    /// </para>
    /// </summary>
    public sealed class PullPlanResult
    {
        public bool Feasible { get; }

        public IReadOnlyList<PullLine> Lines { get; }

        public IReadOnlyList<string> MissingItemNames { get; }

        public PullPlanResult(bool feasible, IReadOnlyList<PullLine> lines, IReadOnlyList<string> missingItemNames)
        {
            Feasible = feasible;
            Lines = lines ?? Array.Empty<PullLine>();
            MissingItemNames = missingItemNames ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// The all-or-nothing pull planner: given what is required and what storage holds, it decides
    /// exactly which items come from which source, or refuses the whole plan. Pure logic, so it
    /// must never reference UnityEngine, Valheim, BepInEx or Jotunn.
    /// </summary>
    public static class PullPlan
    {
        /// <summary>
        /// Builds a plan for <paramref name="requests"/> against <paramref name="stocks"/>.
        /// <para>
        /// The whole plan is computed before anything is returned, so a caller that receives
        /// <c>Feasible = true</c> can execute every line without a second availability check, and
        /// a caller that receives <c>Feasible = false</c> consumes nothing.
        /// </para>
        /// </summary>
        /// <param name="requests">Requirements, in the order they should be satisfied.</param>
        /// <param name="stocks">One entry per (source, item name) pair.</param>
        /// <param name="leaveOne">When true, a source may contribute at most <c>Amount - 1</c>, so it always keeps one.</param>
        /// <param name="byDistance">When true, each request draws from the nearest sources first, and its lines come out nearest-first.</param>
        /// <param name="allowUpgraderResources">
        /// When false, any request with <see cref="PullRequest.IsUpgraderResource"/> is skipped
        /// entirely — it neither produces a line nor counts as missing. The caller passes true
        /// only when the player is actually at an upgrader.
        /// </param>
        /// <param name="fillUpTo">
        /// When false (the default) a request is a <b>fixed amount</b> and a shortfall on any one of
        /// them discards the whole plan — the all-or-nothing rule that keeps a craft from being paid
        /// for out of storage and then not happen.
        /// <para>
        /// When true the plan is a <b>fill</b>: each request asks for its amount but accepts
        /// <c>min(available, requested)</c>, so a request storage can only partly cover still yields
        /// lines for the part it can cover and is not reported missing. A fill is a different
        /// operation from a craft — "take as much as is available, up to the station's remaining
        /// capacity" — and must never be refused wholesale merely because storage cannot cover the
        /// whole amount (R31). A request with no stock at all simply produces no line; the caller
        /// then behaves exactly as the unmodified interaction does.
        /// </para>
        /// </param>
        public static PullPlanResult Plan(
            IReadOnlyList<PullRequest> requests,
            IReadOnlyList<StorageStock> stocks,
            bool leaveOne,
            bool byDistance,
            bool allowUpgraderResources,
            bool fillUpTo = false)
        {
            if (requests == null)
                requests = Array.Empty<PullRequest>();
            if (stocks == null)
                stocks = Array.Empty<StorageStock>();

            int count = stocks.Count;

            // The order in which a request walks the stocks: the given order, or nearest-first
            // when byDistance is set. A stable sort keeps the given order for equal distances.
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
                order[i] = i;
            if (byDistance)
                SortByDistance(order, stocks);

            // One shared working copy of the remaining amounts across the whole plan, so two
            // requests for the same item name draw from the same stocks without double-counting.
            // The leaveOne cap is clamped at zero: a zero- or single-item stock contributes nothing
            // rather than going negative.
            int[] remaining = new int[count];
            for (int i = 0; i < count; i++)
                remaining[i] = leaveOne ? Math.Max(0, stocks[i].Amount - 1) : stocks[i].Amount;

            var lines = new List<PullLine>();
            var missing = new List<string>();

            for (int r = 0; r < requests.Count; r++)
            {
                PullRequest request = requests[r];

                // A disallowed upgrader resource is skipped, not failed: the caller decides the
                // flag, so its absence from the plan is intentional rather than a shortfall.
                if (request.IsUpgraderResource && !allowUpgraderResources)
                    continue;

                int needed = request.Amount;

                for (int o = 0; o < order.Length && needed > 0; o++)
                {
                    int index = order[o];
                    if (!string.Equals(stocks[index].ItemName, request.ItemName, StringComparison.Ordinal))
                        continue;

                    int available = remaining[index];
                    int take = needed < available ? needed : available;
                    if (take <= 0)
                        continue;

                    remaining[index] -= take;
                    needed -= take;
                    lines.Add(new PullLine(stocks[index].SourceId, request.ItemName, take));
                }

                // A shortfall only fails the plan for a fixed-amount request. A fill asks for a
                // maximum, so what the walk found is the whole answer: the lines already added stand,
                // nothing is recorded missing and the plan stays feasible — even when it is empty.
                if (needed > 0 && !fillUpTo)
                    missing.Add(request.ItemName);
            }

            // All-or-nothing: a single shortfall discards the entire plan.
            if (missing.Count > 0)
                return new PullPlanResult(false, Array.Empty<PullLine>(), missing);

            return new PullPlanResult(true, lines, Array.Empty<string>());
        }

        /// <summary>
        /// Stable insertion sort of <paramref name="order"/> by ascending
        /// <see cref="StorageStock.Distance"/>, so equal distances keep the given order.
        /// </summary>
        private static void SortByDistance(int[] order, IReadOnlyList<StorageStock> stocks)
        {
            for (int i = 1; i < order.Length; i++)
            {
                int key = order[i];
                float keyDistance = stocks[key].Distance;
                int j = i - 1;
                while (j >= 0 && stocks[order[j]].Distance > keyDistance)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = key;
            }
        }
    }
}
