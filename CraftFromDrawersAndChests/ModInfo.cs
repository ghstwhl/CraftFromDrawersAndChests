namespace CraftFromDrawersAndChests
{
    /// <summary>
    /// The mod's identity, in one place. <c>tools/Build.ps1</c> reads
    /// <see cref="Version"/>, <see cref="ThunderstoreTeam"/> and
    /// <see cref="ThunderstorePackage"/> out of this file by regular expression, so the
    /// declarations must keep the exact form:
    /// <code>public const string &lt;Name&gt; = "&lt;value&gt;";</code>
    /// </summary>
    public static class ModInfo
    {
        public const string ModName = "CraftFromDrawersAndChests";
        public const string ModGuid = "NorCal_Nerds.CraftFromDrawersAndChests";
        public const string Version = "1.0.8";
        public const string ThunderstoreTeam = "NorCal_Nerds";
        public const string ThunderstorePackage = "CraftFromDrawersAndChests";
        public const string GitHubRepo = "ghstwhl/CraftFromDrawersAndChests";

        /// <summary>
        /// The console command that reports this mod's storage state. Named after the mod, the way
        /// CraftFromContainers names its own command (<c>craftfromcontainers reset</c>, CFC 4.0.7
        /// lines 1498-1503); the mod name is long to type, so <see cref="ConsoleCommandAlias"/> is
        /// accepted too. Matched case-insensitively, as CFC matches its own.
        /// </summary>
        public const string ConsoleCommand = "craftfromdrawersandchests";

        /// <summary>The mod's short form, the same acronym the per-storage ZDO key uses (<c>CFDAC_NoPull</c>).</summary>
        public const string ConsoleCommandAlias = "cfdac";
    }
}
