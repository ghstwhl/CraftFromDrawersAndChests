using System.Collections.Generic;
using BepInEx.Logging;
using CraftFromDrawersAndChests.Logic;
using CraftFromDrawersAndChests.Pull;
using CraftFromDrawersAndChests.Storage;
using HarmonyLib;
using UnityEngine;

namespace CraftFromDrawersAndChests.Patches
{
    /// <summary>
    /// The mod's console command (Task 15b): <c>craftfromdrawersandchests</c>, or its alias
    /// <c>cfdac</c>, followed by a subcommand. It reports the mod's storage state (<c>status</c>) or
    /// reloads the config and forgets the tracked containers (<c>reset</c>), and hands anything else
    /// back to vanilla.
    /// <para>
    /// <b>The shape mirrors CraftFromContainers 4.0.7.</b> CFC prefixes <c>Terminal.InputText</c> and
    /// matches one whole lower-cased line, <c>"craftfromcontainers reset"</c> (CFC
    /// <c>BepInExPlugin.cs</c> line 1506); on a match it reloads and saves its config, echoes the typed
    /// line and prints <c>"Craft From Containers config reloaded"</c> (lines 1508-1511), then returns
    /// false so the line is not processed again. This patch is the same prefix on the same game method,
    /// with the same echo-then-answer output and the same "consume a matched line" return. Its command
    /// name and alias are <see cref="ModInfo.ConsoleCommand"/> and
    /// <see cref="ModInfo.ConsoleCommandAlias"/>.
    /// </para>
    /// <para>
    /// <b>The status report is this mod's own.</b> Neither CFC nor any other source of it exists —
    /// CFC has no status subcommand — so it reports the two things that are otherwise invisible from
    /// inside the game: how many storage sources a pull would actually use, and whether the soft
    /// Item Drawers dependency resolved. The count is the pull engine's own eligible-source list for
    /// the player's position (<see cref="PullEngine.Collect"/>), so it is exactly what a pull would
    /// draw on rather than a separate estimate that could disagree with one. The drawer line is
    /// <see cref="DrawerApi.IsAvailable"/>.
    /// </para>
    /// <para>
    /// <b><c>reset</c> keeps CFC's meaning and adds the registry clear.</b> CFC's reset reloads and
    /// saves the config file; this one does the same, then re-reads the entries into the shared
    /// snapshot — this mod reads configuration through <c>PluginConfig.Flags</c> rather than the
    /// entries, so without that re-read a reload would not take effect — and then calls
    /// <see cref="StorageRegistry.Clear"/>. Clear is documented as being for tearing a world down
    /// rather than for routine use, because the tracking patch only re-adds a container as it awakes;
    /// the command's own output says so (<see cref="ConsoleCommandRules.ResetMessage"/>) rather than
    /// hiding the caveat.
    /// </para>
    /// <para>
    /// <b>The command is deliberately not gated on the mod-enabled setting</b>, unlike CFC's
    /// (line 1503), because the report exists to be readable exactly when something is misconfigured.
    /// It consumes only lines that name this mod, so every other console line — including vanilla's
    /// own commands — is left untouched.
    /// </para>
    /// </summary>
    internal static class TerminalPatch
    {
        // Fully qualified: UnityEngine.Logger exists too, so the bare name is ambiguous.
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        /// <summary>Set once, so the live-patch breadcrumb is written at most once per session.</summary>
        private static bool firstLineLogged;

