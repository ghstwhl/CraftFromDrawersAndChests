using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;

namespace CraftFromDrawersAndChests
{
    /// <summary>
    /// BepInEx entry point. All later patches hang off this class: they read
    /// <see cref="Harmony"/> to register patches and write through <see cref="Log"/>.
    /// </summary>
    [BepInPlugin(ModInfo.ModGuid, ModInfo.ModName, ModInfo.Version)]
    // Hard dependency: Jötunn also provides the network-compatibility enforcement below.
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Hard dependency: MultiUserChest is the only supported chest-removal path. Without it the
    // only working chest implementation is the direct inventory mutation this design forbids.
    [BepInDependency("com.maxsch.valheim.MultiUserChest")]
    // Soft dependency: the mod still runs chest-only when Item Drawers is not installed.
    [BepInDependency("kg.ItemDrawers", BepInDependency.DependencyFlags.SoftDependency)]
    // The two mods this one replaces; both pull from storage, so coexisting would desync.
    [BepInIncompatibility("aedenthorn.CraftFromContainers")]
    [BepInIncompatibility("Azumatt.AzuCraftyBoxes")]
    // Version enforcement: every player and the server must run this mod at a matching version.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    // Task 8's requirement is that a client inherits the server's values and that a non-admin sees
    // every entry read-only. Jotunn enforces that for entries carrying IsAdminOnly only when the
    // plugin's AdminOnlyStrictness says so; with no attribute the strictness is Jotunn's implicit
    // default, which its XML docs do not state. Pinning it to Always makes the lock independent of
    // whether Jotunn detects this mod on the server (a hard dependency with EveryoneMustHaveMod
    // below, so it always is), so a non-admin client can never edit a value the server owns.
    [SynchronizationMode(AdminOnlyStrictness.Always)]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>The live plugin instance, for later patches that need the <see cref="BaseUnityPlugin"/>.</summary>
        public static Plugin Instance;

        /// <summary>The Harmony instance patches are registered against.</summary>
        public static Harmony Harmony;

        /// <summary>
        /// The plugin's logger, forwarded from <see cref="BaseUnityPlugin.Logger"/>. It is a
        /// forwarding property rather than a field so that <see cref="Awake"/> holds only the
        /// fixed start-up sequence — assign <see cref="Instance"/>, create <see cref="Harmony"/>,
        /// initialise and read the configuration, then run <c>PatchAll</c> — and carries no
        /// logging plumbing of its own.
        /// </summary>
        internal static ManualLogSource Log => Instance.Logger;

        private void Awake()
        {
            Instance = this;
            Harmony = new Harmony(ModInfo.ModGuid);

            // Configuration first: PluginConfig.ReadInto is the single writer of the shared
            // FeatureFlags snapshot, and it must run before PatchAll so every patch observes a
            // fully populated snapshot rather than the all-false values a non-defaulted DTO
            // would otherwise hold.
            //
            // A failure here must not be able to disable the whole mod, which is exactly what one
            // wrong config call did (Task 15f): the throw aborted Awake before PatchAll, so no
            // patch was applied at all - and BepInEx still reported "0 failed", because an
            // exception in a plugin's Awake is logged by Unity, not by the chainloader, which made
            // a total failure silent. So the configuration is initialised inside a guard whose
            // catch names this mod and the failing step and logs at Error level (LogError is
            // written to LogOutput.log whatever the user's configured log level), and then still
            // falls through to Harmony.PatchAll().
            //
            // On failure the guard is deliberately "patch anyway, loudly, and deterministically
            // off": FeatureFlags is non-defaulted, so a read that failed partway would otherwise
            // leave an unpredictable half-populated snapshot. Forcing ModEnabled false instead
            // makes the outcome a clean, fully disabled mod - the safest state - and it cannot be a
            // silent no-op, because the LogError below is always emitted and is the only thing that
            // reports it. Every config-gated feature checks ModEnabled (directly, or through the
            // same flags in Enabled()), so forcing it false leaves the mod deterministically inert
            // rather than half-configured.
            try
            {
                PluginConfig.Init(Config);
                PluginConfig.ReadInto(PluginConfig.Flags);
            }
            catch (Exception e)
            {
                if (PluginConfig.Flags != null)
                    PluginConfig.Flags.ModEnabled = false;

                Log.LogError("CraftFromDrawersAndChests: configuration initialisation failed; the " +
                             "mod is being left fully disabled until this is fixed (Harmony patches " +
                             "are still applied, but every config-gated feature is inert). Cause: " + e);
            }

            // The assembly is passed EXPLICITLY. Harmony's no-argument PatchAll() infers its target
            // from a stack frame - new StackTrace().GetFrame(1).GetMethod().ReflectedType.Assembly -
            // and if that inference lands anywhere other than this assembly, it patches
            // AccessTools.GetTypesFromAssembly(theWrongAssembly) instead: zero patches, no exception
            // and no log line. That is exactly what 0.1.1 did. Naming the assembly removes the
            // inference entirely, so this can no longer depend on stack-frame resolution.
            Harmony.PatchAll(typeof(Plugin).Assembly);

            LogPatchReport();
        }

