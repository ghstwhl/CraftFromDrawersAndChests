using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;
using System.Collections.Generic;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The pure half of station feeding (Task 15a): how much a station can still take, and whether an
    /// item's prefab name is on a configured disallow list. The game-facing patches read the station's
    /// counts off the game and hand them to these; nothing here touches the game.
    /// </summary>
    [TestFixture]
    public class StationFeedRulesTests
    {
        // ---------------- IsDisallowedType ----------------

        [Test]
        public void IsDisallowedType_ExactPrefabName_Matches()
        {
            // CraftFromContainers' own default fuel list.
            Assert.That(StationFeedRules.IsDisallowedType("RoundLog,FineWood", "RoundLog"), Is.True);
            Assert.That(StationFeedRules.IsDisallowedType("RoundLog,FineWood", "FineWood"), Is.True);
        }

        [Test]
        public void IsDisallowedType_UnlistedName_DoesNotMatch()
        {
            Assert.That(StationFeedRules.IsDisallowedType("RoundLog,FineWood", "Wood"), Is.False);
        }

        [Test]
        public void IsDisallowedType_IsOrdinal_CaseSensitive()
        {
            // CraftFromContainers matches with Split(',').Contains(...), which is ordinal: a lowercase
            // entry must not match, so a server that mis-types a name is not silently protected.
            Assert.That(StationFeedRules.IsDisallowedType("roundlog", "RoundLog"), Is.False);
            Assert.That(StationFeedRules.IsDisallowedType("RoundLog", "roundlog"), Is.False);
        }

        [Test]
        public void IsDisallowedType_IsNotASubstringMatch()
        {
            // A list entry is a whole prefab name, not an affix: "Log" must not match "RoundLog".
            Assert.That(StationFeedRules.IsDisallowedType("Log", "RoundLog"), Is.False);
            Assert.That(StationFeedRules.IsDisallowedType("RoundLog", "Log"), Is.False);
        }

        [Test]
        public void IsDisallowedType_TrimsEntriesAndDropsEmpties()
        {
            // The list goes through ConfigModel.ParseList, the one parser this project pins.
            Assert.That(StationFeedRules.IsDisallowedType(" RoundLog , FineWood ", "RoundLog"), Is.True);
            Assert.That(StationFeedRules.IsDisallowedType(" RoundLog , FineWood ", "FineWood"), Is.True);
            Assert.That(StationFeedRules.IsDisallowedType(" , , ", "RoundLog"), Is.False);
        }

        [Test]
        public void IsDisallowedType_EmptyOrNullInput_DoesNotMatch()
        {
            Assert.That(StationFeedRules.IsDisallowedType(null, "RoundLog"), Is.False);
            Assert.That(StationFeedRules.IsDisallowedType(string.Empty, "RoundLog"), Is.False);
            Assert.That(StationFeedRules.IsDisallowedType("RoundLog,FineWood", null), Is.False);
            Assert.That(StationFeedRules.IsDisallowedType("RoundLog,FineWood", string.Empty), Is.False);
        }

        // ---------------- RemainingCapacity ----------------

        [Test]
        public void RemainingCapacity_CountsTheDifference()
        {
            // A smelter's ore queue: m_maxOre less GetQueueSize().
            Assert.That(StationFeedRules.RemainingCapacity(10, 3), Is.EqualTo(7));
        }

        [Test]
        public void RemainingCapacity_FullOrOver_FloorsAtZero()
        {
            Assert.That(StationFeedRules.RemainingCapacity(10, 10), Is.EqualTo(0));
            Assert.That(StationFeedRules.RemainingCapacity(10, 12), Is.EqualTo(0));
            Assert.That(StationFeedRules.RemainingCapacity(0, 0), Is.EqualTo(0));
        }

        // ---------------- RemainingFuelCapacity ----------------

        [Test]
        public void RemainingFuelCapacity_CountsWholeItemsOnVanillasBoundary()
        {
            // Vanilla calls a fire full when CeilToInt(fuel) >= m_maxFuel, and adds one fuel per item,
            // so the room left is that capacity less the ceiling of what is stored.
            Assert.That(StationFeedRules.RemainingFuelCapacity(5f, 0f), Is.EqualTo(5));
            Assert.That(StationFeedRules.RemainingFuelCapacity(5f, 4f), Is.EqualTo(1));
            Assert.That(StationFeedRules.RemainingFuelCapacity(5f, 0.5f), Is.EqualTo(4));
        }

        [Test]
        public void RemainingFuelCapacity_FullOrOver_FloorsAtZero()
        {
            Assert.That(StationFeedRules.RemainingFuelCapacity(5f, 5f), Is.EqualTo(0));
            Assert.That(StationFeedRules.RemainingFuelCapacity(5f, 5.5f), Is.EqualTo(0));
        }

        [Test]
        public void RemainingFuelCapacity_AgreesWithTheSmelterFullBoundary()
        {
            // CFC line 589 (and Smelter.OnAddFuel's own guard) call a smelter full at
            // GetFuel() > m_maxFuel - 1. The two must agree on the boundary or a station could be left
            // one fuel short: at 4 of 5 there is room, at 4.5 of 5 there is not.
            Assert.That(StationFeedRules.RemainingFuelCapacity(5f, 4f) > 0, Is.True);
            Assert.That(StationFeedRules.RemainingFuelCapacity(5f, 4.5f) > 0, Is.False);
        }

        // ---------------- FillAmount (Task 15b) ----------------

        [Test]
        public void FillAmount_WithoutTheModifier_IsAlwaysOne()
        {
            // CraftFromContainers' `pullAll ? ... : 1` (CFC lines 317, 503, 624): the released key
            // requests one item per interaction.
            Assert.That(StationFeedRules.FillAmount(false, 7), Is.EqualTo(1));
            Assert.That(StationFeedRules.FillAmount(false, 1), Is.EqualTo(1));
        }

        [Test]
        public void FillAmount_WithTheModifier_IsTheWholeRemainingCapacity()
        {
            Assert.That(StationFeedRules.FillAmount(true, 7), Is.EqualTo(7));
            Assert.That(StationFeedRules.FillAmount(true, 1), Is.EqualTo(1));
        }

        [Test]
        public void FillAmount_FullStation_AsksForNothingEitherWay()
        {
            // A station with no room is never fed, modifier or not, so a full station can never cost
            // the player stock that would sit unused in their inventory.
            Assert.That(StationFeedRules.FillAmount(false, 0), Is.EqualTo(0));
            Assert.That(StationFeedRules.FillAmount(true, 0), Is.EqualTo(0));
            Assert.That(StationFeedRules.FillAmount(true, -3), Is.EqualTo(0));
            Assert.That(StationFeedRules.FillAmount(false, -3), Is.EqualTo(0));
        }

        // ---------------- ShouldAttemptFeed (1.0.6) ----------------

        [Test]
        public void ShouldAttemptFeed_Released_DefersToVanillaWhenThePlayerCarriesAnInput()
        {
            // CraftFromContainers' `inventory.HaveItem(...) && !pullAll` (CFC lines 280/582): with the key
            // released, an input already in the inventory is vanilla's to consume, one per interaction.
            Assert.That(StationFeedRules.ShouldAttemptFeed(false, true), Is.False);
            Assert.That(StationFeedRules.ShouldAttemptFeed(false, false), Is.True);
        }

        [Test]
        public void ShouldAttemptFeed_WithTheModifier_IgnoresWhatThePlayerCarries()
        {
            // The regression this pins (2026-10-23): a blast furnace with flametal in the inventory
            // added one ore per press, because the "already carries an input" guard short-circuited
            // before the modifier was consulted.
            Assert.That(StationFeedRules.ShouldAttemptFeed(true, true), Is.True);
            Assert.That(StationFeedRules.ShouldAttemptFeed(true, false), Is.True);
        }

        // ---------------- ShouldStopInputWalk (1.0.7) ----------------

        [Test]
        public void ShouldStopInputWalk_StopsWhenTheDestinationHoldsTheInput()
        {
            Assert.That(StationFeedRules.ShouldStopInputWalk(true), Is.True);
        }

        [Test]
        public void ShouldStopInputWalk_DoesNotStopWhenTheDestinationHoldsNothing()
        {
            // The 1.0.6 regression: the walk stopped when the *pull* returned success, and a pull
            // storage has nothing to add to is a satisfied no-op. A smelter whose storage held tin
            // but no copper therefore never walked past copper, and nothing was ever fed.
            Assert.That(StationFeedRules.ShouldStopInputWalk(false), Is.False);
        }

        // ---------------- FeedSummary (1.0.8) ----------------

        [Test]
        public void FeedSummary_NothingAdded_IsNull()
        {
            Assert.That(StationFeedRules.FeedSummary(null), Is.Null);
            Assert.That(StationFeedRules.FeedSummary(new StationFeedRules.FedAmount[0]), Is.Null);
        }

        [Test]
        public void FeedSummary_OneOre_ReadsAsVanillasAddedLine()
        {
            string summary = StationFeedRules.FeedSummary(new[]
            {
                new StationFeedRules.FedAmount("Tin", 1)
            });

            Assert.That(summary, Is.EqualTo("Added 1 Tin"));
        }

        [Test]
        public void FeedSummary_SeveralOres_KeepTheGivenOrder()
        {
            // The order is the station's own conversion order, not the order storage handed them over.
            string summary = StationFeedRules.FeedSummary(new[]
            {
                new StationFeedRules.FedAmount("Scrap iron", 10),
                new StationFeedRules.FedAmount("Tin", 3),
                new StationFeedRules.FedAmount("Copper", 7)
            });

            Assert.That(summary, Is.EqualTo("Added 10 Scrap iron, 3 Tin, 7 Copper"));
        }

        [Test]
        public void FeedSummary_SkipsEntriesWithNoNameOrNoAmount()
        {
            string summary = StationFeedRules.FeedSummary(new[]
            {
                new StationFeedRules.FedAmount(null, 5),
                new StationFeedRules.FedAmount(string.Empty, 5),
                new StationFeedRules.FedAmount("Tin", 0),
                new StationFeedRules.FedAmount("Scrap iron", -2),
                new StationFeedRules.FedAmount("Copper", 4)
            });

            Assert.That(summary, Is.EqualTo("Added 4 Copper"));
        }

        [Test]
        public void FeedSummary_OnlyUnusableEntries_IsNull()
        {
            Assert.That(
                StationFeedRules.FeedSummary(new[]
                {
                    new StationFeedRules.FedAmount("", 3),
                    new StationFeedRules.FedAmount("Tin", 0)
                }),
                Is.Null);
        }

        // ---------------- OrderFed (1.0.9) ----------------

        [Test]
        public void OrderFed_NoTally_IsEmpty()
        {
            Assert.That(StationFeedRules.OrderFed(null, new[] { "CopperOre" }).Count, Is.EqualTo(0));
            Assert.That(
                StationFeedRules.OrderFed(new Dictionary<string, int>(), new[] { "CopperOre" }).Count,
                Is.EqualTo(0));
        }

        [Test]
        public void OrderFed_FourOreTypes_AreAllListedInTheStationsOrder()
        {
            // A smelter can convert copper, tin, iron and silver; one press can feed all four, and the
            // line must name all four. The tally's own order is deliberately not the station's.
            var fed = new Dictionary<string, int>
            {
                { "SilverOre", 1 },
                { "IronScrap", 1 },
                { "TinOre", 1 },
                { "CopperOre", 1 }
            };

            List<KeyValuePair<string, int>> ordered = StationFeedRules.OrderFed(
                fed, new[] { "CopperOre", "TinOre", "IronScrap", "SilverOre" });

            var prefabs = new List<string>();
            for (int i = 0; i < ordered.Count; i++)
                prefabs.Add(ordered[i].Key);

            Assert.That(prefabs, Is.EqualTo(new[] { "CopperOre", "TinOre", "IronScrap", "SilverOre" }));
        }

        [Test]
        public void OrderFed_NeverDropsATypeTheStationDoesNotName()
        {
            // Under-reporting is the failure that matters: a type that was fed but is missing from the
            // station's own conversion list must still appear, not vanish from the message.
            var fed = new Dictionary<string, int>
            {
                { "CopperOre", 1 },
                { "MysteryOre", 2 }
            };

            List<KeyValuePair<string, int>> ordered = StationFeedRules.OrderFed(fed, new[] { "CopperOre" });

            Assert.That(ordered.Count, Is.EqualTo(2));
            Assert.That(ordered[0].Key, Is.EqualTo("CopperOre"));
            Assert.That(ordered[1].Key, Is.EqualTo("MysteryOre"));
            Assert.That(ordered[1].Value, Is.EqualTo(2));
        }

        [Test]
        public void OrderFed_ATypeListedTwiceByTheStation_IsEmittedOnce()
        {
            var fed = new Dictionary<string, int> { { "CopperOre", 5 } };

            List<KeyValuePair<string, int>> ordered = StationFeedRules.OrderFed(
                fed, new[] { "CopperOre", "CopperOre" });

            Assert.That(ordered.Count, Is.EqualTo(1));
            Assert.That(ordered[0].Value, Is.EqualTo(5));
        }

        [Test]
        public void OrderFed_UnnamedOrEmptyTallyEntries_AreIgnored()
        {
            var fed = new Dictionary<string, int>
            {
                { "CopperOre", 0 },
                { string.Empty, 3 }
            };

            Assert.That(StationFeedRules.OrderFed(fed, new[] { "CopperOre" }).Count, Is.EqualTo(0));
        }

        [Test]
        public void OrderFed_ThenFeedSummary_ListsAllFourOreTypes()
        {
            // The owner's scenario: one copper, one tin, one iron and one silver in the inventory, one
            // press. The line must list all four, not stop at two.
            var fed = new Dictionary<string, int>
            {
                { "CopperOre", 1 },
                { "TinOre", 1 },
                { "IronScrap", 1 },
                { "SilverOre", 1 }
            };

            List<KeyValuePair<string, int>> ordered = StationFeedRules.OrderFed(
                fed, new[] { "CopperOre", "TinOre", "IronScrap", "SilverOre" });

            var amounts = new List<StationFeedRules.FedAmount>();
            for (int i = 0; i < ordered.Count; i++)
            {
                string display = ordered[i].Key == "SilverOre" ? "Silver" : ordered[i].Key;
                amounts.Add(new StationFeedRules.FedAmount(display, ordered[i].Value));
            }

            Assert.That(
                StationFeedRules.FeedSummary(amounts),
                Is.EqualTo("Added 1 CopperOre, 1 TinOre, 1 IronScrap, 1 Silver"));
        }

        // ---------------- FillAllHint (Task 15b) ----------------

        [Test]
        public void FillAllHint_UnboundModifier_HidesTheHint()
        {
            // CraftFromContainers only appends the hint when the key is bound
            // (`fillAllModKey.Value?.Length > 0`, CFC line 436).
            Assert.That(StationFeedRules.FillAllHint(null, "$piece_smelter_add"), Is.Null);
            Assert.That(StationFeedRules.FillAllHint(string.Empty, "$piece_smelter_add"), Is.Null);
        }

        [Test]
        public void FillAllHint_BoundModifier_MatchesCraftFromContainersMarkup()
        {
            // The exact shape CFC line 438 appends for the smelter's fuel hover, before localization.
            Assert.That(
                StationFeedRules.FillAllHint("left shift", "$piece_smelter_add"),
                Is.EqualTo("\n[<color=yellow><b>left shift+$KEY_Use</b></color>] $piece_smelter_add max"));
        }

        [Test]
        public void FillAllHint_UsesTheSuppliedActionToken()
        {
            // The ore hover passes the smelter's own m_addOreTooltip in place of the literal
            // (CFC line 453).
            Assert.That(
                StationFeedRules.FillAllHint("shift", "ore tooltip"),
                Is.EqualTo("\n[<color=yellow><b>shift+$KEY_Use</b></color>] ore tooltip max"));
        }
    }
}
