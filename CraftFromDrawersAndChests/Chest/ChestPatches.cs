using System;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace CraftFromDrawersAndChests.Chest
{
    /// <summary>
    /// Start-up for the absorbed MultiUserChest layer. Everything else in this namespace is upstream
    /// MultiUserChest 0.6.2 (MIT) with the namespace rewritten; this file replaces upstream's
    /// <c>Plugin : BaseUnityPlugin</c>, which cannot be absorbed as written because a second
    /// <c>[BepInPlugin]</c> inside our assembly would have BepInEx treat this assembly as two plugins.
    /// <para>
    /// What upstream's plugin class did, and where each part went: <c>Log.Init(Logger)</c> and the
    /// service locator are handled by <see cref="InitialiseLogging"/>; <c>harmony.PatchAll()</c> is the
    /// plugin's own assembly-wide <c>PatchAll</c>, which picks these classes up because they carry
    /// <c>[HarmonyPatch]</c> on the type; the conditional item-drawers patch is
    /// <see cref="ApplyCompatPatches"/>.
    /// </para>
    /// </summary>
    internal static class ChestPatches
    {
        /// <summary>
        /// Upstream's identity, kept as constants because the plugin GUID appears in log lines and the
        /// route names below are wire data. The GUID itself is no longer claimed - the mod it names is
        /// now an incompatibility - so these are for diagnostics and attribution only.
        /// </summary>
        internal const string UpstreamName = "MultiUserChest";

        /// <summary>The upstream plugin GUID. Our plugin refuses to load when this plugin is present.</summary>
        internal const string UpstreamGuid = "com.maxsch.valheim.MultiUserChest";

        /// <summary>The upstream version this layer was taken from.</summary>
        internal const string UpstreamVersion = "0.6.2";

        /// <summary>
        /// The mod whose item-drawers support upstream patched conditionally, and whose GUID is the same
        /// one this repo reads for its own drawer sources.
        /// </summary>
        private const string ItemDrawersGuid = "mkz.itemdrawers";

        private static bool loggingReady;
        private static bool compatApplied;

        /// <summary>
        /// Points the absorbed <see cref="Log"/> facade at this mod's logger. Upstream did this in its own
        /// <c>Awake</c>; without it every absorbed line silently falls back to
        /// <c>UnityEngine.Debug</c>, which BepInEx does not route into <c>LogOutput.log</c>.
        /// Idempotent, and safe to call before any patch runs.
        /// </summary>
        /// <param name="log">This plugin's log source.</param>
        /// <param name="isDebug">
        /// The mod's own Debug setting, read on every log call so a change takes effect immediately.
        /// This replaces upstream's compile-time <c>#if DEBUG</c> switch.
        /// </param>
        internal static void InitialiseLogging(ManualLogSource log, Func<bool> isDebug)
        {
            if (loggingReady || log == null)
                return;

            loggingReady = true;
            Log.Init(log);
            Log.SetDebugProbe(isDebug);
        }

        /// <summary>
        /// Applies the patches upstream applied conditionally. Called once, after the assembly-wide
        /// <c>PatchAll</c>, because <see cref="ItemDrawerCompat"/> carries its <c>[HarmonyPatch]</c>
        /// attributes on the methods rather than on the type - so the assembly scan skips it by design,
        /// exactly as it did upstream. Idempotent.
        /// </summary>
        internal static void ApplyCompatPatches(Harmony harmony)
        {
            if (compatApplied || harmony == null)
                return;

            compatApplied = true;

            // Upstream deliberately leaves QuickStack unpatched ("broken in the current Valheim
            // release") and its class is kept only for reference; see QuickStackPatch.cs.
            if (Chainloader.PluginInfos.ContainsKey(ItemDrawersGuid))
            {
                harmony.PatchAll(typeof(ItemDrawerCompat));
                Log.LogInfo(UpstreamName + " layer: item-drawers compatibility patched.");
            }
        }
    }
}
