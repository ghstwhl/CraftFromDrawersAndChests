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
            PluginConfig.Init(Config);
            PluginConfig.ReadInto(PluginConfig.Flags);

            Harmony.PatchAll();
        }
    }
}
