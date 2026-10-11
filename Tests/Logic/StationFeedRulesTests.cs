using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

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
    }
}
