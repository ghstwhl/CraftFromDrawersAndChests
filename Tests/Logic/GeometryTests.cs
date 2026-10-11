using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    [TestFixture]
    public class GeometryTests
    {
        [Test]
        public void SqrDistanceTo_IsThreeFourFiveSquared()
        {
            Assert.That(new Vec3(0, 0, 0).SqrDistanceTo(new Vec3(3, 4, 0)), Is.EqualTo(25f));
        }

        [Test]
        public void DistanceTo_IsFive()
        {
            Assert.That(new Vec3(0, 0, 0).DistanceTo(new Vec3(3, 4, 0)), Is.EqualTo(5f).Within(0.0001f));
        }
    }
}
