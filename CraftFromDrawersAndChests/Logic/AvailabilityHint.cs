using System;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// Why a storage that holds a requirement's item cannot be used, as flags so a requirement can
    /// report every reason at once (one drawer switched off <i>and</i> one locked chest).
    /// <para>
    /// Deliberately narrower than "why is this not a pull source": only the two states a player would
    /// call a lock are represented. A storage that is out of range, a loose container, an ignored
    /// chest type or a disabled storage kind is not <i>locked</i> — it was never a candidate — and
    /// reporting it would turn "you have none nearby" into noise.
    /// </para>
    /// </summary>
    [Flags]
    public enum StorageBlockReasons
    {
        /// <summary>Nothing in range is blocked; there is nothing to explain.</summary>
        None = 0,

        /// <summary>The player switched the storage off with the per-storage toggle.</summary>
        SwitchedOff = 1,

        /// <summary>
        /// The storage refuses this player: another player's private chest, or a chest a ward denies
        /// (<c>Container.CheckAccess</c>, or the guard-stone check for the chest's position).
        /// </summary>
        NoAccess = 2
    }

    /// <summary>
    /// The crafting readout's "nearby but blocked" decision, in pure form: whether a requirement that
    /// storage does not cover should say <i>why</i>, what marker to append, and how to name the
    /// reason. The game-facing readout supplies the counts and the configured strings; every decision
    /// it could get wrong lives here, where it is unit-tested.
    /// <para>
    /// <b>The hint never promises the item is reachable.</b> It answers "why does this row say 0 when
    /// I know it is in that drawer?" — so it fires even when the blocked stock covers only part of the
    /// shortfall, and it fires for a storage the player may not be able to unlock either.
    /// </para>
    /// </summary>
    public static class AvailabilityHint
    {
        /// <summary>The shipped marker, appended to the amount text when the hint fires.</summary>
        public const string DefaultSuffix = " *";

        /// <summary>How the readout names a switched-off storage.</summary>
        public const string SwitchedOffPhrase = "switched off";

        /// <summary>How the readout names a storage this player may not use.</summary>
        public const string NoAccessPhrase = "locked";

        /// <summary>The phrase when both reasons occur in range at once.</summary>
        public const string BothPhrase = "switched off or locked";

        /// <summary>
        /// Whether the requirement row should show the blocked hint: the requirement is <b>not</b>
        /// covered (<paramref name="available"/> below <paramref name="required"/>) <b>and</b> some
        /// in-range storage holds the item while being blocked. A covered requirement is the flash
        /// state instead, and a requirement with nothing blocked behind it is the ordinary "you have
        /// none" case; both must look exactly as they do without this feature.
        /// </summary>
        /// <param name="required">How much the requirement needs.</param>
        /// <param name="available">What the player can draw on: their own pool plus usable storage.</param>
        /// <param name="blockedStock">How much of the item in-range <i>blocked</i> storage holds.</param>
        public static bool ShouldHint(int required, int available, int blockedStock)
        {
            return required > 0 && available < required && blockedStock > 0;
        }

        /// <summary>
        /// The configured marker, appended verbatim to the requirement's amount text. An unset or
        /// whitespace-only setting gives an empty marker, which is supported: the row's tint still
        /// carries the signal, and a player who wants the bare CraftFromContainers-style numbers can
        /// have them.
        /// </summary>
        public static string Suffix(string configuredSuffix)
        {
            return configuredSuffix == null ? string.Empty : configuredSuffix;
        }

        /// <summary>
        /// The reason as a phrase the HUD line can use, in a fixed order so the same situation always
        /// reads the same way. <see cref="StorageBlockReasons.None"/> has nothing to say and yields an
        /// empty string rather than a placeholder.
        /// </summary>
        public static string ReasonPhrase(StorageBlockReasons reasons)
        {
            bool off = (reasons & StorageBlockReasons.SwitchedOff) != 0;
            bool noAccess = (reasons & StorageBlockReasons.NoAccess) != 0;

            if (off && noAccess)
                return BothPhrase;
            if (off)
                return SwitchedOffPhrase;
            if (noAccess)
                return NoAccessPhrase;

            return string.Empty;
        }

        /// <summary>
        /// The one-off HUD line, for the moment the crafting panel shows a recipe whose shortfall sits
        /// in blocked storage: <c>"Seal Blubber is nearby but the storage holding it is switched off."</c>
        /// Returns an empty string when there is no reason to report, so a caller can print the result
        /// without pre-checking.
        /// </summary>
        /// <param name="displayName">
        /// The item's localized display name. A null or blank name still produces a readable sentence
        /// rather than a line starting with a space.
        /// </param>
        public static string Message(string displayName, StorageBlockReasons reasons)
        {
            string phrase = ReasonPhrase(reasons);
            if (phrase.Length == 0)
                return string.Empty;

            string name = string.IsNullOrEmpty(displayName) || displayName.Trim().Length == 0
                ? "An ingredient"
                : displayName.Trim();

            return name + " is nearby but the storage holding it is " + phrase + ".";
        }
    }
}
