using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The pure decisions behind the ctrl-click pull key and its feedback text: whether the mod is
    /// allowed to act given the prevent key, whether a click becomes a pull at all, how the
    /// shortfall names are collapsed, the two messages the player sees, and the requirement
    /// readout's text.
    /// <para>
    /// The gate is a faithful port of CraftFromContainers 4.0.7's <c>AllowByKey()</c>:
    /// <c>preventKeyHeld ? switchPrevent : !switchPrevent</c>. With <c>switchPrevent</c> off (the
    /// default) the prevent key suppresses the mod; with it on the mod only acts while the prevent
    /// key is held. Nothing here touches Unity, Valheim, BepInEx or Jotunn, so the game-facing
    /// patch reads the live key state and feeds it in — the decision (and its tests) live here.
    /// </para>
    /// </summary>
    public static class PullKeyRules
    {
        /// <summary>
        /// Shown when a pull is refused because the player's inventory cannot take the shortfall.
        /// Listed as a constant so the message is pinned by a test rather than written inline in the
        /// patch.
        /// </summary>
        public const string NoRoomMessage = "Not enough room in your inventory to pull these materials.";

        /// <summary>
        /// Whether the mod is allowed to act at all this frame, from the prevent key
        /// (<c>"Prevent mod key"</c>) and the <c>"Switch prevent"</c> setting. Ported verbatim from
        /// CraftFromContainers 4.0.7's <c>AllowByKey()</c>:
        /// <c>preventKeyHeld ? switchPrevent : !switchPrevent</c>.
        /// <para>
        /// <c>switchPrevent</c> off (the shipped default): holding the prevent key suppresses the
        /// mod, and not holding it allows the mod. <c>switchPrevent</c> on: the roles invert — the
        /// mod only acts while the prevent key is held. The setting is a true "switch": it flips
        /// which of the two key states means "go", so a player can bind the prevent key to a key
        /// they already hold when they want the mod to be active.
        /// </para>
        /// </summary>
        /// <param name="switchPrevent">The resolved <c>"Switch prevent"</c> setting.</param>
        /// <param name="preventKeyHeld">Whether the configured prevent key is held this frame.</param>
        public static bool AllowByKey(bool switchPrevent, bool preventKeyHeld)
        {
            return preventKeyHeld ? switchPrevent : !switchPrevent;
        }

        /// <summary>
        /// Whether a recipe click becomes a pull: the pull key
        /// (<c>"Pull items key"</c>) must be held <b>and</b> <see cref="AllowByKey"/> must agree.
        /// When this is false the click is vanilla — the prefix lets the game craft.
        /// </summary>
        /// <param name="pullKeyHeld">Whether the configured pull key is held this frame.</param>
        /// <param name="preventKeyHeld">Whether the configured prevent key is held this frame.</param>
        /// <param name="switchPrevent">The resolved <c>"Switch prevent"</c> setting.</param>
        public static bool ShouldPull(bool pullKeyHeld, bool preventKeyHeld, bool switchPrevent)
        {
            return pullKeyHeld && AllowByKey(switchPrevent, preventKeyHeld);
        }

        /// <summary>
        /// The distinct, non-empty names in <paramref name="names"/>, in first-seen order. A recipe
        /// may name the same item in several requirements, so the planner's shortfall list can repeat
        /// a name once per unfilled request (Task 4's ruling); the message names each item once.
        /// Null input, null entries and empty entries are dropped, never thrown on, and the result is
        /// never null.
        /// </summary>
        public static string[] DistinctNames(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0)
                return Array.Empty<string>();

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var distinct = new List<string>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (!string.IsNullOrEmpty(name) && seen.Add(name))
                    distinct.Add(name);
            }
            return distinct.ToArray();
        }

        /// <summary>
        /// The failure message shown when storage could not supply the shortfall, naming the
        /// missing items once each. An empty (or all-empty) list yields a generic sentence rather
        /// than a dangling prefix, so the player always sees a reason.
        /// </summary>
        public static string MissingMessage(IReadOnlyList<string> missingItemNames)
        {
            string[] distinct = DistinctNames(missingItemNames);
            if (distinct.Length == 0)
                return "Not enough materials within range.";

            var builder = new StringBuilder("Not enough materials within range: ");
            for (int i = 0; i < distinct.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(distinct[i]);
            }
            builder.Append('.');
            return builder.ToString();
        }

        /// <summary>
        /// The requirement readout's text: the configured <c>"Resource string"</c>
        /// (<c>"{0}/{1}"</c> by default) formatted with the available and required amounts, exactly as
        /// CraftFromContainers writes it. An empty or whitespace-only setting falls back to the plain
        /// required amount, and a malformed format string — one whose braces do not balance or that
        /// names an argument the readout does not supply — is caught and falls back the same way rather
        /// than throwing inside the game's UI loop.
        /// </summary>
        /// <param name="resourceString">The configured format; null, empty or whitespace means "plain amount".</param>
        /// <param name="available">How much the player can draw on — their inventory plus covered storage.</param>
        /// <param name="required">How much the requirement needs.</param>
        public static string ResourceAmountText(string resourceString, int available, int required)
        {
            string plain = required.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(resourceString) || resourceString.Trim().Length == 0)
                return plain;

            try
            {
                return string.Format(CultureInfo.InvariantCulture, resourceString, available, required);
            }
            catch (FormatException)
            {
                return plain;
            }
        }
    }
}