        /// <summary>
        /// The console input hook. A prefix on <c>Terminal.InputText</c>, mirroring CraftFromContainers'
        /// own <c>InputText_Patch</c>: a line that names this mod is echoed and answered and the prefix
        /// returns false, so the console does not also try to resolve it; anything else returns true and
        /// is vanilla's.
        /// </summary>
        [HarmonyPatch(typeof(Terminal), "InputText")]
        [HarmonyPrefix]
        private static bool InputTextPrefix(Terminal __instance)
        {
            if (__instance == null)
                return true;

            string input = InputText(__instance);

            // One-shot proof that this patch is live and what it actually sees. 0.1.1 shipped a build
            // whose patches were never applied, and no log line could tell that apart from a hook that
            // runs and does not match - so the hook now says so once, whatever the line is.
            if (!firstLineLogged)
            {
                firstLineLogged = true;
                Log.LogInfo(ModInfo.ModName + ": console hook is live; first line seen was '" + input + "'.");
            }

            if (!ConsoleCommandRules.TryParse(
                    input, ModInfo.ConsoleCommand, ModInfo.ConsoleCommandAlias, out ConsoleSubcommand subcommand))
                return true;

            // CFC echoes the typed line before its answer (line 1510); the same here, so the console
            // shows what was answered.
            __instance.AddString(input.Trim());

            switch (subcommand)
            {
                case ConsoleSubcommand.Status:
                    Print(__instance, StatusReport());
                    break;

                case ConsoleSubcommand.Reset:
                    Reset(__instance);
                    break;

                default:
                    // No subcommand, or one this mod does not know: print the usage rather than
                    // silently swallowing the line.
                    Print(__instance, ConsoleCommandRules.Help(ModInfo.ConsoleCommand, ModInfo.ConsoleCommandAlias));
                    break;
            }

            return false;
        }

        /// <summary>
        /// Builds the status report from the live world: the eligible sources within range of the local
        /// player, and whether the Item Drawers API resolved. With no player loaded there is no origin
        /// to measure from, so the counts are reported as unknown rather than as zero.
        /// <para>
        /// The configuration snapshot is guarded exactly as <see cref="Reset"/> guards it, and for the
        /// same reason: <see cref="PullEngine.Collect"/> reads the eligible sources through it
        /// (<c>StorageRegistry.Collect</c> dereferences its range and its chest/drawer switches), so a
        /// null snapshot would make this prefix throw instead of printing a report. There is nothing
        /// to measure with, so the counts are reported as unknown, naming which piece was missing.
        /// </para>
        /// </summary>
        private static string StatusReport()
        {
            Player player = Player.m_localPlayer;
            FeatureFlags flags = PluginConfig.Flags;

            if (player == null || flags == null)
                return ConsoleCommandRules.StatusReport(false, 0, 0, DrawerApi.IsAvailable,
                    player == null ? "no player is loaded" : "the config is not ready");

            Vector3 position = player.transform.position;
            var origin = new Vec3(position.x, position.y, position.z);

            IReadOnlyList<IStorageSource> sources = PullEngine.Collect(origin, flags);
            int chests = 0;
            int drawers = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].Kind == StorageKind.Chest)
                    chests++;
                else if (sources[i].Kind == StorageKind.Drawer)
                    drawers++;
            }

            return ConsoleCommandRules.StatusReport(true, chests, drawers, DrawerApi.IsAvailable);
        }

        /// <summary>
        /// Reloads the config file, re-reads it into the shared snapshot so the reload actually takes
        /// effect, forgets the tracked containers and prints the result. The config file is optional —
        /// a null file (an uninitialised config) skips the reload but still clears and reports, so the
        /// command never throws.
        /// </summary>
        private static void Reset(Terminal terminal)
        {
            if (PluginConfig.File != null)
            {
                PluginConfig.File.Reload();
                PluginConfig.File.Save();
            }

            // This mod reads configuration through the snapshot, not the entries, so a reload must be
            // followed by a re-read or every flag would keep its old value.
            if (PluginConfig.Flags != null)
                PluginConfig.ReadInto(PluginConfig.Flags);

            // CFC's reset is a config reload; the tracked-container clear is this mod's addition, for
            // the same reason the registry documents it: a reset is a teardown. See ResetMessage.
            StorageRegistry.Clear();

            Print(terminal, ConsoleCommandRules.ResetMessage);
            Debug("Console: config reloaded and tracked storage cleared.");
        }

        /// <summary>The console's current input text, or null when there is none.</summary>
        private static string InputText(Terminal terminal)
        {
            return terminal.m_input != null ? terminal.m_input.text : null;
        }

        /// <summary>Prints one or more lines to the console's scrollback.</summary>
        private static void Print(Terminal terminal, string text)
        {
            if (terminal == null || string.IsNullOrEmpty(text))
                return;

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                terminal.AddString(lines[i]);
        }

        /// <summary>Logs a line only when debug logging is on, so the default is quiet.</summary>
        private static void Debug(string message)
        {
            FeatureFlags flags = PluginConfig.Flags;
            if (flags != null && flags.IsDebug)
                Log.LogInfo(message);
        }
    }
}
