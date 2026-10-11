namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// The two kinds of storage the mod can pull from. A chest is a vanilla
    /// <c>Valheim.Container</c>; a drawer is a KG Item Drawers <c>ZNetView</c> and is not a
    /// <c>Container</c>. Pure logic, so it must never reference UnityEngine, Valheim, BepInEx
    /// or Jotunn.
    /// </summary>
    public enum StorageKind
    {
        Chest,
        Drawer,
    }
}
