using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The pure decision behind spending ammunition out of an equipped Adventure Backpacks backpack
    /// without moving it into the player's inventory.
    /// <para>
    /// Vanilla spends a round with <c>Inventory.RemoveItem(ammoItem, 1)</c> on the player's own
    /// inventory, and that method **refuses** an item it does not contain
    /// (<c>Inventory.decompiled.cs:374-377</c> for the whole stack, <c>:391</c> for a partial one) — so
    /// redirecting the spend is what keeps a shot from being free. The redirect must fire only when the
    /// round really is in the backpack and not in the inventory: if the inventory held it, vanilla's own
    /// removal already handled it and spending again would take two rounds for one shot.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BackpackAmmoRulesTests
    {
        [Test]
        public void ShouldSpendFromBackpack_RoundOnlyInTheBackpack_IsTrue()
        {
            Assert.That(BackpackAmmoRules.ShouldSpendFromBackpack(inventoryContainsItem: false, backpackContainsItem: true),
                Is.True);
        }

        [Test]
        public void ShouldSpendFromBackpack_RoundInTheInventory_IsFalse()
        {
            // Vanilla's own removal covers it; spending again would consume two rounds for one shot.
            Assert.That(BackpackAmmoRules.ShouldSpendFromBackpack(inventoryContainsItem: true, backpackContainsItem: true),
                Is.False);
        }

        [Test]
        public void ShouldSpendFromBackpack_NotInTheBackpack_IsFalse()
        {
            // An unrelated removal (a dropped item, a chest) must never touch the backpack.
            Assert.That(BackpackAmmoRules.ShouldSpendFromBackpack(inventoryContainsItem: false, backpackContainsItem: false),
                Is.False);
            Assert.That(BackpackAmmoRules.ShouldSpendFromBackpack(inventoryContainsItem: true, backpackContainsItem: false),
                Is.False);
        }
    }
}
