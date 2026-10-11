using System;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// Decides whether a container is excluded as a pull source because of its chest type:
    /// a ship or wagon parent, or one of the four chest prefab kinds. Pure logic — it must
    /// never reference UnityEngine, Valheim, BepInEx or Jotunn so it can be unit-tested
    /// outside the game.
    /// </summary>
    public class ChestTypeRules
    {
        public bool IgnoreShipContainers;
        public bool IgnoreWagonContainers;
        public bool IgnoreWoodChests;
        public bool IgnorePrivateChests;
        public bool IgnoreBlackMetalChests;
        public bool IgnoreReinforcedChests;

        // Prefixes copied from CraftFromContainers 4.0.7. They are disjoint: the reinforced
        // prefix ends in '(' where the others have '_', so "piece_chest_wood(Clone)" does not
        // start with "piece_chest(".
        private const string WoodChestPrefix = "piece_chest_wood(";
        private const string PrivateChestPrefix = "piece_chest_private(";
        private const string BlackMetalChestPrefix = "piece_chest_blackmetal(";
        private const string ReinforcedChestPrefix = "piece_chest(";

        /// <summary>
        /// Returns true when any enabled rule matches this container. <paramref name="objectName"/>
        /// is the container's <c>gameObject.name</c>; ship and wagon are decided by the parent flags.
        /// </summary>
        public bool IsExcluded(string objectName, bool parentIsShip, bool parentIsWagon)
        {
            if (IgnoreShipContainers && parentIsShip)
                return true;
            if (IgnoreWagonContainers && parentIsWagon)
                return true;
            if (IgnoreWoodChests && objectName.StartsWith(WoodChestPrefix, StringComparison.Ordinal))
                return true;
            if (IgnorePrivateChests && objectName.StartsWith(PrivateChestPrefix, StringComparison.Ordinal))
                return true;
            if (IgnoreBlackMetalChests && objectName.StartsWith(BlackMetalChestPrefix, StringComparison.Ordinal))
                return true;
            if (IgnoreReinforcedChests && objectName.StartsWith(ReinforcedChestPrefix, StringComparison.Ordinal))
                return true;
            return false;
        }
    }
}
