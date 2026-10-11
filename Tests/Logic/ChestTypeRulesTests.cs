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
    }
}
