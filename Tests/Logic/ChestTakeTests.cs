using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// Pins the pure decisions behind removing items from a vanilla chest through MultiUserChest
    /// (Task 11 fix). These are the parts that can be checked without the game: which chests can be
    /// served synchronously (owned by us, or unowned and about to be claimed) versus which would be
    /// routed to another player; whether a MultiUserChest answer was routed, applied inline or
    /// declined; and the return contract — a definite <c>&gt;= 0</c> count, or the <c>-1</c>
    /// <see cref="ChestTake.Deferred"/> sentinel that must never be confused with a real zero.
    /// <para>
    /// The game-facing part around these decisions — reading <c>ZNetView.HasOwner()</c>/<c>IsOwner()</c>
    /// and <c>ContainerHandler.RemoveItemFromChest</c>'s request — is not covered here; see the task
    /// report.
    /// </para>
    /// </summary>
    [TestFixture]
    public class ChestTakeTests
    {
        [Test]
        public void DeferredSentinel_IsMinusOne_AndDistinctFromZero()
        {
            // The whole point of the sentinel: a routed removal must not look like "nothing removed".
            Assert.That(ChestTake.Deferred, Is.EqualTo(-1));
            Assert.That(ChestTake.Deferred, Is.Not.EqualTo(0));
        }

        [Test]
        public void UnownedChest_CanBeServedSynchronously()
        {
            // Nobody owns it, so we claim it and the removal is local.
            Assert.That(ChestTake.CanServeSynchronously(hasOwner: false, isOwner: false), Is.True);
        }

        [Test]
        public void ChestOwnedByUs_CanBeServedSynchronously()
        {
            Assert.That(ChestTake.CanServeSynchronously(hasOwner: true, isOwner: true), Is.True);
        }

        [Test]
        public void ChestOwnedByAnotherPlayer_CannotBeServedSynchronously()
        {
            // Routed, never stolen: the caller must decline it before anything is sent.
            Assert.That(ChestTake.CanServeSynchronously(hasOwner: true, isOwner: false), Is.False);
        }

        [Test]
        public void NullRequest_IsNotRouted()
        {
            // MultiUserChest's inline path: applied and credited before it returned.
            Assert.That(ChestTake.WasRouted(requestIsNull: true, requestDragAmount: 0), Is.False);
        }

        [Test]
        public void DeclinedRequest_IsNotRouted()
        {
            // Non-null request with a zero drag amount: declined locally, nothing was sent.
            Assert.That(ChestTake.WasRouted(requestIsNull: false, requestDragAmount: 0), Is.False);
        }

        [Test]
        public void PositiveRequest_IsRouted()
        {
            Assert.That(ChestTake.WasRouted(requestIsNull: false, requestDragAmount: 5), Is.True);
        }

        [Test]
        public void RoutedResult_IsTheDeferredSentinel()
        {
            Assert.That(ChestTake.Result(routed: true, delivered: 0), Is.EqualTo(ChestTake.Deferred));
            Assert.That(ChestTake.Result(routed: true, delivered: 5), Is.EqualTo(ChestTake.Deferred));
        }

        [Test]
        public void LocalResult_IsTheDeliveredCount()
        {
            Assert.That(ChestTake.Result(routed: false, delivered: 3), Is.EqualTo(3));
            Assert.That(ChestTake.Result(routed: false, delivered: 0), Is.EqualTo(0));
        }

        [Test]
        public void NegativeDelta_IsClampedToZero_NotToTheSentinel()
        {
            Assert.That(ChestTake.Result(routed: false, delivered: -2), Is.EqualTo(0));
            Assert.That(ChestTake.Result(routed: false, delivered: -2), Is.Not.EqualTo(ChestTake.Deferred));
        }

        [Test]
        public void DeliveredNothing_WarnsOnlyForNonRoutedZero()
        {
            Assert.That(ChestTake.DeliveredNothing(routed: false, delivered: 0), Is.True);
            Assert.That(ChestTake.DeliveredNothing(routed: false, delivered: 4), Is.False);
            // A routed request delivered nothing *yet*, but the items are expected: no warning.
            Assert.That(ChestTake.DeliveredNothing(routed: true, delivered: 0), Is.False);
        }
    }
}
