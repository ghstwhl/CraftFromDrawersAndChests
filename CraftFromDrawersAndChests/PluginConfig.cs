using BepInEx.Configuration;
using CraftFromDrawersAndChests.Logic;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace CraftFromDrawersAndChests
{
    /// <summary>
    /// The mod's configuration surface, and the bridge between BepInEx and the pure logic layer.
    /// It is deliberately named <c>PluginConfig</c> rather than <c>Config</c>: from inside
    /// <see cref="Plugin"/> the simple name <c>Config</c> binds to <c>BaseUnityPlugin.Config</c>,
    /// so <c>Config.Init(Config)</c> would not compile.
    /// </summary>
    /// <remarks>
    /// Every section name, key name and default comes from <see cref="ConfigModel"/>; this file
    /// holds no string literal that duplicates one of its constants. The entries are registered
    /// with Jötunn's <see cref="SynchronizationManager"/>, and each is marked admin-only, which is
    /// what makes their values server-authoritative: the server's copy is pushed to every client
    /// and the entries are locked for non-admins. <see cref="ReadInto"/> is the only writer of
    /// <see cref="FeatureFlags"/>; it runs from <c>Plugin.Awake</c> before any patch is applied
    /// and again from the <see cref="SynchronizationManager.OnConfigurationSynchronized"/> handler,
    /// so the snapshot tracks the values Jötunn has just written into the entries.
    /// </remarks>
    public static class PluginConfig
    {
        /// <summary>The plugin's configuration file, as handed in by <see cref="Init"/>.</summary>
        public static ConfigFile File;

        /// <summary>
        /// The shared, resolved snapshot every consumer reads. Created by <see cref="Init"/> and
        /// filled by <see cref="ReadInto"/>; never construct another one.
        /// </summary>
        public static FeatureFlags Flags;

        // One entry per key-name constant in ConfigModel (the ZDO key is a constant, not a
        // setting, and so is deliberately not bound). Kept private: consumers read Flags or the
        // flash-colour accessors below.
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> isDebug;
        private static ConfigEntry<float> maxRange;
        private static ConfigEntry<string> pullItemsKey;
        private static ConfigEntry<string> toggleStorageKey;
        private static ConfigEntry<string> preventModKey;
        private static ConfigEntry<string> fillAllModKey;
        private static ConfigEntry<bool> switchPrevent;
        private static ConfigEntry<bool> enableChests;
        private static ConfigEntry<bool> enableDrawers;
        private static ConfigEntry<bool> leaveOne;
        private static ConfigEntry<bool> pullByDistance;
        private static ConfigEntry<bool> pullFuel;
        private static ConfigEntry<bool> pullOre;
        private static ConfigEntry<bool> pullCooking;
        private static ConfigEntry<string> fuelDisallowTypes;
        private static ConfigEntry<string> oreDisallowTypes;
        private static ConfigEntry<string> resourceString;
        private static ConfigEntry<string> pulledMessage;
        private static ConfigEntry<Color> flashColor;
        private static ConfigEntry<Color> unFlashColor;
        private static ConfigEntry<bool> ignoreShipContainers;
        private static ConfigEntry<bool> ignoreWagonContainers;
        private static ConfigEntry<bool> ignoreWoodChests;
        private static ConfigEntry<bool> ignorePrivateChests;
        private static ConfigEntry<bool> ignoreBlackMetalChests;
        private static ConfigEntry<bool> ignoreReinforcedChests;

        /// <summary>
        /// The colour the resource readout flashes when materials come from storage. A
        /// game-facing accessor: the flash colours are presentation values, deliberately kept off
        /// <see cref="FeatureFlags"/> because the pure logic never reads them.
        /// </summary>
        public static Color FlashColor => flashColor.Value;

        /// <summary>
        /// The colour the resource readout alternates with while flashing. A game-facing
        /// accessor, for the same reason as <see cref="FlashColor"/>.
        /// </summary>
        public static Color UnFlashColor => unFlashColor.Value;

        /// <summary>
        /// Binds every config entry and registers the file with Jötunn for server-authoritative
        /// synchronisation. Called once, from <c>Plugin.Awake</c>.
        /// </summary>
        public static void Init(ConfigFile config)
        {
            File = config;
            Flags = new FeatureFlags();

            modEnabled = Bind(config, ConfigModel.SectionServer, ConfigModel.ModEnabled, true,
                "Enable this mod.");
            maxRange = Bind(config, ConfigModel.SectionServer, ConfigModel.MaxRange, ConfigModel.DefaultMaxRange,
                "The maximum distance at which storage is used. Measured from the player, or from the station for the fuel, ore and cooking features.");
            enableChests = Bind(config, ConfigModel.SectionServer, ConfigModel.EnableChests, ConfigModel.DefaultEnableChests,
                "Pull from vanilla chests.");
            enableDrawers = Bind(config, ConfigModel.SectionServer, ConfigModel.EnableDrawers, ConfigModel.DefaultEnableDrawers,
                "Pull from KG Item Drawers drawers.");
            leaveOne = Bind(config, ConfigModel.SectionServer, ConfigModel.LeaveOne, ConfigModel.DefaultLeaveOne,
                "Always leave the last item of each type in the source.");
            pullByDistance = Bind(config, ConfigModel.SectionServer, ConfigModel.PullByDistance, ConfigModel.DefaultPullByDistance,
                "Pull from the nearest storage first.");
            pullFuel = Bind(config, ConfigModel.SectionServer, ConfigModel.PullFuel, true,
                "Let the mod feed fuel to fireplaces and smelters.");
            pullOre = Bind(config, ConfigModel.SectionServer, ConfigModel.PullOre, true,
                "Let the mod feed ore to smelters.");
            pullCooking = Bind(config, ConfigModel.SectionServer, ConfigModel.PullCooking, true,
                "Let the mod feed cooking ingredients to cooking stations.");
            fuelDisallowTypes = Bind(config, ConfigModel.SectionServer, ConfigModel.FuelDisallowTypes, "RoundLog,FineWood",
                "Item prefab names that may not be used as fuel, comma-separated.");
            oreDisallowTypes = Bind(config, ConfigModel.SectionServer, ConfigModel.OreDisallowTypes, "RoundLog,FineWood",
                "Item prefab names that may not be used as ore, comma-separated.");
            resourceString = Bind(config, ConfigModel.SectionServer, ConfigModel.ResourceString, "{0}/{1}",
                "Format used for the required/available resource readout; {0} is available and {1} is required. Empty leaves it at the default.");
            pulledMessage = Bind(config, ConfigModel.SectionServer, ConfigModel.PulledMessage, "Pulled items to inventory",
                "HUD message shown after pulling items into the inventory. Empty disables it.");
            flashColor = Bind(config, ConfigModel.SectionServer, ConfigModel.FlashColor, Color.yellow,
                "Colour the resource readout flashes when the materials come from storage.");
            unFlashColor = Bind(config, ConfigModel.SectionServer, ConfigModel.UnFlashColor, Color.white,
                "Colour the resource readout alternates with while flashing. Set both colours equal to disable the flash.");
            ignoreShipContainers = Bind(config, ConfigModel.SectionServer, ConfigModel.IgnoreShipContainers, false,
                "Ignore chests aboard ships.");
            ignoreWagonContainers = Bind(config, ConfigModel.SectionServer, ConfigModel.IgnoreWagonContainers, false,
                "Ignore chests aboard carts.");
            ignoreWoodChests = Bind(config, ConfigModel.SectionServer, ConfigModel.IgnoreWoodChests, false,
                "Ignore wood chests.");
            ignorePrivateChests = Bind(config, ConfigModel.SectionServer, ConfigModel.IgnorePrivateChests, false,
                "Ignore private chests.");
            ignoreBlackMetalChests = Bind(config, ConfigModel.SectionServer, ConfigModel.IgnoreBlackMetalChests, false,
                "Ignore black metal chests.");
            ignoreReinforcedChests = Bind(config, ConfigModel.SectionServer, ConfigModel.IgnoreReinforcedChests, false,
                "Ignore reinforced chests.");

            pullItemsKey = Bind(config, ConfigModel.SectionKeys, ConfigModel.PullItemsKey, "left ctrl",
                "Held key that pulls a recipe's ingredients into the inventory. Use a Unity key name.");
            toggleStorageKey = Bind(config, ConfigModel.SectionKeys, ConfigModel.ToggleStorageKey, string.Empty,
                "Key that toggles a hovered storage on or off. Use a Unity key name; empty leaves the toggle unbound.");
            preventModKey = Bind(config, ConfigModel.SectionKeys, ConfigModel.PreventModKey, ConfigModel.DefaultPreventModKey,
                "Held key that suppresses this mod's pulling. Use a Unity key name.");
            fillAllModKey = Bind(config, ConfigModel.SectionKeys, ConfigModel.FillAllModKey, ConfigModel.DefaultFillAllModKey,
                "Held key that fills a station to its maximum instead of a single item. Use a Unity key name.");
            switchPrevent = Bind(config, ConfigModel.SectionKeys, ConfigModel.SwitchPrevent, ConfigModel.DefaultSwitchPrevent,
                "If true, holding the prevent key enables this mod's pulling instead of suppressing it.");

            isDebug = Bind(config, ConfigModel.SectionDebug, ConfigModel.IsDebug, true,
                "Print debug messages to the log.");

            // Jötunn writes the server's values into these entries after Init has run and then
            // raises this event. Re-reading the snapshot there is what actually makes the values
            // server-authoritative; without it a client would keep its own local file's values.
            // Wired before the registration call so the refresh stays in place independently of it.
            SynchronizationManager.OnConfigurationSynchronized += RefreshFlags;

            SynchronizationManager.Instance.RegisterCustomConfig(config);
        }

        /// <summary>
        /// Copies every entry's current value into <paramref name="flags"/>. The only writer of a
        /// <see cref="FeatureFlags"/> instance; called from <c>Plugin.Awake</c> before any patch,
        /// and again from <see cref="RefreshFlags"/> whenever Jötunn replaces the entries' values.
        /// The two flash colours are intentionally not copied — they are presentation values
        /// exposed through <see cref="FlashColor"/> and <see cref="UnFlashColor"/> instead.
        /// </summary>
        public static void ReadInto(FeatureFlags flags)
        {
            flags.ModEnabled = modEnabled.Value;
            flags.IsDebug = isDebug.Value;

            flags.MaxRange = maxRange.Value;

            flags.PullItemsKey = pullItemsKey.Value;
            flags.ToggleStorageKey = toggleStorageKey.Value;
            flags.PreventModKey = preventModKey.Value;
            flags.FillAllModKey = fillAllModKey.Value;
            flags.SwitchPrevent = switchPrevent.Value;

            flags.EnableChests = enableChests.Value;
            flags.EnableDrawers = enableDrawers.Value;
            flags.LeaveOne = leaveOne.Value;
            flags.PullByDistance = pullByDistance.Value;

            flags.PullFuel = pullFuel.Value;
            flags.PullOre = pullOre.Value;
            flags.PullCooking = pullCooking.Value;
            flags.FuelDisallowTypes = fuelDisallowTypes.Value;
            flags.OreDisallowTypes = oreDisallowTypes.Value;

            flags.ResourceString = resourceString.Value;
            flags.PulledMessage = pulledMessage.Value;

            flags.IgnoreShipContainers = ignoreShipContainers.Value;
            flags.IgnoreWagonContainers = ignoreWagonContainers.Value;
            flags.IgnoreWoodChests = ignoreWoodChests.Value;
            flags.IgnorePrivateChests = ignorePrivateChests.Value;
            flags.IgnoreBlackMetalChests = ignoreBlackMetalChests.Value;
            flags.IgnoreReinforcedChests = ignoreReinforcedChests.Value;
        }

        /// <summary>
        /// Jötunn's handler for the configuration-synchronised event. By the time it fires the
        /// entries hold the server's values — the initial push on connect, or a later change — so
        /// re-reading them into <see cref="Flags"/> is what makes the snapshot server-authoritative.
        /// <see cref="Flags"/> is created in <see cref="Init"/> and every property is assigned
        /// synchronously here, so a patch can never observe it null or half-populated.
        /// </summary>
        private static void RefreshFlags(object sender, ConfigurationSynchronizationEventArgs e)
        {
            ReadInto(Flags);
        }

        /// <summary>
        /// Binds one entry. Every entry is marked <c>IsAdminOnly</c>, which Jötunn requires for an
        /// entry to be synchronised, and which is what locks it for non-admins.
        /// </summary>
        private static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T defaultValue, string description)
        {
            return config.Bind(section, key, defaultValue,
                new ConfigDescription(description, null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }
    }
}
