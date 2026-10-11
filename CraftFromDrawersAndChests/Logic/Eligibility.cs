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
        public readonly bool IsInsidePiece;
        public readonly bool PassesGuardStone;

        /// <param name="objectName">The container's <c>gameObject.name</c>; may be null when a
        /// component is missing, in which case <see cref="Eligibility.IsEligible"/> treats it as
        /// matching no name-based chest rule.</param>
        /// <param name="isInsidePiece">True when the container is part of a built <c>Piece</c>; a
        /// loose container is never eligible. A drawer is not a <c>Piece</c> child, so callers pass
        /// false for a drawer and the predicate ignores it.</param>
        /// <param name="passesGuardStone">True when the container's guard-stone privacy (if any)
        /// grants access at its position. A drawer has no guard stone, so callers pass false for a
        /// drawer and the predicate ignores it.</param>
        public StorageCandidate(
            StorageKind kind,
            Vec3 position,
            string objectName,
            bool parentIsShip,
            bool parentIsWagon,
            bool toggledOff,
            bool hasAccess,
            bool hasInventory,
            bool isInsidePiece,
            bool passesGuardStone)
        {
            Kind = kind;
            Position = position;
            ObjectName = objectName;
            ParentIsShip = parentIsShip;
            ParentIsWagon = parentIsWagon;
            ToggledOff = toggledOff;
            HasAccess = hasAccess;
            HasInventory = hasInventory;
            IsInsidePiece = isInsidePiece;
            PassesGuardStone = passesGuardStone;
        }
    }

    /// <summary>
    /// The single predicate that decides whether a storage is a candidate pull source: in range
    /// of the origin, enabled for its kind, not switched off, and — for a chest only — inside a
    /// <c>Piece</c>, granted by its guard stone, accessible, carrying an inventory and not excluded
    /// by the chest-type rules. Pure logic, so it must never reference UnityEngine, Valheim, BepInEx
    /// or Jotunn.
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

            if (!IsWithinRange(range, c.Position.SqrDistanceTo(origin)))
                return false;

            // A drawer is a ZNetView, not a Container: it has no chest type, no Piece parent, no
            // guard stone, no access check and no Container inventory, so only its kind being enabled
            // matters beyond the checks above.
            if (c.Kind == StorageKind.Drawer)
                return drawersEnabled;

            if (!chestsEnabled || !c.HasAccess || !c.HasInventory)
                return false;

            // Chest-only, like the checks above: a loose container is skipped, and a guard-stone
            // container is skipped where PrivateArea denies access. A drawer never reaches here.
            if (!c.IsInsidePiece || !c.PassesGuardStone)
                return false;

            // Guard at the call site rather than in Task 2's file: IsExcluded throws on a null name
            // when a name rule is enabled, and candidates are assembled from possibly-missing
            // components. A null name selects no name-based rule.
            string objectName = c.ObjectName ?? string.Empty;
            return !chestRules.IsExcluded(objectName, c.ParentIsShip, c.ParentIsWagon);
        }

        /// <summary>
        /// Whether a squared distance counts as being inside <paramref name="range"/>.
        /// <para>
        /// A non-positive range means <b>unlimited</b>, not zero-scope. This is CraftFromContainers'
        /// behaviour (<c>maxRange.Value <= 0 || Vector3.Distance(center, ...) < maxRange.Value</c>,
        /// CFC 4.0.7 line 173) and the owner's ruling after preparing a test package: a player carrying
        /// a <c>0</c> over from CFC otherwise gets a silently dead mod, with nothing in the log to say
        /// why, because a zero radius excludes every container that is not exactly at the origin.
        /// </para>
        /// <para>
        /// Both range consumers read this one predicate - <see cref="IsEligible"/> for chests and
        /// drawers, and <c>DrawerApi</c> for the drawer query's own filter - so a non-positive range
        /// cannot mean unlimited for one kind of storage and nothing for the other. Squared distances
        /// are compared, so no square root is needed, and a distance exactly equal to the range counts
        /// as inside.
        /// </para>
        /// </summary>
        /// <param name="range">The configured maximum range; zero or less means unlimited.</param>
        /// <param name="sqrDistance">The squared distance from the pull origin to the candidate.</param>
        public static bool IsWithinRange(float range, float sqrDistance)
        {
            return range <= 0f || sqrDistance <= range * range;
        }
    }
}
