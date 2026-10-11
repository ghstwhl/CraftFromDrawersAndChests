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
        public const string Version = "0.1.0";
        public const string ThunderstoreTeam = "NorCal_Nerds";
        public const string ThunderstorePackage = "CraftFromDrawersAndChests";
        public const string GitHubRepo = "ghstwhl/CraftFromDrawersAndChests";
    }
}
