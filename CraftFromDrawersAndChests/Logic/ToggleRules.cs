using System.Text;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The two decisions behind the per-storage pull switch that need no game type: the hover suffix
    /// text and whether a toggle key is configured at all. Both are pure, so they live here and are
    /// unit-tested; the ZDO read/write and the Harmony patches that surround them cannot be run
    /// outside the game and stay in the game-facing <c>Toggle/</c> layer.
    /// <para>
    /// The suffix is Unity rich text because the game renders hover text through TextMeshPro, which
    /// understands <c>&lt;color&gt;</c> tags; that makes the current state visible at a glance without
    /// relying on the surrounding text's own colour.
    /// </para>
    /// </summary>
    public static class ToggleRules
    {
        /// <summary>The colour of the "off" suffix: a storage excluded from pulling.</summary>
        public const string OffColor = "#FF5555";

        /// <summary>The colour of the "on" suffix: a storage the mod may pull from.</summary>
        public const string OnColor = "#55FF55";

        /// <summary>The "off" label, exactly as the brief names it, leading space included.</summary>
        public const string OffLabel = " (pull: off)";

        /// <summary>The "on" label, exactly as the brief names it, leading space included.</summary>
        public const string OnLabel = " (pull: on)";

        /// <summary>
        /// The coloured suffix appended to a storage's hover text to show its switch state. A storage
        /// that is off says so; a storage that is on says so. Off and on never produce the same
        /// string, so a change is always visible.
        /// </summary>
        public static string HoverSuffix(bool off)
        {
            return off
                ? "<color=" + OffColor + ">" + OffLabel + "</color>"
                : "<color=" + OnColor + ">" + OnLabel + "</color>";
        }

        /// <summary>
        /// Whether a configured key value represents a bound key. The shipped default is empty, and an
        /// empty (or whitespace-only) value must never reach the input system, which throws on it; a
        /// configured-but-unrecognised name is the game layer's problem, caught there.
        /// </summary>
        public static bool IsKeyConfigured(string key)
        {
            return !string.IsNullOrWhiteSpace(key);
        }

        /// <summary>
        /// The hover hint that names the key which flips a storage's switch, or states plainly that no
        /// key is bound. It sits alongside <see cref="HoverSuffix"/>, which reports the state but not
        /// how to change it - and this setting is server-authoritative, so a client cannot necessarily
        /// read the key out of their own configuration file.
        /// <para>
        /// The unbound wording is a statement rather than an instruction to change a setting, for that
        /// same reason: the value a client sees comes from the server, so "set this yourself" could be
        /// wrong. A configured name is shown as a player would say it - "left shift" reads as
        /// "Left Shift".
        /// </para>
        /// </summary>
        /// <param name="configuredKey">The configured key name; null, empty or whitespace means unbound.</param>
        public static string ToggleHint(string configuredKey)
        {
            return IsKeyConfigured(configuredKey)
                ? " (press " + DisplayKeyName(configuredKey) + " to toggle)"
                : " (no toggle key is set)";
        }

        /// <summary>
        /// A configured key name as it should read to a player: each space-separated word capitalised,
        /// so the stored <c>"left shift"</c> shows as <c>"Left Shift"</c>. The rest of each word is
        /// lower-cased, so an unexpected casing in the setting is still legible rather than mangled,
        /// and the separators are preserved so the name keeps the shape it was configured with.
        /// </summary>
        private static string DisplayKeyName(string key)
        {
            string trimmed = key.Trim();
            if (trimmed.Length == 0)
                return trimmed;

            var builder = new StringBuilder(trimmed.Length);
            bool startOfWord = true;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == ' ')
                {
                    startOfWord = true;
                    builder.Append(c);
                    continue;
                }

                builder.Append(startOfWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                startOfWord = false;
            }

            return builder.ToString();
        }
    }
}
