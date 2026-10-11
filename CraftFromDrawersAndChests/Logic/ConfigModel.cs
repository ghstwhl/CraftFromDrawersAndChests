using System.Collections.Generic;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The single source of truth for the BepInEx configuration surface: the section names,
    /// the user-visible key names, and the defaults the game-facing <c>Config</c> layer binds.
    /// Pure logic, so it must never reference <c>BepInEx.Configuration</c>; the
    /// <c>ConfigEntry</c> wiring (Task 8) lives in the game-facing layer and reads these
    /// constants rather than writing string literals of its own.
    /// </summary>
    public static class ConfigModel
    {
        // Sections: the user-visible grouping labels in the ConfigurationManager window.
        public const string SectionServer = "1 - Server";
        public const string SectionKeys = "2 - Keys";
        public const string SectionDebug = "9 - Debug";

        // Key names: the user-visible keys in the generated config file.
        public const string MaxRange = "Maximum range";
        public const string PullItemsKey = "Pull items key";
        public const string ToggleStorageKey = "Toggle storage key";
        public const string PreventModKey = "Prevent mod key";
        public const string FillAllModKey = "Fill all mod key";
        public const string SwitchPrevent = "Switch prevent";
        public const string EnableChests = "Enable chests";
        public const string EnableDrawers = "Enable drawers";
        public const string LeaveOne = "Leave one";
        public const string PullByDistance = "Pull by distance";
        public const string PullFuel = "Pull fuel";
        public const string PullOre = "Pull ore";
        public const string PullCooking = "Pull cooking";
        public const string FuelDisallowTypes = "Fuel disallow types";
        public const string OreDisallowTypes = "Ore disallow types";
        public const string ResourceString = "Resource string";
        public const string PulledMessage = "Pulled message";
        public const string FlashColor = "Flash color";
        public const string UnFlashColor = "Unflash color";
        public const string IgnoreShipContainers = "Ignore ship containers";
        public const string IgnoreWagonContainers = "Ignore wagon containers";
        public const string IgnoreWoodChests = "Ignore wood chests";
        public const string IgnorePrivateChests = "Ignore private chests";
        public const string IgnoreBlackMetalChests = "Ignore black metal chests";
        public const string IgnoreReinforcedChests = "Ignore reinforced chests";
        public const string ModEnabled = "Enable mod";
        public const string IsDebug = "Debug";

        // Defaults: the values the ConfigEntry definitions use, so no default is written twice.
        public const float DefaultMaxRange = 4f;
        public const bool DefaultEnableChests = true;
        public const bool DefaultEnableDrawers = true;
        public const bool DefaultLeaveOne = false;
        public const bool DefaultPullByDistance = false;

        // The two held-modifier keys default to CFC 4.0.7's values. switchPrevent stays off, so out
        // of the box holding the prevent key suppresses pulling rather than enabling it.
        public const string DefaultPreventModKey = "left alt";
        public const string DefaultFillAllModKey = "left shift";
        public const bool DefaultSwitchPrevent = false;

        /// <summary>
        /// The per-storage off-switch key. A single boolean stored under this key on the
        /// storage's own ZDO marks it "off" for everyone — the switch is a shared world
        /// property, not a per-player one. Defined here, ahead of the sources (Tasks 9/10) and
        /// the toggle (Task 12), so all three share one literal instead of re-declaring it.
        /// </summary>
        public const string ZdoKey = "CFDAC_NoPull";

        /// <summary>
        /// Splits a comma-separated list, trimming each entry and dropping empty entries. Total:
        /// null input, empty input, and input that trims away to nothing all return an empty
        /// array, never null.
        /// </summary>
        public static string[] ParseList(string csv)
        {
            if (string.IsNullOrEmpty(csv))
                return new string[0];

            string[] parts = csv.Split(',');
            var kept = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim();
                if (trimmed.Length != 0)
                    kept.Add(trimmed);
            }
            return kept.ToArray();
        }
    }
}
