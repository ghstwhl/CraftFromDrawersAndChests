namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// A resolved snapshot of every configuration setting, filled in from BepInEx
    /// <c>ConfigEntry</c> values by the game-facing <c>Config</c> layer (Task 8). Deliberately a
    /// plain mutable class with one property per setting and no BepInEx types, so the pure logic
    /// can consume it and the tests can run outside the game. The defaults live in
    /// <see cref="ConfigModel"/>; this type is populated by the config layer, not defaulted here.
    /// </summary>
    /// <remarks>
    /// Single writer: <c>Config.ReadInto</c> (Task 8) is the only code that assigns to these
    /// properties, and it runs from <c>Plugin.Awake</c> before any Harmony patch is applied, so
    /// every patch observes a fully populated snapshot. Because the type is deliberately
    /// non-defaulted, a consumer that skipped that read would see every flag false; consumers must
    /// read the shared instance the config layer fills in, not construct their own.
    /// </remarks>
    public sealed class FeatureFlags
    {
        public bool ModEnabled { get; set; }
        public bool IsDebug { get; set; }

        public float MaxRange { get; set; }

        public string PullItemsKey { get; set; }
        public string ToggleStorageKey { get; set; }
        public string PreventModKey { get; set; }
        public string FillAllModKey { get; set; }
        public bool SwitchPrevent { get; set; }

        public bool EnableChests { get; set; }
        public bool EnableDrawers { get; set; }
        public bool LeaveOne { get; set; }
        public bool PullByDistance { get; set; }

        public bool PullFuel { get; set; }
        public bool PullOre { get; set; }
        public bool PullCooking { get; set; }
        public string FuelDisallowTypes { get; set; }
        public string OreDisallowTypes { get; set; }

        public string ResourceString { get; set; }
        public string PulledMessage { get; set; }

        public bool IgnoreShipContainers { get; set; }
        public bool IgnoreWagonContainers { get; set; }
        public bool IgnoreWoodChests { get; set; }
        public bool IgnorePrivateChests { get; set; }
        public bool IgnoreBlackMetalChests { get; set; }
        public bool IgnoreReinforcedChests { get; set; }
    }
}
