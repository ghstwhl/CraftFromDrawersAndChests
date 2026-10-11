using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// Pins the pure "how many of this item does this drawer contribute" decision (Task 9). This is
    /// the part of <c>DrawerSource.Count</c> that can be checked without a live ZDO: storage is
    /// keyed by prefab name, and a drawer reporting a prefab with a zero amount contributes nothing
    /// (Review Focus 4).
    /// </summary>
    [TestFixture]
    public class DrawerStockTests
    {
        [Test]
        public void MatchingPrefabWithStock_ContributesItsAmount()
        {
            Assert.That(DrawerStock.CountFor("Wood", 5, "Wood"), Is.EqualTo(5));
        }

        [Test]
        public void MatchingPrefabButZeroAmount_ContributesNothing()
        {
            // Review Focus 4: a drawer whose ZDO reports a prefab but a zero amount contributes
            // nothing and throws nothing.
            Assert.That(DrawerStock.CountFor("Wood", 0, "Wood"), Is.EqualTo(0));
        }

        [Test]
        public void DifferentPrefab_ContributesNothing()
        {
            Assert.That(DrawerStock.CountFor("Wood", 5, "Stone"), Is.EqualTo(0));
        }

        [Test]
        public void NegativeAmount_ContributesNothing()
        {
            Assert.That(DrawerStock.CountFor("Wood", -3, "Wood"), Is.EqualTo(0));
        }

        [Test]
        public void EmptyDrawerPrefab_ContributesNothing()
        {
            Assert.That(DrawerStock.CountFor("", 5, "Wood"), Is.EqualTo(0));
        }

        [Test]
        public void NullItemName_ContributesNothing()
        {
            Assert.That(DrawerStock.CountFor("Wood", 5, null), Is.EqualTo(0));
        }

        [Test]
        public void PrefabMatchIsOrdinalAndCaseSensitive()
        {
            // Prefab names are exact: "wood" is a different prefab from "Wood".
            Assert.That(DrawerStock.CountFor("Wood", 5, "wood"), Is.EqualTo(0));
        }
    }
}
