using System.Collections.Generic;
using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    [TestFixture]
    public class PullPlanTests
    {
        private static PullRequest Req(string itemName, int amount, bool upgrader = false)
        {
            return new PullRequest(itemName, itemName, amount, upgrader);
        }

        private static StorageStock Stock(string sourceId, string itemName, int amount, float distance = 0f)
        {
            return new StorageStock(sourceId, itemName, amount, distance);
        }

        private static PullPlanResult Plan(
            PullRequest[] requests,
            StorageStock[] stocks,
            bool leaveOne = false,
            bool byDistance = false,
            bool allowUpgraderResources = true)
        {
            return PullPlan.Plan(requests, stocks, leaveOne, byDistance, allowUpgraderResources);
        }

        /// <summary>Total amount every line draws from one source; proves a stock is never over-drawn.</summary>
        private static int SumFor(PullPlanResult result, string sourceId)
        {
            int total = 0;
            foreach (PullLine line in result.Lines)
            {
                if (line.SourceId == sourceId)
                    total += line.Amount;
            }
            return total;
        }

        [Test]
        public void RequirementExactlyMet_CreatesOneLine()
        {
            var result = Plan(new[] { Req("Wood", 5) }, new[] { Stock("c1", "Wood", 5) });

            Assert.That(result.Feasible, Is.True);
            Assert.That(result.Lines, Has.Count.EqualTo(1));
            Assert.That(result.Lines[0].SourceId, Is.EqualTo("c1"));
            Assert.That(result.Lines[0].ItemName, Is.EqualTo("Wood"));
            Assert.That(result.Lines[0].Amount, Is.EqualTo(5));
            Assert.That(result.MissingItemNames, Is.Empty);
        }

        [Test]
        public void RequirementSpreadAcrossSources_SplitsIntoLines()
        {
            var result = Plan(
                new[] { Req("Wood", 5) },
                new[] { Stock("c1", "Wood", 2), Stock("c2", "Wood", 3) });

            Assert.That(result.Feasible, Is.True);
            Assert.That(result.Lines, Has.Count.EqualTo(2));
            Assert.That(SumFor(result, "c1"), Is.EqualTo(2));
            Assert.That(SumFor(result, "c2"), Is.EqualTo(3));
            Assert.That(result.MissingItemNames, Is.Empty);
        }

        [Test]
        public void RequirementShortByOne_IsInfeasibleAndHasNoLines()
        {
            // Review Focus 1: a requirement larger than everything available must consume nothing.
            var result = Plan(new[] { Req("Wood", 5) }, new[] { Stock("c1", "Wood", 4) });

            Assert.That(result.Feasible, Is.False);
            Assert.That(result.Lines, Is.Empty);
            Assert.That(result.MissingItemNames, Is.EqualTo(new[] { "Wood" }));
        }

        [Test]
        public void InfeasiblePlan_ConsumesNothing()
        {
            // Wood is fully covered, Stone is short. Because the plan is all-or-nothing,
            // even the satisfiable Wood request yields no line.
            var result = Plan(
                new[] { Req("Wood", 2), Req("Stone", 5) },
                new[] { Stock("c1", "Wood", 2), Stock("c1", "Stone", 3) });

            Assert.That(result.Feasible, Is.False);
            Assert.That(result.Lines, Is.Empty);
            Assert.That(result.MissingItemNames, Is.EqualTo(new[] { "Stone" }));
        }

        [Test]
        public void LeaveOne_SkipsALastItemWhenOthersCanCover()
        {
            // c1 holds a single Wood: leaveOne makes it untouchable, and c2 covers the request.
            var result = Plan(
                new[] { Req("Wood", 2) },
                new[] { Stock("c1", "Wood", 1), Stock("c2", "Wood", 3) },
                leaveOne: true);

            Assert.That(result.Feasible, Is.True);
            Assert.That(SumFor(result, "c1"), Is.EqualTo(0));
            Assert.That(SumFor(result, "c2"), Is.EqualTo(2));
            Assert.That(result.MissingItemNames, Is.Empty);
        }

        [Test]
        public void LeaveOne_CannotBeSatisfied_IsInfeasible()
        {
            // All-or-nothing beats leaveOne: a lone item cannot be taken, so nothing is.
            var result = Plan(
                new[] { Req("Wood", 1) },
                new[] { Stock("c1", "Wood", 1) },
                leaveOne: true);

            Assert.That(result.Feasible, Is.False);
            Assert.That(result.Lines, Is.Empty);
            Assert.That(result.MissingItemNames, Is.EqualTo(new[] { "Wood" }));
        }

        [Test]
        public void ByDistance_OrdersLinesNearestFirst()
        {
            var result = Plan(
                new[] { Req("Wood", 2) },
                new[] { Stock("far", "Wood", 1, distance: 10f), Stock("near", "Wood", 1, distance: 1f) },
                byDistance: true);

            Assert.That(result.Feasible, Is.True);
            Assert.That(result.Lines, Has.Count.EqualTo(2));
            Assert.That(result.Lines[0].SourceId, Is.EqualTo("near"));
            Assert.That(result.Lines[1].SourceId, Is.EqualTo("far"));
        }

        [Test]
        public void TwoRequestsForSameItemName_ShareSources()
        {
            // Both requests draw from the same stocks; no stock ever contributes more than it holds.
            var result = Plan(
                new[] { Req("Wood", 3), Req("Wood", 1) },
                new[] { Stock("a", "Wood", 2), Stock("b", "Wood", 3) });

            Assert.That(result.Feasible, Is.True);
            Assert.That(SumFor(result, "a"), Is.EqualTo(2));   // exactly what "a" holds
            Assert.That(SumFor(result, "b"), Is.EqualTo(2));   // draws 1 + 1, never over "b"'s 3
            Assert.That(SumFor(result, "a") + SumFor(result, "b"), Is.EqualTo(4));
        }

        [Test]
        public void UpgraderResource_IsSkippedWhenDisallowed()
        {
            // Flint is an upgrader resource and there is no stock for it; when disallowed it is
            // skipped entirely, so it is neither pulled nor reported missing.
            var result = Plan(
                new[] { Req("Coal", 1), Req("Flint", 1, upgrader: true) },
                new[] { Stock("c1", "Coal", 1) },
                allowUpgraderResources: false);

            Assert.That(result.Feasible, Is.True);
            Assert.That(result.Lines, Has.Count.EqualTo(1));
            Assert.That(result.Lines[0].ItemName, Is.EqualTo("Coal"));
            Assert.That(result.MissingItemNames, Is.Empty);
        }
    }
}
