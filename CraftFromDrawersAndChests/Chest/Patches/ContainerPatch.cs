using System;
using System.Linq;
using CraftFromDrawersAndChests.Logic;
using HarmonyLib;
using UnityEngine;

namespace CraftFromDrawersAndChests.Chest.Patches {
    [HarmonyPatch]
    public static class ContainerPatch {
        public const string ItemMoveRPC = "MUC_RequestItemMove";
        public const string ItemMoveResponseRPC = "MUC_RequestItemMoveResponse";

        public const string ItemAddRPC = "MUC_RequestItemAdd";
        public const string ItemAddResponseRPC = "MUC_RequestItemAddResponse";

        public const string ItemRemoveRPC = "MUC_RequestItemRemove";
        public const string ItemRemoveResponseRPC = "MUC_RequestItemRemoveResponse";

        public const string ItemConsumeRPC = "MUC_RequestItemConsume";
        public const string ItemConsumeResponseRPC = "MUC_RequestItemConsumeResponse";

        public const string ItemDropRPC = "MUC_RequestItemDrop";
        public const string ItemDropResponseRPC = "MUC_RequestItemDropResponse";

        [HarmonyPatch(typeof(Container), nameof(Container.Awake)), HarmonyPostfix]
        public static void ContainerAwakePatch(Container __instance) {
            __instance.gameObject.AddComponent<ContainerExtend>();

            if (__instance.IgnoreInventory()) {
                return;
            }

            if (!__instance.m_nview) {
                __instance.m_nview = __instance.m_rootObjectOverride ? __instance.m_rootObjectOverride.GetComponent<ZNetView>() : __instance.GetComponent<ZNetView>();
            }
        }

        // This could maybe converted to a transpiler but is currently not worth it as the order of the statements have to be changed
        [HarmonyPatch(typeof(Container), nameof(Container.RPC_RequestOpen)), HarmonyPrefix]
        public static bool ContainerRPC_RequestOpenPatch(Container __instance, long uid, long playerID) {
            return HandleOpenRequest(__instance, uid, playerID, nameof(Container.RPC_OpenResponse), "open");
        }

        [HarmonyPatch(typeof(Container), nameof(Container.RPC_RequestStack)), HarmonyPrefix]
        public static bool ContainerRPC_RequestStackPatch(Container __instance, long uid, long playerID) {
            return HandleOpenRequest(__instance, uid, playerID, nameof(Container.RPC_StackResponse), "stack");
        }

        /// <summary>
        /// The four branches upstream wrote inline, now driven by <see cref="ChestOpenRules.Decide"/> so
        /// the truth table is testable, with one diagnostic line per request naming every input that
        /// decided it.
        /// <para>
        /// DIVERGENCE FROM UPSTREAM: the branch is chosen by the rule above rather than by a chain of
        /// early returns, and custody — "has a player claimed this container" — replaces upstream's in-use
        /// flag. Upstream moved ownership to every requester that was not currently using the container,
        /// which meant opening a chest changed which player could count it; see the 2.1.0 changelog entry.
        /// The inputs plus the chosen action are logged when this mod's Debug setting is on.
        /// </para>
        /// </summary>
        private static bool HandleOpenRequest(Container container, long uid, long playerID, string responseRpc, string kind) {
            ZNetView nview = container.m_nview;
            ZDO zdo = nview ? nview.GetZDO() : null;

            bool ignored = container.IgnoreInventory();
            bool isOwner = nview && nview.IsOwner();

            if (zdo == null) {
                // No ZDO means there is nothing to hand over and nothing to read custody from; let the
                // game answer rather than writing to a null.
                return true;
            }

            // Custody: a ZDO with an explicit owner belongs to that peer. Only a container nobody has
            // claimed (owner 0, which is how the server sees a fresh one) may be handed to the requester.
            bool containerClaimed = zdo.GetOwner() != 0L;

            // Access is only consulted on the path that could answer at all, exactly as upstream did:
            // the predicate returns Vanilla for an ignored or foreign instance before it looks at this.
            bool hasAccess = ignored || !isOwner || container.CheckAccess(playerID);

            bool containerInUse = container.IsInUse();
            bool wagonInUse = container.m_wagon && container.m_wagon.InUse();
            bool requesterIsLocal = playerID == ZNet.GetUID();

            ChestOpenAction action = ChestOpenRules.Decide(ignored, isOwner, hasAccess, containerClaimed);

            if (Log.IsDebug) {
                Log.LogDebug("[chest] " + kind + " request: requester=" + uid + " player=" + playerID +
                             " localUid=" + ZNet.GetUID() + " zdoOwner=" + zdo.GetOwner() +
                             " claimed=" + containerClaimed + " instanceIsOwner=" + isOwner +
                             " ignored=" + ignored + " hasAccess=" + hasAccess + " inUse=" + containerInUse +
                             " wagonInUse=" + wagonInUse + " inUseByOther=" +
                             ChestOpenRules.IsInUse(containerInUse, wagonInUse, requesterIsLocal) +
                             " requesterIsLocal=" + requesterIsLocal + " => " + action);
            }

            switch (action) {
                case ChestOpenAction.Vanilla:
                    return true;

                case ChestOpenAction.DenyAccess:
                    nview.InvokeRPC(uid, responseRpc, false);
                    return false;

                case ChestOpenAction.GrantInUse:
                    nview.InvokeRPC(uid, responseRpc, true);
                    return false;

                default:
                    ZDOMan.instance.ForceSendZDO(uid, zdo.m_uid);
                    zdo.SetOwner(uid);
                    nview.InvokeRPC(uid, responseRpc, true);
                    return false;
            }
        }
    }
}
