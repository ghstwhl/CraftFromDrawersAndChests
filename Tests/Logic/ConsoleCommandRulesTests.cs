using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The pure half of the mod's console command (Task 15b): whether a typed line names this mod,
    /// which subcommand it asks for, and the text the command prints. The game-facing
    /// <c>TerminalPatch</c> reads the console and hands the line here; nothing here touches the game.
    /// </summary>
    [TestFixture]
    public class ConsoleCommandRulesTests
    {
        private const string Command = "craftfromdrawersandchests";
        private const string Alias = "cfdac";

        // ---------------- TryParse ----------------

        [Test]
        public void TryParse_BlankLine_IsNotThisMod()
        {
            Assert.That(ConsoleCommandRules.TryParse(null, Command, Alias, out var none1), Is.False);
            Assert.That(none1, Is.EqualTo(ConsoleSubcommand.None));
            Assert.That(ConsoleCommandRules.TryParse("   ", Command, Alias, out var none2), Is.False);
            Assert.That(none2, Is.EqualTo(ConsoleSubcommand.None));
        }

        [Test]
        public void TryParse_AnotherCommand_IsNotThisMod()
        {
            Assert.That(ConsoleCommandRules.TryParse("craftfromcontainers reset", Command, Alias, out var sub), Is.False);
            Assert.That(sub, Is.EqualTo(ConsoleSubcommand.None));
        }

        [Test]
        public void TryParse_CommandWithoutSubcommand_IsThisModAndAsksForUsage()
        {
            Assert.That(ConsoleCommandRules.TryParse(Command, Command, Alias, out var sub), Is.True);
            Assert.That(sub, Is.EqualTo(ConsoleSubcommand.None));
        }

        [Test]
        public void TryParse_RecognisesBothTokensCaseInsensitively()
        {
            Assert.That(ConsoleCommandRules.TryParse("cfdac status", Command, Alias, out var s1), Is.True);
            Assert.That(s1, Is.EqualTo(ConsoleSubcommand.Status));

            Assert.That(ConsoleCommandRules.TryParse("CFDAC STATUS", Command, Alias, out var s2), Is.True);
            Assert.That(s2, Is.EqualTo(ConsoleSubcommand.Status));

            Assert.That(ConsoleCommandRules.TryParse("CraftFromDrawersAndChests reset", Command, Alias, out var s3), Is.True);
            Assert.That(s3, Is.EqualTo(ConsoleSubcommand.Reset));
        }

        [Test]
        public void TryParse_ToleratesSurroundingAndRepeatedWhitespace()
        {
            Assert.That(ConsoleCommandRules.TryParse("  cfdac   status  ", Command, Alias, out var sub), Is.True);
            Assert.That(sub, Is.EqualTo(ConsoleSubcommand.Status));
        }

        [Test]
        public void TryParse_UnknownSubcommand_IsReportedAsUnknown()
        {
            // A mistyped subcommand must be self-explanatory (the caller prints the usage) rather than
            // silently consuming the line.
            Assert.That(ConsoleCommandRules.TryParse("cfdac wibble", Command, Alias, out var sub), Is.True);
            Assert.That(sub, Is.EqualTo(ConsoleSubcommand.Unknown));
        }

        [Test]
        public void TryParse_ExtraTokensAfterTheSubcommand_AreIgnored()
        {
            Assert.That(ConsoleCommandRules.TryParse("cfdac status now please", Command, Alias, out var sub), Is.True);
            Assert.That(sub, Is.EqualTo(ConsoleSubcommand.Status));
        }

        // ---------------- IsCommand ----------------

        [Test]
        public void IsCommand_UnsetOrNullConfiguredToken_NeverMatches()
        {
            // An unset alias must not swallow every console line.
            Assert.That(ConsoleCommandRules.IsCommand("cfdac", Command, string.Empty), Is.False);
            Assert.That(ConsoleCommandRules.IsCommand("cfdac", Command, null), Is.False);
            Assert.That(ConsoleCommandRules.IsCommand(string.Empty, Command, Alias), Is.False);
            Assert.That(ConsoleCommandRules.IsCommand(null, Command, Alias), Is.False);
        }

        // ---------------- ParseVerb ----------------

        [Test]
        public void ParseVerb_MapsTheTwoKnownVerbs()
        {
            Assert.That(ConsoleCommandRules.ParseVerb("status"), Is.EqualTo(ConsoleSubcommand.Status));
            Assert.That(ConsoleCommandRules.ParseVerb("RESET"), Is.EqualTo(ConsoleSubcommand.Reset));
        }

        [Test]
        public void ParseVerb_EmptyIsNoneAndAnythingElseIsUnknown()
        {
            Assert.That(ConsoleCommandRules.ParseVerb(null), Is.EqualTo(ConsoleSubcommand.None));
            Assert.That(ConsoleCommandRules.ParseVerb(string.Empty), Is.EqualTo(ConsoleSubcommand.None));
            Assert.That(ConsoleCommandRules.ParseVerb("reload"), Is.EqualTo(ConsoleSubcommand.Unknown));
        }

        // ---------------- StatusReport ----------------

        [Test]
        public void StatusReport_WithAPlayer_CountsEverySourceInRange()
        {
            Assert.That(
                ConsoleCommandRules.StatusReport(true, 3, 1, true),
                Is.EqualTo("Sources in range of the player: 4 (3 chests, 1 drawer)\nItem Drawers API: resolved"));
        }

        [Test]
        public void StatusReport_PluralisesBothKinds()
        {
            Assert.That(
                ConsoleCommandRules.StatusReport(true, 1, 0, false),
                Is.EqualTo("Sources in range of the player: 1 (1 chest, 0 drawers)\nItem Drawers API: not resolved (chest-only)"));
        }

        [Test]
        public void StatusReport_WithNoPlayer_ReportsTheCountAsUnknown()
        {
            // At the main menu, or on a dedicated server's console, there is no origin to measure
            // from, so zero would be a lie; the drawer line is still a property of the assemblies.
            Assert.That(
                ConsoleCommandRules.StatusReport(false, 0, 0, true),
                Is.EqualTo("Sources in range: unknown (no player is loaded)\nItem Drawers API: resolved"));
        }

        [Test]
        public void StatusReport_AnUnmeasurableCount_NamesTheReasonItCouldNotBeMeasured()
        {
            // The other way the count can be unmeasurable is a missing configuration snapshot (the
            // game-facing patch guards it exactly as reset does); the report must name that reason
            // rather than blame the player.
            Assert.That(
                ConsoleCommandRules.StatusReport(false, 0, 0, false, "the config is not ready"),
                Is.EqualTo("Sources in range: unknown (the config is not ready)\nItem Drawers API: not resolved (chest-only)"));
        }

        [Test]
        public void StatusReport_NegativeCounts_ClampAtZero()
        {
            Assert.That(
                ConsoleCommandRules.StatusReport(true, -5, -1, false),
                Is.EqualTo("Sources in range of the player: 0 (0 chests, 0 drawers)\nItem Drawers API: not resolved (chest-only)"));
        }

        // ---------------- Help ----------------

        [Test]
        public void Help_NamesBothTokensAndBothSubcommandsAndCarriesTheResetCaveat()
        {
            string help = ConsoleCommandRules.Help(Command, Alias);
            Assert.That(help, Does.Contain("Usage: " + Command));
            Assert.That(help, Does.Contain("(alias: " + Alias + ")"));
            Assert.That(help, Does.Contain("<status|reset>"));
            Assert.That(help, Does.Contain("re-awake"));
        }

        [Test]
        public void Help_WithoutAnAlias_StillNamesTheCommandAndSubcommands()
        {
            string help = ConsoleCommandRules.Help(Command, string.Empty);
            Assert.That(help, Does.Contain("Usage: " + Command));
            Assert.That(help, Does.Not.Contain("alias"));
            Assert.That(help, Does.Contain("<status|reset>"));
        }

        // ---------------- ResetMessage ----------------

        [Test]
        public void ResetMessage_CarriesTheReAwakeCaveat()
        {
            // The registry's own warning: a mid-session reset leaves the tracked set incomplete until
            // the containers re-awake, and the command's output says so rather than hiding it.
            Assert.That(ConsoleCommandRules.ResetMessage, Does.Contain("re-awake"));
        }
    }
}
