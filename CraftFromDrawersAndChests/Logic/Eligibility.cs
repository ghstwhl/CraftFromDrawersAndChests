namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// One candidate pull source, flattened to plain values so the decision logic stays free of
    /// UnityEngine/Valheim types. It is built at the edge, where the game objects live, and
    /// passed by <c>in</c> (read-only reference) to <see cref="Eligibility.IsEligible"/>.
    /// <para>
    /// There is deliberately no "in use" / "open" field: unlike CraftFromContainers, this mod
    /// treats a chest that another player has open as a valid source, so there is nothing to
    /// represent.
    /// </para>
    /// </summary>
    public readonly struct StorageCandidate
    {
        public readonly StorageKind Kind;
        public readonly Vec3 Position;
        public readonly string ObjectName;
        public readonly bool ParentIsShip;
        public readonly bool ParentIsWagon;
        public readonly bool ToggledOff;
        public readonly bool HasAccess;
        public readonly bool HasInventory;

        /// <param name="objectName">The container's <c>gameObject.name</c>; may be null when a
        /// component is missing, in which case <see cref="Eligibility.IsEligible"/> treats it as
        /// matching no name-based chest rule.</param>
        public StorageCandidate(
            StorageKind kind,
            Vec3 position,
            string objectName,
            bool parentIsShip,
            bool parentIsWagon,
            bool toggledOff,
            bool hasAccess,
            bool hasInventory)
        {
            Kind = kind;
            Position = position;
            ObjectName = objectName;
            ParentIsShip = parentIsShip;
            ParentIsWagon = parentIsWagon;
            ToggledOff = toggledOff;
            HasAccess = hasAccess;
            HasInventory = hasInventory;
        }
    }

    /// <summary>
    /// The single predicate that decides whether a storage is a candidate pull source: in range
    /// of the origin, enabled for its kind, not switched off, and — for a chest only — accessible,
    /// carrying an inventory and not excluded by the chest-type rules. Pure logic, so it must
    /// never reference UnityEngine, Valheim, BepInEx or Jotunn.
    /// <para>
    /// Range is compared on squared distances; <see cref="Vec3.SqrDistanceTo"/> avoids a square
    /// root per candidate. Distance equal to the range counts as in range.
    /// </para>
    /// </summary>
    public static class Eligibility
    {
        public static bool IsEligible(
            in StorageCandidate c,
            Vec3 origin,
            float range,
            bool chestsEnabled,
            bool drawersEnabled,
            ChestTypeRules chestRules)
        {
            if (c.ToggledOff)
                return false;

            float sqrRange = range * range;
            if (c.Position.SqrDistanceTo(origin) > sqrRange)
                return false;

            // A drawer is a ZNetView, not a Container: it has no chest type, no access check and
            // no Container inventory, so only its kind being enabled matters beyond the checks above.
            if (c.Kind == StorageKind.Drawer)
                return drawersEnabled;

            if (!chestsEnabled || !c.HasAccess || !c.HasInventory)
                return false;

            // Guard at the call site rather than in Task 2's file: IsExcluded throws on a null name
            // when a name rule is enabled, and candidates are assembled from possibly-missing
            // components. A null name selects no name-based rule.
            string objectName = c.ObjectName ?? string.Empty;
            return !chestRules.IsExcluded(objectName, c.ParentIsShip, c.ParentIsWagon);
        }
    }
}
