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
    /// holds no string literal that duplicates one of its constants. Each entry is marked
    /// admin-only, which is what makes its value server-authoritative: Jötunn synchronises a
    /// plugin's own config file implicitly, so the server's copy is pushed to every client and the
    /// entries are locked for non-admins (see <see cref="Init"/>). <see cref="ReadInto"/> is the only writer of
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
        /// Binds every config entry. Jötunn synchronises a plugin's own config file implicitly, so
        /// no registration call is needed — see the note below on the one that must never come back.
        /// Called once, from <c>Plugin.Awake</c>.
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
            toggleStorageKey = Bind(config, ConfigModel.SectionKeys, ConfigModel.ToggleStorageKey, "left shift + e",
                "Key or key combination that toggles a hovered storage on or off. Write a combination like \"left shift + e\": every name but the last must be held, and the last is pressed. Empty leaves the toggle unbound. AzuCraftyBoxes uses LeftShift+E for the same feature.");
            preventModKey = Bind(config, ConfigModel.SectionKeys, ConfigModel.PreventModKey, ConfigModel.DefaultPreventModKey,
                "Held key that suppresses this mod's pulling. Use a Unity key name.");
            fillAllModKey = Bind(config, ConfigModel.SectionKeys, ConfigModel.FillAllModKey, ConfigModel.DefaultFillAllModKey,
                "Held key that asks storage for a station's whole remaining capacity instead of a single item, taking as much of it as the storage can supply. Use a Unity key name.");
            switchPrevent = Bind(config, ConfigModel.SectionKeys, ConfigModel.SwitchPrevent, ConfigModel.DefaultSwitchPrevent,
                "If true, holding the prevent key enables this mod's pulling instead of suppressing it.");

            isDebug = Bind(config, ConfigModel.SectionDebug, ConfigModel.IsDebug, true,
                "Print debug messages to the log.");

            // Jötunn writes the server's values into these entries after Init has run and then
            // raises this event. Re-reading the snapshot there is what actually makes the values
            // server-authoritative; without it a client would keep its own local file's values.
            SynchronizationManager.OnConfigurationSynchronized += RefreshFlags;

            // DO NOT add SynchronizationManager.Instance.RegisterCustomConfig(config) back here.
            //
            // That method registers a NON-DEFAULT config file and throws ArgumentException for a
            // plugin's own file, because it "is a default mod config and is already implicitly
            // synchronized" (the documented exception on
            // Jotunn.Managers.SynchronizationManager.RegisterCustomConfig). A plugin's own
            // BaseUnityPlugin.Config IS a default mod config - GetConfigFiles covers "all default
            // and custom config files ... of plugins that have Jotunn as a dependency" - so the
            // call could never be correct: it only ever threw, and a re-throw is not a warning.
            //
            // Its former position, inside Plugin.Awake, is what made Task 15f a total failure: the
            // throw aborted Awake before Harmony.PatchAll(), so not a single patch was applied and
            // every feature was dead, while BepInEx still reported "0 failed" (an exception in a
            // plugin's Awake is logged by Unity, not by the chainloader). Restoring this call would
            // silently disable every feature again. Nothing is lost by removing it: each entry is
            // still bound with IsAdminOnly, which is the attribute Jötunn requires for an entry to
            // be synchronised and locked for non-admins.
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
        /// <para>
        /// Every entry re-reads the whole snapshot when it changes, so an edit made while the game is
        /// running - through the ConfigurationManager (F1), or anything else that writes the entry -
        /// takes effect immediately. Without this the snapshot moved only on Jötunn's server push, so a
        /// setting changed in game appeared to do nothing until the next world load or a
        /// <c>cfdac reset</c>: a configured toggle key still read as unset on the hover line and its key
        /// never fired. Re-reading everything is deliberate - the snapshot is a single object, and a
        /// partly refreshed one would let a patch observe a combination of values that never existed.
        /// </para>
        /// </summary>
        private static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T defaultValue, string description)
        {
            ConfigEntry<T> entry = config.Bind(section, key, defaultValue,
                new ConfigDescription(description, null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            entry.SettingChanged += OnSettingChanged;
            return entry;
        }

        /// <summary>
        /// BepInEx's entry-change handler. The whole snapshot is re-read rather than the one property
        /// that changed; see <see cref="Bind"/> for why.
        /// </summary>
        private static void OnSettingChanged(object sender, System.EventArgs e)
        {
            FeatureFlags flags = Flags;
            if (flags != null)
                ReadInto(flags);
        }
    }
}
