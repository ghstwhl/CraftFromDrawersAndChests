using System;
using System.Collections.Generic;
using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// Pins the sequencing that executes a pull (Task 13 fix round 1). This is the behaviour the
    /// review found entirely untested, and where the Critical lived: vanilla creates the crafted item
    /// before it consumes the materials, so a line that is declared failed lets the caller skip
    /// consumption and hand out a free item. The decide-and-take step is a delegate, so all of it runs
    /// here with a plain lambda and no game types.
    /// <para>
    /// The three <see cref="IStorageSource.Take"/> answer shapes — a full/short count, zero, and the
    /// <see cref="ChestTake.Deferred"/> sentinel — are covered, together with the retry and the
    /// all-or-nothing outcome.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PullRunnerTests
    {
        private static PullLine Line(string sourceId, string itemName, int amount)
        {
            return new PullLine(sourceId, itemName, amount);
        }

        [Test]
        public void CountMeetsTheLine_Satisfies()
        {
            var lines = new List<PullLine> { Line("0", "Wood", 50) };
            int calls = 0;

            bool ok = PullRunner.Run(lines, (line, amount) => { calls++; return amount; }, null);

            Assert.That(ok, Is.True);
            Assert.That(calls, Is.EqualTo(1), "a full delivery needs no retry");
        }

        [Test]
        public void ShapeShortThenMet_RetriesForTheRemainder_AndSatisfies()
        {
            // The chest hands over one stack (30) first; the retry supplies the last 20. The Critical
            // was treating this first short answer as terminal.
            var lines = new List<PullLine> { Line("0", "Wood", 50) };
            var requested = new List<int>();
            var answers = new Queue<int>(new[] { 30, 20 });

            bool ok = PullRunner.Run(lines, (line, amount) => { requested.Add(amount); return answers.Dequeue(); }, null);

            Assert.That(ok, Is.True);
            Assert.That(requested, Is.EqualTo(new[] { 50, 20 }), "the retry asks only for the remainder");
        }

        [Test]
        public void ShapeZero_SourceExhausted_Fails()
        {
            var lines = new List<PullLine> { Line("0", "Wood", 50) };
            int calls = 0;

            // A short delivery is retried, but the next answer of 0 means the source is exhausted.
            bool ok = PullRunner.Run(lines, (line, amount) => { calls++; return calls == 1 ? 30 : 0; }, null);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void ShapeDeferred_IsTreatedAsSatisfied_AndNeverRetried()
        {
            // The items have already left the storage and are on their way; failing the line here is
            // exactly what would create a free crafted item, and retrying would duplicate them.
            var lines = new List<PullLine> { Line("0", "Wood", 50) };
            int calls = 0;

            bool ok = PullRunner.Run(lines, (line, amount) => { calls++; return ChestTake.Deferred; }, null);

            Assert.That(ok, Is.True);
            Assert.That(calls, Is.EqualTo(1), "a routed removal must not be retried");
        }

        [Test]
        public void ShapeDeferred_AfterAPartialDelivery_IsStillSatisfied()
        {
            var lines = new List<PullLine> { Line("0", "Wood", 50) };
            int calls = 0;

            bool ok = PullRunner.Run(lines, (line, amount) => { calls++; return calls == 1 ? 30 : ChestTake.Deferred; }, null);

            Assert.That(ok, Is.True);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void OneLineCannotBeMet_FailsTheWholeRun()
        {
            // All-or-nothing: line 0 is met (and its items really are taken), line 1 is exhausted, so
            // the run as a whole fails.
            var lines = new List<PullLine> { Line("0", "Wood", 10), Line("1", "Iron", 5) };
            var itemsTaken = new List<string>();

            bool ok = PullRunner.Run(
                lines,
                (line, amount) =>
                {
                    if (line.ItemName == "Iron")
                        return 0;
                    itemsTaken.Add(line.ItemName);
                    return amount;
                },
                null);

            Assert.That(ok, Is.False);
            Assert.That(itemsTaken, Is.EqualTo(new[] { "Wood" }), "the earlier line was taken before the failure");
        }

        [Test]
        public void GateAndExecutorAgree_ALineBiggerThanOneStack_IsRetriedAndSecured()
        {
            // Mirrors the reviewer's Critical from the gate's side: a source holding 2 x 30 (Count 60)
            // passes a 50-item gate (60 >= 50). Its Take can only hand over one stack per call, so the
            // planner's feasibility (R4) and the executor's outcome must still agree: the executor
            // retries across stacks (R1/R3) and secures the line the gate promised.
            var requests = new[] { new PullRequest("Wood", "Wood", 50, false) };
            var stocks = new[] { new StorageStock("0", "Wood", 60, 0f) };

            PullPlanResult plan = PullPlan.Plan(requests, stocks, false, false, true);
            Assert.That(plan.Feasible, Is.True, "the gate must see all 60 the source holds");
            Assert.That(plan.Lines.Count, Is.EqualTo(1));
            Assert.That(plan.Lines[0].Amount, Is.EqualTo(50));

            // A take step shaped exactly like the fixed chest source: one stack per call.
            int remaining = 60;
            const int maxStack = 30;
            int calls = 0;
            bool ok = PullRunner.Run(
                plan.Lines,
                (line, amount) =>
                {
                    calls++;
                    int chunk = Math.Min(Math.Min(amount, remaining), maxStack);
                    remaining -= chunk;
                    return chunk;
                },
                null);

            Assert.That(ok, Is.True, "the pull must secure what the gate promised");
            Assert.That(calls, Is.EqualTo(2), "a 50-of-60 request costs two single-stack removals");
        }

        [Test]
        public void EveryLineMet_Satisfies()
        {
            var lines = new List<PullLine> { Line("0", "Wood", 10), Line("1", "Iron", 5) };

            bool ok = PullRunner.Run(lines, (line, amount) => amount, null);

            Assert.That(ok, Is.True);
        }

        [Test]
        public void RetryIsBoundedByProgress_AndCannotSpin()
        {
            // A source that dribbles out one at a time still finishes: each positive answer strictly
            // shrinks the remainder, so a line of 5 costs exactly 5 calls and the loop cannot spin.
            var lines = new List<PullLine> { Line("0", "Wood", 5) };
            int calls = 0;

            bool ok = PullRunner.Run(lines, (line, amount) => { calls++; return 1; }, null);

            Assert.That(ok, Is.True);
            Assert.That(calls, Is.EqualTo(5));
        }

        [Test]
        public void NoLines_SatisfiesVacuously()
        {
            Assert.That(PullRunner.Run(new List<PullLine>(), (line, amount) => 0, null), Is.True);
            Assert.That(PullRunner.Run(null, (line, amount) => 0, null), Is.True);
        }
    }
}
