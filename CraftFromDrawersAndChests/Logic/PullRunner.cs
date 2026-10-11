using System;
using System.Collections.Generic;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The sequencing half of executing a pull: walking a plan's lines, reacting to each line's
    /// <c>Take</c> result, and enforcing the all-or-nothing rule. It is deliberately pure — the
    /// decide-and-take step is injected as a delegate — so the behaviour that decides whether a craft
    /// pays for itself can be driven from tests with a plain lambda, without a live <c>Player</c>,
    /// <c>Inventory</c> or storage. Lives in <c>Logic/</c> and must never reference
    /// <c>UnityEngine</c>, <c>Valheim</c>, <c>BepInEx</c> or <c>Jotunn</c>.
    /// <para>
    /// <b>Why this exists.</b> Vanilla creates the crafted item <i>before</i> it consumes the
    /// materials, so a pull that gives up on a line and lets the caller skip consumption hands the
    /// player a free item. A line must therefore only be declared satisfied when it really is, and a
    /// short-but-progressing delivery must be retried rather than treated as terminal.
    /// </para>
    /// </summary>
    public static class PullRunner
    {
        /// <summary>
        /// Runs a plan's <paramref name="lines"/> through <paramref name="take"/>, returning true only
        /// when <b>every</b> line was satisfied. The first line that cannot be is a failure and the
        /// walk stops; whatever earlier lines already delivered stays where it is (in practice the
        /// player's inventory, which is recoverable).
        /// <para>
        /// <see cref="IStorageSource.Take"/> has three answer shapes and all three are honoured:
        /// </para>
        /// <list type="bullet">
        /// <item><description><b>A full count</b> (<c>&gt;= amount</c>) — the line is satisfied.</description></item>
        /// <item><description><b>A short but positive count</b> — <b>not</b> terminal: the same source
        /// is asked again for the remainder, and this repeats while it keeps making progress. A source
        /// that instead returned 0 is exhausted, and the line fails. Because every retry asks for a
        /// strictly smaller remainder, the loop runs at most <c>line.Amount</c> times — the retry is
        /// bounded and a misbehaving source cannot spin.</description></item>
        /// <item><description><b><see cref="ChestTake.Deferred"/> (<c>-1</c>)</b> — treated as
        /// <b>satisfied</b>. The items have already left the storage and are on their way to the
        /// destination, so the line is paid for; failing it here is exactly what would create a free
        /// crafted item. The sentinel is never a count and is never summed, and it is never retried —
        /// the request is already in flight and a retry would duplicate it.</description></item>
        /// </list>
        /// </summary>
        /// <param name="lines">The plan's lines, in the order they should be secured; null means an empty plan.</param>
        /// <param name="take">
        /// The decide-and-take step: given a line and the amount still needed, it resolves the line's
        /// source, takes from it, and returns the source's result. It must never be asked for more than
        /// the line's amount, and it is called more than once for a line only when a delivery came back
        /// short but positive.
        /// </param>
        /// <param name="warn">Receives a human-readable diagnostic for each failure or deferred outcome; may be null.</param>
        public static bool Run(IReadOnlyList<PullLine> lines, Func<PullLine, int, int> take, Action<string> warn)
        {
            if (lines == null)
                return true;

            for (int i = 0; i < lines.Count; i++)
            {
                PullLine line = lines[i];
                if (line.Amount <= 0)
                    continue;

                int remaining = line.Amount;
                while (remaining > 0)
                {
                    int taken = take(line, remaining);

                    // Check the sentinel BEFORE any comparison: -1 is not a count, so it must never be
                    // summed or read as a shortfall. A routed removal is settled, not failed.
                    if (taken == ChestTake.Deferred)
                    {
                        if (warn != null)
                            warn("Pull: " + remaining + "x '" + line.ItemName +
                                 "' were routed to another player; treating the line as satisfied.");
                        break;
                    }

                    if (taken <= 0)
                    {
                        if (warn != null)
                            warn("Pull aborted: the source supplied " + (line.Amount - remaining) + " of " +
                                 line.Amount + "x '" + line.ItemName + "' and is now exhausted.");
                        return false;
                    }

                    // Positive, so remaining strictly decreases and the walking loop terminates.
                    remaining -= taken;
                }
            }
            return true;
        }
    }
}
