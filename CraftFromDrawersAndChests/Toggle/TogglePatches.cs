using System;
using System.Reflection;
using CraftFromDrawersAndChests.Storage;
using HarmonyLib;
using UnityEngine;

namespace CraftFromDrawersAndChests.Toggle
{
    /// <summary>
    /// The game-facing half of the per-storage switch: the hotkey that flips it and the hover line that
    /// shows it. Three patches hang off this class, all attributed, so <c>Plugin.Awake</c>'s
    /// assembly-wide <c>Harmony.PatchAll()</c> registers them with no extra call.
    /// <para>
    /// <b>Where a hover text comes from.</b> The game's crosshair reads the hover text through the
    /// <c>Hoverable</c> interface: <c>Hud.UpdateCrosshair</c> calls
    /// <c>GetComponentInParent&lt;Hoverable&gt;()</c> and then <c>Hoverable.GetHoverText()</c>. Both a
    /// vanilla <c>Container</c> and a KG Item Drawers <c>DrawerComponent</c> implement that interface
    /// (<c>Container : MonoBehaviour, Hoverable, Interactable</c>;
    /// <c>DrawerComponent : MonoBehaviour, Interactable, Hoverable</c>), so the drawer's text is
    /// produced by its own <c>DrawerComponent.GetHoverText()</c>, not by a <c>Container</c> method and
    /// not by <c>Player.Update</c>. Because <c>Hoverable</c> is an interface, there is no single method
    /// to patch that covers both: a chest is covered by a postfix on <c>Container.GetHoverText</c>, and
    /// a drawer by a postfix on the drawer's own method, applied at runtime because Item Drawers is a
    /// soft dependency with no compile-time type here.
    /// </para>
    /// </summary>
    internal static class TogglePatches
    {
        /// <summary>
        /// Appends the switch state to a chest's hover text. A postfix, so the game has already built
        /// the full text; the suffix is only added when the hovered object really is a storage.
        /// </summary>
        [HarmonyPatch(typeof(Container), "GetHoverText")]
        [HarmonyPostfix]
        private static void ContainerGetHoverTextPostfix(Container __instance, ref string __result)
        {
            AppendState(VanillaChestSource.ViewOf(__instance), ref __result);
        }

        /// <summary>
        /// The hotkey. A postfix on <c>Player.Update</c>, which is what calls <c>Player.UpdateHover</c>:
        /// running after it means <see cref="Player.GetHoverObject"/> already holds this frame's hover,
        /// where a prefix would read the previous frame's. The local player only — a remote player's
        /// <c>Update</c> must never flip anything on this client — and the key check never throws, so an
        /// unbound (empty) or unrecognised key value simply does nothing.
        /// </summary>
        [HarmonyPatch(typeof(Player), "Update")]
        [HarmonyPostfix]
        private static void PlayerUpdatePostfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer)
                return;

            if (!StorageToggle.IsToggleKeyPressed())
                return;

            ZNetView view = HoveredStorageView();
            if (view != null)
                StorageToggle.Flip(view);
        }

        /// <summary>
        /// The ZNetView of whatever storage the local player is hovering, or null. A chest is resolved
        /// through <see cref="VanillaChestSource.ViewOf"/> — the same root-object-override lookup
        /// <c>Container.Awake</c> uses — and anything else must be a drawer
        /// (<see cref="DrawerApi.IsDrawer"/>) before it is returned, so a door or a random piece is
        /// never treated as storage.
        /// </summary>
        private static ZNetView HoveredStorageView()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return null;

            GameObject hovered = player.GetHoverObject();
            if (hovered == null)
                return null;

            Container container = hovered.GetComponentInParent<Container>();
            if (container != null)
                return VanillaChestSource.ViewOf(container);

            ZNetView view = hovered.GetComponentInParent<ZNetView>();
            return view != null && DrawerApi.IsDrawer(view) ? view : null;
        }

        /// <summary>Appends the storage's switch state to its hover text; a null view adds nothing.</summary>
        private static void AppendState(ZNetView view, ref string text)
        {
            if (view == null)
                return;

            text += StorageToggle.HoverSuffix(StorageToggle.IsOff(view));
        }
    }

    /// <summary>
    /// Appends the switch state to a drawer's hover text. Separate from <see cref="TogglePatches"/>
    /// because the target lives in a soft-dependent assembly: <see cref="Prepare"/> reports whether
    /// Item Drawers (and so the target type) is present, and HarmonyX then skips the whole class when
    /// it is not, so no drawer patch is ever attempted without the mod.
    /// </summary>
    [HarmonyPatch]
    internal static class DrawerHoverTextPatch
    {
        /// <summary>The drawer component's full type name, resolved at runtime because Item Drawers is a soft dependency.</summary>
        private const string DrawerComponentFullName = "kg_ItemDrawers.DrawerComponent";

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return DrawerComponentType() != null;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            Type type = DrawerComponentType();
            return type != null ? AccessTools.Method(type, "GetHoverText") : null;
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance, ref string __result)
        {
            var component = __instance as Component;
            ZNetView view = component != null ? component.GetComponent<ZNetView>() : null;
            if (view == null || !DrawerApi.IsDrawer(view))
                return;

            __result += StorageToggle.HoverSuffix(StorageToggle.IsOff(view));
        }

        private static Type DrawerComponentType()
        {
            return AccessTools.TypeByName(DrawerComponentFullName);
        }
    }
}
