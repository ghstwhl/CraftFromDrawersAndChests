using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The pure half of feeding a Plateautem: which item prefab one press of its interaction will
    /// consume, read from the totem's own fuel selection.
    /// <para>
    /// Facts this fixture encodes, read from Plateautem 0.4.3 (the build in play):
    /// <c>Interact</c> branches on <c>currentFuelItemIndex == -1</c> to <c>TakeStoneFromUser</c> and
    /// otherwise to <c>TakeFuelItemFromUser(currentFuelItemIndex)</c>; the stone prefab is the literal
    /// <c>Stone</c> (<c>stoneItemPrefabName</c>); the fuel list comes from the "Fuel Item List" config,
    /// default <c>Wood=1,Coal=2.5,Resin=5</c>, and each entry's <c>prefabName</c> is what the totem
    /// counts in the player's inventory. There is no third mode and no fallback: an index outside the
    /// list is a state this mod must not guess about, so it supplies nothing.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PlateautemFuelRulesTests
    {
        private static readonly string[] DefaultFuels = { "Wood", "Coal", "Resin" };

        [Test]
        public void PrefabToSupply_StoneMode_IsStone()
        {
            // -1 is the totem's stone mode, independent of the configured fuel list.
            Assert.That(PlateautemFuelRules.PrefabToSupply(-1, DefaultFuels), Is.EqualTo("Stone"));
            Assert.That(PlateautemFuelRules.PrefabToSupply(-1, null), Is.EqualTo("Stone"));
        }

        [Test]
        public void PrefabToSupply_SelectedFuel_IsThatPrefab()
        {
            Assert.That(PlateautemFuelRules.PrefabToSupply(0, DefaultFuels), Is.EqualTo("Wood"));
            Assert.That(PlateautemFuelRules.PrefabToSupply(1, DefaultFuels), Is.EqualTo("Coal"));
            Assert.That(PlateautemFuelRules.PrefabToSupply(2, DefaultFuels), Is.EqualTo("Resin"));
        }

        [Test]
        public void PrefabToSupply_IndexBeyondTheConfiguredList_SuppliesNothing()
        {
            // A server can shrink "Fuel Item List" while a client still has a selection from the old
            // list. There is no item to ask storage for, and inventing one would pull the wrong fuel.
            Assert.That(PlateautemFuelRules.PrefabToSupply(3, DefaultFuels), Is.Null);
            Assert.That(PlateautemFuelRules.PrefabToSupply(0, null), Is.Null);
            Assert.That(PlateautemFuelRules.PrefabToSupply(0, new string[0]), Is.Null);
        }

        [Test]
        public void PrefabToSupply_BlankEntry_SuppliesNothing()
        {
            // A malformed "Fuel Item List" entry parses to an empty prefab name.
            Assert.That(PlateautemFuelRules.PrefabToSupply(0, new[] { string.Empty }), Is.Null);
            Assert.That(PlateautemFuelRules.PrefabToSupply(0, new string[] { null }), Is.Null);
        }

        // ---------------- UnitsInRoom / UnitsToRequest ----------------
        //
        // The totem's own arithmetic, from Plateautem 0.4.3: a hold press takes
        // `min(roomInUnits, whatThePlayerHolds)` where the fuel room is
        // `floor((configMaximumFuel - GetTotalFuelStored()) / fuelValue)` and the stone room is
        // `floor(configMaximumStone - currentStoneStored)`. A single press takes one.
        // Pulling one per tick is why a hold only fed one item at a time (reported 2026-11-22).

        [Test]
        public void UnitsInRoom_OneUnitPerItem_FillsToCapacity()
        {
            Assert.That(PlateautemFuelRules.UnitsInRoom(250f, 0f, 1f), Is.EqualTo(250));
            Assert.That(PlateautemFuelRules.UnitsInRoom(250f, 249f, 1f), Is.EqualTo(1));
        }

        [Test]
        public void UnitsInRoom_WeightsByTheItemsFuelValue()
        {
            // Coal is worth 2.5: 150 units of room holds 60 coal, and 10 left holds 4.
            Assert.That(PlateautemFuelRules.UnitsInRoom(250f, 100f, 2.5f), Is.EqualTo(60));
            Assert.That(PlateautemFuelRules.UnitsInRoom(250f, 240f, 2.5f), Is.EqualTo(4));
        }

        [Test]
        public void UnitsInRoom_FullOrOverfull_IsZero()
        {
            Assert.That(PlateautemFuelRules.UnitsInRoom(250f, 250f, 1f), Is.EqualTo(0));
            Assert.That(PlateautemFuelRules.UnitsInRoom(250f, 300f, 1f), Is.EqualTo(0));
        }

        [Test]
        public void UnitsInRoom_NonPositiveFuelValue_IsZero()
        {
            // A "Wood=0" entry in the fuel list would otherwise divide by zero (or produce nonsense);
            // nothing is pulled for an item the totem gives no fuel value for.
            Assert.That(PlateautemFuelRules.UnitsInRoom(250f, 0f, 0f), Is.EqualTo(0));
            Assert.That(PlateautemFuelRules.UnitsInRoom(250f, 0f, -1f), Is.EqualTo(0));
        }

        [Test]
        public void UnitsToRequest_SinglePress_IsOne()
        {
            Assert.That(PlateautemFuelRules.UnitsToRequest(false, 250), Is.EqualTo(1));
            Assert.That(PlateautemFuelRules.UnitsToRequest(false, 1), Is.EqualTo(1));
        }

        [Test]
        public void UnitsToRequest_Hold_IsTheWholeRemainingRoom()
        {
            Assert.That(PlateautemFuelRules.UnitsToRequest(true, 250), Is.EqualTo(250));
            Assert.That(PlateautemFuelRules.UnitsToRequest(true, 1), Is.EqualTo(1));
        }

        [Test]
        public void UnitsToRequest_NoRoom_RequestsNothing()
        {
            // The totem is full: pull nothing and let its own "$msg_cantaddmore" answer the press.
            Assert.That(PlateautemFuelRules.UnitsToRequest(false, 0), Is.EqualTo(0));
            Assert.That(PlateautemFuelRules.UnitsToRequest(true, 0), Is.EqualTo(0));
        }
    }
}