        /// <summary>
        /// Logs how many game methods this mod's Harmony instance actually patched, and what
        /// Harmony's own no-argument <c>PatchAll()</c> would have chosen instead.
        /// <para>
        /// This is permanent, not debugging scaffolding. 0.1.1 loaded cleanly, threw nothing and
        /// patched nothing, and the log could not distinguish that from a mod that runs correctly and
        /// simply finds no storage nearby - which cost two test cycles. The count is the one fact that
        /// separates those two, and it is cheap. <c>GetPatchedMethods()</c> is an instance method that
        /// filters to this instance's own patches (it calls the static
        /// <c>GetAllPatchedMethods()</c> and then keeps only the methods whose owners include this
        /// instance's ID), so a non-zero count means this mod's patches are live.
        /// </para>
        /// </summary>
        private static void LogPatchReport()
        {
            int patchedCount = 0;
            foreach (var patchedMethod in Harmony.GetPatchedMethods())
                patchedCount++;

            Log.LogInfo(ModInfo.ModName + " " + ModInfo.Version + ": Harmony patched " + patchedCount +
                        " game method(s). Harmony's no-argument PatchAll() would have inferred the " +
                        "assembly '" + InferPatchAllAssembly() + "'.");

            if (patchedCount == 0)
            {
                Log.LogError(ModInfo.ModName + ": Harmony applied ZERO patches, so every feature is " +
                             "inert (no chest pulling, no drawer pulling, no per-storage switch, no " +
                             "console command). Nothing else in this log marks that state.");
            }
        }

        /// <summary>
        /// Reproduces, and reports, the expression Harmony's no-argument <c>PatchAll()</c> uses to
        /// choose the assembly it patches: <c>new StackTrace().GetFrame(1).GetMethod().
        /// ReflectedType.Assembly</c>.
        /// <para>
        /// The stack-frame index matters: in <c>PatchAll()</c>, frame 1 is <c>Awake</c>, so this probe
        /// is deliberately its own method - called FROM <c>Awake</c> - which puts <c>Awake</c> at frame
        /// 1 here too and makes the reported value the one the no-argument call would have used. It
        /// exists only to report that value; patching itself no longer uses it.
        /// </para>
        /// <para>
        /// NoInlining is load-bearing: the frame index is only equivalent to Harmony's while this is a
        /// real frame. If the JIT inlined it into <c>Awake</c>, frame 1 would become <c>Awake</c>'s own
        /// caller and the probe would report a Unity/native assembly - which would look exactly like
        /// the bug being diagnosed, when the inference had in fact worked.
        /// </para>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string InferPatchAllAssembly()
        {
            try
            {
                System.Diagnostics.StackFrame frame = new System.Diagnostics.StackTrace().GetFrame(1);
                if (frame == null)
                    return "<null frame>";

                System.Reflection.MethodBase method = frame.GetMethod();
                if (method == null)
                    return "<null method>";

                System.Type reflectedType = method.ReflectedType;
                if (reflectedType == null)
                    return "<null reflected type>";

                return reflectedType.Assembly.GetName().Name;
            }
            catch (Exception e)
            {
                return "<threw " + e.GetType().Name + ">";
            }
        }
    }
}
