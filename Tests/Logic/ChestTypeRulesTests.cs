using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    [TestFixture]
    public class ChestTypeRulesTests
    {
        [Test]
        public void WoodChest_IsExcludedWhenRuleOn()
        {
            var r = new ChestTypeRules { IgnoreWoodChests = true };
            Assert.That(r.IsExcluded("piece_chest_wood(Clone)", false, false), Is.True);
        }

        [Test]
        public void WoodChest_IsKeptWhenRuleOff()
        {
            var r = new ChestTypeRules();
            Assert.That(r.IsExcluded("piece_chest_wood(Clone)", false, false), Is.False);
        }

        [Test]
        public void PrivateChest_IsExcludedWhenRuleOn()
        {
            var r = new ChestTypeRules { IgnorePrivateChests = true };
            Assert.That(r.IsExcluded("piece_chest_private(Clone)", false, false), Is.True);
        }

        [Test]
        public void PrivateChest_IsKeptWhenRuleOff()
        {
            var r = new ChestTypeRules();
            Assert.That(r.IsExcluded("piece_chest_private(Clone)", false, false), Is.False);
        }

        [Test]
        public void BlackMetalChest_IsExcludedWhenRuleOn()
        {
            var r = new ChestTypeRules { IgnoreBlackMetalChests = true };
            Assert.That(r.IsExcluded("piece_chest_blackmetal(Clone)", false, false), Is.True);
        }

        [Test]
        public void BlackMetalChest_IsKeptWhenRuleOff()
        {
            var r = new ChestTypeRules();
            Assert.That(r.IsExcluded("piece_chest_blackmetal(Clone)", false, false), Is.False);
        }

        [Test]
        public void ReinforcedChest_IsExcludedWhenRuleOn()
        {
            var r = new ChestTypeRules { IgnoreReinforcedChests = true };
            Assert.That(r.IsExcluded("piece_chest(Clone)", false, false), Is.True);
        }

        [Test]
        public void ReinforcedRule_DoesNotMatchWoodChest()
        {
            var r = new ChestTypeRules { IgnoreReinforcedChests = true };
            Assert.That(r.IsExcluded("piece_chest_wood(Clone)", false, false), Is.False);
        }

        [Test]
        public void ReinforcedRule_DoesNotMatchPrivateChest()
        {
            var r = new ChestTypeRules { IgnoreReinforcedChests = true };
            Assert.That(r.IsExcluded("piece_chest_private(Clone)", false, false), Is.False);
        }

        [Test]
        public void ReinforcedRule_DoesNotMatchBlackMetalChest()
        {
            var r = new ChestTypeRules { IgnoreReinforcedChests = true };
            Assert.That(r.IsExcluded("piece_chest_blackmetal(Clone)", false, false), Is.False);
        }

        [Test]
        public void ShipContainer_IsExcludedWhenParentIsShip()
        {
            var r = new ChestTypeRules { IgnoreShipContainers = true };
            Assert.That(r.IsExcluded("piece_chest(Clone)", true, false), Is.True);
        }

        [Test]
        public void ShipContainer_IsKeptWhenRuleOff()
        {
            var r = new ChestTypeRules();
            Assert.That(r.IsExcluded("piece_chest(Clone)", true, false), Is.False);
        }

        [Test]
        public void WagonContainer_IsExcludedWhenParentIsWagon()
        {
            var r = new ChestTypeRules { IgnoreWagonContainers = true };
            Assert.That(r.IsExcluded("piece_chest(Clone)", false, true), Is.True);
        }

        [Test]
        public void WagonContainer_IsKeptWhenRuleOff()
        {
            var r = new ChestTypeRules();
            Assert.That(r.IsExcluded("piece_chest(Clone)", false, true), Is.False);
        }

        /// <summary>
        /// <see cref="ChestTypeRules.FromFlags"/> must map each of the six <c>FeatureFlags</c> ignore
        /// settings to its own field. Each case sets exactly one flag and asserts all six fields, so a
        /// dropped field, a swapped source flag or a copy-paste duplicate fails the case for the two
        /// flags involved rather than silently disabling a chest-type rule.
        /// </summary>
        [TestCase("ship")]
        [TestCase("wagon")]
        [TestCase("wood")]
        [TestCase("private")]
        [TestCase("blackmetal")]
        [TestCase("reinforced")]
        public void FromFlags_MapsEachIgnoreFlagToItsField(string enabled)
        {
            var flags = new FeatureFlags();
            switch (enabled)
            {
                case "ship": flags.IgnoreShipContainers = true; break;
                case "wagon": flags.IgnoreWagonContainers = true; break;
                case "wood": flags.IgnoreWoodChests = true; break;
                case "private": flags.IgnorePrivateChests = true; break;
                case "blackmetal": flags.IgnoreBlackMetalChests = true; break;
                case "reinforced": flags.IgnoreReinforcedChests = true; break;
            }

            ChestTypeRules r = ChestTypeRules.FromFlags(flags);

            Assert.That(r.IgnoreShipContainers, Is.EqualTo(enabled == "ship"));
            Assert.That(r.IgnoreWagonContainers, Is.EqualTo(enabled == "wagon"));
            Assert.That(r.IgnoreWoodChests, Is.EqualTo(enabled == "wood"));
            Assert.That(r.IgnorePrivateChests, Is.EqualTo(enabled == "private"));
            Assert.That(r.IgnoreBlackMetalChests, Is.EqualTo(enabled == "blackmetal"));
            Assert.That(r.IgnoreReinforcedChests, Is.EqualTo(enabled == "reinforced"));
        }
    }
}
