using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The <c>Debug</c> observation cadence. The rule throttles logging only — it never decides that a
    /// chest may be read, written, or asked about — so these tests pin nothing but the timing.
    /// </summary>
    [TestFixture]
    public class StorageRefreshRulesTests
    {
        private const float Interval = 10f;

        [Test]
        public void NeverSeen_IsDue()
        {
            Assert.That(StorageRefreshRules.IsDue(now: 0f, lastSeenAt: 0f, minIntervalSeconds: Interval), Is.True);
            Assert.That(StorageRefreshRules.IsDue(now: 25f, lastSeenAt: 0f, minIntervalSeconds: Interval), Is.True);
        }

        [Test]
        public void SeenRecently_IsNotDue()
        {
            Assert.That(StorageRefreshRules.IsDue(now: 100f, lastSeenAt: 95f, minIntervalSeconds: Interval), Is.False);
            Assert.That(StorageRefreshRules.IsDue(now: 100f, lastSeenAt: 90.5f, minIntervalSeconds: Interval), Is.False);
        }

        [Test]
        public void IntervalElapsed_IsDue()
        {
            Assert.That(StorageRefreshRules.IsDue(now: 100f, lastSeenAt: 90f, minIntervalSeconds: Interval), Is.True);
            Assert.That(StorageRefreshRules.IsDue(now: 100f, lastSeenAt: 80f, minIntervalSeconds: Interval), Is.True);
        }

        [Test]
        public void NonPositiveInterval_DisablesReporting()
        {
            // A zero interval would otherwise mean "log every frame" — the opposite of what a setting
            // like this should degrade to.
            Assert.That(StorageRefreshRules.IsDue(now: 100f, lastSeenAt: 0f, minIntervalSeconds: 0f), Is.False);
            Assert.That(StorageRefreshRules.IsDue(now: 100f, lastSeenAt: 0f, minIntervalSeconds: -1f), Is.False);
        }
    }
}
