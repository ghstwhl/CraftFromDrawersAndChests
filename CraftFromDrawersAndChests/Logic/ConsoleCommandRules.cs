using System;
using System.Globalization;
using System.Text;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// Which subcommand a console line asks this mod for. It is parsed from the line's second token,
    /// the same position CraftFromContainers 4.0.7 reads its own single subcommand from
    /// (<c>craftfromcontainers reset</c>, CFC <c>BepInExPlugin.cs</c> line 1506).
    /// </summary>
    public enum ConsoleSubcommand
    {
        /// <summary>No subcommand: the mod should print its usage.</summary>
        None,

        /// <summary><c>status</c> — report the storage state.</summary>
        Status,

        /// <summary><c>reset</c> — reload the config and forget the tracked containers.</summary>
        Reset,

        /// <summary>A word this mod does not know: the mod should print its usage.</summary>
        Unknown
    }

    /// <summary>
    /// The pure half of this mod's console command (Task 15b): whether a typed line names this mod,
    /// which subcommand it asks for, and the text the command prints. The game-facing
    /// <c>Patches.TerminalPatch</c> reads the line off the console and hands it here; nothing in this
    /// file touches UnityEngine, Valheim, BepInEx or Jötunn, so every decision is unit-tested.
    /// <para>
    /// <b>The shape mirrors CraftFromContainers.</b> CFC matches one whole lower-cased line
    /// (<c>text.ToLower().Equals("craftfromcontainers reset")</c>, CFC line 1506) and has exactly one
    /// subcommand, <c>reset</c>, which reloads and saves the config and echoes a "config reloaded"
    /// line (CFC lines 1508-1511). This mod keeps that name form — a command token followed by a
    /// subcommand — keeps <c>reset</c>'s meaning, and adds <c>status</c> for its own report. The
    /// match here is case-insensitive and tolerates surrounding and repeated whitespace, which is a
    /// superset of CFC's own <c>ToLower().Equals</c>.
    /// </para>
    /// </summary>
    public static class ConsoleCommandRules
    {
        /// <summary>The <c>status</c> subcommand: report the storage state.</summary>
        public const string StatusVerb = "status";

        /// <summary>The <c>reset</c> subcommand: reload the config and forget the tracked containers.</summary>
        public const string ResetVerb = "reset";

        /// <summary>
        /// What <c>reset</c> prints once the config has been reloaded and the tracked containers
        /// forgotten. It carries the caveat the storage registry's own clear is documented with: the
        /// tracking patch only re-adds a container as it awakes, so a mid-session reset leaves the
        /// tracked set incomplete until the objects re-awake.
        /// </summary>
        public const string ResetMessage =
            "Config reloaded; storage tracking cleared. Containers re-join the tracked set as they " +
            "re-awake, so it stays incomplete until they do.";

        /// <summary>
        /// Parses a console line. Returns true when the line names this mod — its first token equals
        /// <paramref name="commandName"/> or <paramref name="alias"/>, case-insensitively — and then
        /// reports which subcommand the line asks for. Returns false, with
        /// <paramref name="subcommand"/> left as <see cref="ConsoleSubcommand.None"/>, when the line is
        /// blank or belongs to some other command, so the caller can leave the console to vanilla.
        /// <para>
        /// An empty, missing or unrecognised subcommand parses as <see cref="ConsoleSubcommand.None"/>
        /// or <see cref="ConsoleSubcommand.Unknown"/>; both tell the caller to print the usage, so a
        /// mistyped subcommand is self-explanatory instead of silently consuming the line.
        /// </para>
        /// </summary>
        /// <param name="input">The console's raw input text; may be null.</param>
        /// <param name="commandName">The mod's full command token; may be null or empty.</param>
        /// <param name="alias">The mod's short command token; may be null or empty.</param>
        /// <param name="subcommand">The parsed subcommand; <see cref="ConsoleSubcommand.None"/> unless the line matched.</param>
        public static bool TryParse(
            string input, string commandName, string alias, out ConsoleSubcommand subcommand)
        {
            subcommand = ConsoleSubcommand.None;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string[] tokens = input.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return false;

            if (!IsCommand(tokens[0], commandName, alias))
                return false;

            subcommand = tokens.Length > 1 ? ParseVerb(tokens[1]) : ConsoleSubcommand.None;
            return true;
        }

        /// <summary>
        /// Whether <paramref name="token"/> is one of this mod's command tokens. The comparison is
        /// ordinal and case-insensitive, so <c>CFDAC</c> and <c>cfdac</c> are the same command, and a
        /// null or empty configured token never matches anything (so an unset alias cannot swallow
        /// every line).
        /// </summary>
        public static bool IsCommand(string token, string commandName, string alias)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            return Matches(token, commandName) || Matches(token, alias);
        }

        /// <summary>
        /// Maps a subcommand token to its <see cref="ConsoleSubcommand"/>. Only the two tokens this mod
        /// defines are recognised; everything else — including null and empty — is
        /// <see cref="ConsoleSubcommand.Unknown"/> or <see cref="ConsoleSubcommand.None"/>, which the
        /// caller answers with the usage text.
        /// </summary>
        public static ConsoleSubcommand ParseVerb(string verb)
        {
            if (string.IsNullOrEmpty(verb))
                return ConsoleSubcommand.None;

            if (string.Equals(verb, StatusVerb, StringComparison.OrdinalIgnoreCase))
                return ConsoleSubcommand.Status;

            if (string.Equals(verb, ResetVerb, StringComparison.OrdinalIgnoreCase))
                return ConsoleSubcommand.Reset;

            return ConsoleSubcommand.Unknown;
        }

        /// <summary>
        /// The status report: how many storage sources are in range of the player, split into chests
        /// and drawers, and whether the Item Drawers API resolved. The counts are the pull engine's
        /// own eligible-source count, so the number the player sees is the number a pull would use.
        /// <para>
        /// With no player loaded — the main menu, or a dedicated server's console — there is no origin
        /// to measure from, so the source count is reported as unknown rather than as zero, and the
        /// drawer line is still reported because it is a property of the loaded assemblies rather than
        /// of the world.
        /// </para>
        /// </summary>
        /// <param name="playerLoaded">Whether a local player exists to measure storage from.</param>
        /// <param name="chestCount">How many eligible chests are in range.</param>
        /// <param name="drawerCount">How many eligible drawers are in range.</param>
        /// <param name="drawerApiResolved">Whether the Item Drawers API resolved this session.</param>
        public static string StatusReport(
            bool playerLoaded, int chestCount, int drawerCount, bool drawerApiResolved)
        {
            var builder = new StringBuilder();
            if (playerLoaded)
            {
                builder.Append("Sources in range of the player: ")
                    .Append(NonNegative(chestCount) + NonNegative(drawerCount))
                    .Append(" (")
                    .Append(Count(NonNegative(chestCount), "chest", "chests"))
                    .Append(", ")
                    .Append(Count(NonNegative(drawerCount), "drawer", "drawers"))
                    .Append(')');
            }
            else
            {
                builder.Append("Sources in range: unknown (no player is loaded)");
            }

            builder.Append('\n')
                .Append("Item Drawers API: ")
                .Append(drawerApiResolved ? "resolved" : "not resolved (chest-only)");
            return builder.ToString();
        }

        /// <summary>
        /// The usage text, shown when the command is typed without a subcommand or with one this mod
        /// does not know. It names both accepted command tokens and both subcommands, and repeats
        /// <see cref="ResetMessage"/>'s caveat so the reset's consequence is visible before it is run.
        /// </summary>
        /// <param name="commandName">The mod's full command token.</param>
        /// <param name="alias">The mod's short command token.</param>
        public static string Help(string commandName, string alias)
        {
            var builder = new StringBuilder();
            builder.Append("Usage: ").Append(commandName);
            if (!string.IsNullOrEmpty(alias))
                builder.Append(" (alias: ").Append(alias).Append(')');

            builder.Append(" <").Append(StatusVerb).Append('|').Append(ResetVerb).Append('>')
                .Append('\n')
                .Append("  ").Append(StatusVerb)
                .Append("  how many storage sources are in range, and whether the Item Drawers API resolved")
                .Append('\n')
                .Append("  ").Append(ResetVerb)
                .Append("   reload the config and forget the tracked containers; they re-join it as they re-awake, so it stays incomplete until they do");
            return builder.ToString();
        }

        /// <summary>"3 chests", "1 chest" — the count with its correctly pluralised noun.</summary>
        private static string Count(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }

        /// <summary>Clamps a count at zero: a negative would otherwise print a nonsense total.</summary>
        private static int NonNegative(int count)
        {
            return count > 0 ? count : 0;
        }

        /// <summary>An ordinal, case-insensitive token comparison that treats a null configured token as no match.</summary>
        private static bool Matches(string token, string configured)
        {
            return !string.IsNullOrEmpty(configured)
                && string.Equals(token, configured, StringComparison.OrdinalIgnoreCase);
        }
    }
}
