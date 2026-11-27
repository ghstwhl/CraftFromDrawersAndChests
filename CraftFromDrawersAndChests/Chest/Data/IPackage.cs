namespace CraftFromDrawersAndChests.Chest {
    public interface IPackage {
        ZPackage WriteToPackage();

        // DIVERGENCE FROM UPSTREAM: declared unconditionally (upstream wrapped it in #if DEBUG) so the
        // dump can be enabled at runtime by Log.LogDebug's own switch, not by which build was shipped.
        void PrintDebug();
    }
}
