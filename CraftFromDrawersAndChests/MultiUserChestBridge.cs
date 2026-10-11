using System;
using System.Collections.Generic;
using BepInEx.Logging;
using MultiUserChest;

namespace CraftFromDrawersAndChests
{
    /// <summary>
    /// The one sanctioned way this mod removes an item from a vanilla <see cref="Container"/>:
    /// MultiUserChest's request/response protocol. A chest is never mutated by writing
    /// <c>Container.m_inventory</c>; that would desync against whichever player MultiUserChest has
    /// managing the chest.
    /// <para>
    /// <b>Ownership.</b> Two cases, and the difference matters:
    /// <list type="bullet">
    /// <item><description>A chest nobody manages has no owner to route a request to, so it is
    /// claimed first. <c>ZNetView.ClaimOwnership()</c> calls <c>ZDO.SetOwner(ZDOMan.GetSessionID())</c>
    /// synchronously, so <c>IsOwner()</c> is true as soon as it returns and MultiUserChest takes its
    /// local branch.</description></item>
    /// <item><description>A chest another player has open is left alone — claiming it would steal it
    /// from the player using it, exactly the desync the design forbids. The request is routed to that
    /// player's client instead.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Synchronicity.</b> In MultiUserChest 0.6.2, when this client owns the chest,
    /// <c>ContainerHandler.RemoveItemFromChest</c> applies the removal and the delivery inline and
    /// returns <c>null</c>; when it does not, the method sends a routed RPC and returns the request,
    /// and the owning client answers asynchronously. The count returned here is therefore the
    /// destination's own item delta, which is definite for the local branch and 0 for the routed one
    /// (see the remarks on <see cref="RemoveFromChest"/>).
    /// </para>
    /// </summary>
    public static class MultiUserChestBridge
    {
        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        /// <summary>
        /// Removes up to <paramref name="amount"/> of <paramref name="item"/> from
        /// <paramref name="chest"/> through MultiUserChest and delivers them into
        /// <paramref name="destination"/>, returning how many actually reached the destination.
        /// <para>
        /// When this client owns the chest (it owned it already, or it was claimed because nobody
        /// did), MultiUserChest applies the request and credits
        /// <paramref name="destination"/> inline; the return value is then the destination's matching
        /// item-count delta, which is the count that actually moved.
        /// </para>
        /// <para>
        /// When another player owns the chest, the request is routed to them and the outcome is
        /// applied asynchronously, so no synchronous count exists. Returning the requested amount
        /// would be an optimistic claim the all-or-nothing rule cannot trust, so this returns 0 for
        /// that case and logs that the result is deferred. The item still leaves storage exactly once
        /// (it is removed by the owning client), which is what the "two players on one chest" rule
        /// requires; only this client's ability to count it synchronously is lost.
        /// </para>
        /// </summary>
        /// <param name="chest">The chest to remove from. Its inventory is never written here.</param>
        /// <param name="item">The exact stack to remove from, located by the caller.</param>
        /// <param name="destination">Where the removed items are delivered.</param>
        /// <param name="amount">The maximum number of items to remove.</param>
        /// <returns>The number of items delivered into <paramref name="destination"/>.</returns>
        public static int RemoveFromChest(Container chest, ItemDrop.ItemData item, Inventory destination, int amount)
        {
            if (chest == null || item == null || item.m_shared == null || destination == null || amount <= 0)
                return 0;

            ZNetView view = chest.m_nview;
            if (view == null || !view.IsValid())
                return 0;

            // A chest nobody manages has nobody to route a request to: claim it so the removal is
            // applied locally and synchronously. A chest another player has open is not touched here;
            // stealing it would desync against the player using it.
            if (!view.HasOwner())
                view.ClaimOwnership();

            string sharedName = item.m_shared.m_name;
            int deliveredBefore = CountMatching(destination, sharedName);

            // (-1,-1) lets MultiUserChest choose where the items land; a specific slot only has meaning
            // for an interactive drag onto a slot. sender is unused by MultiUserChest's implementation,
            // but the local player is supplied for forward compatibility.
            Vector2i to = new Vector2i(-1, -1);
            ZDOID sender = Player.m_localPlayer != null ? Player.m_localPlayer.GetZDOID() : ZDOID.None;

            RequestChestRemove request =
                ContainerHandler.RemoveItemFromChest(chest, item, destination, to, sender, amount);

            // A null request is MultiUserChest's local branch: the removal and the delivery were
            // applied inline before it returned, so this delta is definite. A non-null request was
            // either routed to another client (dragAmount > 0) or declined (dragAmount == 0).
            int delivered = CountMatching(destination, sharedName) - deliveredBefore;
            if (delivered < 0)
                delivered = 0;

            if (request == null && delivered == 0)
            {
                Log.LogWarning("MultiUserChest delivered nothing into the destination for '" +
                               PrefabName(item) + "' (the chest may be empty or the destination full).");
            }
            else if (request != null && request.dragAmount > 0)
            {
                Log.LogWarning("Chest is managed by another player; routed a request for " +
                               request.dragAmount + "x '" + PrefabName(item) + "'. The owner applies " +
                               "it asynchronously, so the moved count cannot be reported here yet.");
            }

            return delivered;
        }

        /// <summary>
        /// The destination's total stack count of the item whose localized shared name is
        /// <paramref name="sharedName"/>. Counting by shared name (rather than prefab name) mirrors
        /// the very key <c>Inventory.AddItemToInventory</c> merges on, so the before/after difference
        /// is exactly the number of items the delivery placed.
        /// </summary>
        private static int CountMatching(Inventory inventory, string sharedName)
        {
            if (inventory == null || string.IsNullOrEmpty(sharedName))
                return 0;

            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            if (items == null)
                return 0;

            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData candidate = items[i];
                if (candidate == null || candidate.m_shared == null)
                    continue;

                if (string.Equals(candidate.m_shared.m_name, sharedName, StringComparison.Ordinal))
                    total += candidate.m_stack;
            }
            return total;
        }

        /// <summary>The item's prefab name when it has one, else its localized shared name, for logging.</summary>
        private static string PrefabName(ItemDrop.ItemData item)
        {
            if (item == null)
                return "<null>";
            if (item.m_dropPrefab != null)
                return item.m_dropPrefab.name;
            return item.m_shared != null ? item.m_shared.m_name : "<unknown>";
        }
    }
}
