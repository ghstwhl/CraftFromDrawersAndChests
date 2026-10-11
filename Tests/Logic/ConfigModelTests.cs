using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    [TestFixture]
    public class ConfigModelTests
    {
        [Test]
        public void ParseList_TrimsSplitsAndDropsEmpties()
        {
            // The exact case named in the brief.
            Assert.That(ConfigModel.ParseList("a, b ,,c"), Is.EqualTo(new[] { "a", "b", "c" }));

            // A whitespace-only entry is an empty entry: it is trimmed and then dropped.
            Assert.That(ConfigModel.ParseList(" x ,  , y "), Is.EqualTo(new[] { "x", "y" }));
        }

        [Test]
        public void ParseList_EmptyString_IsEmptyArray()
        {
            string[] result = ConfigModel.ParseList(string.Empty);
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseList_Null_IsEmptyArray()
        {
            string[] result = ConfigModel.ParseList(null);
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Defaults_MaxRangeIsFour()
        {
            // 4f, matching CFC's usable default.
            // CraftFromContainers 4.0.7's own default (CFC "ContainerRange", 10f), by the owner's ruling
            // in Task 15e: this project shipped 4f, which silently excluded storage a player arriving
            // from CFC expected to be usable. Pinned here so changing it can only be deliberate.
            Assert.That(ConfigModel.DefaultMaxRange, Is.EqualTo(10f));
        }

        [Test]
        public void Defaults_BothStorageKindsEnabled()
        {
            Assert.That(ConfigModel.DefaultEnableChests, Is.True);
            Assert.That(ConfigModel.DefaultEnableDrawers, Is.True);
        }

        [Test]
        public void Defaults_LeaveOneOffAndPullByDistanceOff()
        {
            Assert.That(ConfigModel.DefaultLeaveOne, Is.False);
            Assert.That(ConfigModel.DefaultPullByDistance, Is.False);
        }

        [Test]
        public void Defaults_PreventAndFillAllKeysAndSwitch()
        {
            // CFC 4.0.7's held-modifier defaults. switchPrevent is off, so out of the box the
            // prevent key means "holding it suppresses pulling", not "holding it enables it".
            Assert.That(ConfigModel.DefaultPreventModKey, Is.EqualTo("left alt"));
            Assert.That(ConfigModel.DefaultFillAllModKey, Is.EqualTo("left shift"));
            Assert.That(ConfigModel.DefaultSwitchPrevent, Is.False);
        }
    }
}
