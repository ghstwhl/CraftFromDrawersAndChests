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
            Assert.That(ConfigModel.DefaultMaxRange, Is.EqualTo(4f));
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
    }
}
