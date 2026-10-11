using CraftFromDrawersAndChests.Logic;
using UnityEngine;

namespace CraftFromDrawersAndChests.Toggle
{
    /// <summary>
    /// The per-storage pull switch: one boolean on the storage's own ZDO that excludes it from every
    /// pull, for everyone. It is a world property that travels with the save, so whoever flips it flips
    /// it for the whole server — nothing here is local-only. It works unchanged for a chest and for a
    /// drawer, because both are <see cref="ZNetView"/>s with ZDOs.
    /// <para>
    /// This class owns the write (<see cref="Flip"/>), the key lookup (<see cref="IsToggleKeyPressed"/>)
    /// and the hover text (<see cref="HoverSuffix"/>). Reading the flag from a raw view
    /// (<see cref="IsOff"/>) is shared by the write and the hover text; the two <c>IStorageSource</c>
    /// implementations keep their own reads and are not plumbed through here. The key literal is not
    /// declared here: the hash is derived from <see cref="ConfigModel.ZdoKey"/>, the single source of
    /// truth Tasks 9 and 10's sources also read, so the string cannot drift.
    /// </para>
    /// </summary>
    public static class StorageToggle
    {
        /// <summary>The shared world off-switch key, hashed the same way the ZDO stores it.</summary>
        public static readonly int ZdoHash = ConfigModel.ZdoKey.GetStableHashCode();

        /// <summary>
        /// The current state of a storage's switch, read from its own ZDO. A view with no reachable
        /// ZDO is not switched off — the same default <c>false</c> the sources' reads use.
        /// </summary>
        public static bool IsOff(ZNetView view)
        {
            ZDO zdo = view != null ? view.GetZDO() : null;
            return zdo != null && zdo.GetBool(ZdoHash, false);
        }

        /// <summary>
        /// Flips the switch on <paramref name="view"/>. Ownership is claimed first: the switch is a ZDO
        /// value and the object must be ours before we write it, exactly as <c>DrawerSource</c> claims
        /// ownership before its drawer RPCs. The negated value is written, not a fixed one, so the call
        /// is a true toggle regardless of what the current state was. A null view or a view with no
        /// reachable ZDO is a no-op.
        /// </summary>
        public static void Flip(ZNetView view)
        {
            if (view == null)
                return;

            view.ClaimOwnership();

            ZDO zdo = view.GetZDO();
            if (zdo == null)
                return;

            zdo.Set(ZdoHash, !zdo.GetBool(ZdoHash, false));
        }

        /// <summary>
        /// The coloured suffix appended to a storage's hover text to show its switch state. The decision
        /// itself is pure (<see cref="ToggleRules.HoverSuffix"/>); this is the game-facing entry point.
        /// </summary>
        public static string HoverSuffix(bool off)
        {
            return ToggleRules.HoverSuffix(off);
        }

        /// <summary>
        /// The complete hover addition for a storage: its switch state and the key that flips it. Both
        /// halves are pure (<see cref="ToggleRules"/>); this is the game-facing entry point, reading the
        /// live snapshot. An empty string for a null view, so a caller can append unconditionally.
        /// <para>
        /// One entry point for both kinds of storage on purpose: a chest and a drawer produce their
        /// hover text through different methods (<c>Container.GetHoverText</c> and the drawer's own),
        /// and two call sites appending two strings each is how the drawer half came to show a state
        /// with no key while the chest half showed neither.
        /// </para>
        /// </summary>
        public static string HoverText(ZNetView view)
        {
            return view == null ? string.Empty : HoverSuffix(IsOff(view)) + ToggleHint();
        }

        /// <summary>
        /// The hint naming the configured toggle key, or stating that none is bound. The shipped default
        /// is empty, so the hint is what tells a player the switch exists but is unbound.
        /// </summary>
        public static string ToggleHint()
        {
            FeatureFlags flags = PluginConfig.Flags;
            return ToggleRules.ToggleHint(flags != null ? flags.ToggleStorageKey : null);
        }

        /// <summary>
        /// Whether the configured toggle key is pressed this frame while it is bound. The shipped
        /// default is empty, so the toggle is unbound out of the box and this returns false. An empty
        /// value must never reach <c>Input</c>, which throws on it, and an unrecognised name throws
        /// too, so the call is guarded and a bad value simply reads as "not pressed" — the same
        /// treatment CraftFromContainers gives its key checks.
        /// </summary>
        public static bool IsToggleKeyPressed()
        {
            FeatureFlags flags = PluginConfig.Flags;
            string key = flags != null ? flags.ToggleStorageKey : null;
            if (!ToggleRules.IsKeyConfigured(key))
                return false;

            try
            {
                return Input.GetKeyDown(key);
            }
            catch
            {
                return false;
            }
        }
    }
}
