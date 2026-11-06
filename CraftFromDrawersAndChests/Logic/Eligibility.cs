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
    /// Why a storage that holds an item cannot be used for it, when the reason is a <b>lock</b> rather
    /// than a disqualification. Consumed by <see cref="AvailabilityHint" /> (via
    /// <see cref="Eligibility.TryBlockReason" />), never by the pull path.
    /// </summary>
    public enum StorageBlockReason
    {
        /// <summary>Not blocked: either usable, or not a storage the player would call locked.</summary>
        None = 0,

        /// <summary>The player switched this storage off with the per-storage toggle.</summary>
        SwitchedOff,

        /// <summary>
        /// The storage refuses this player — another player's private chest, or a chest a ward denies
        /// (<c>Container.CheckAccess</c>, or the guard-stone check for the chest's position).
        /// </summary>
        NoAccess
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
    /// <para>
    /// <see cref="IsEligibleButForSwitch"/> is the same decision with the per-storage off switch
    /// taken out, and it exists for <b>diagnostics only</b>: a switched-off storage is filtered out
    /// before the pullable list is built, so it can never appear there, and the debug readout would
    /// otherwise report "nothing holds it" with no way to say that the item is sitting in a storage
    /// the player switched off. It must never be used to decide what a pull may take from.
    /// </para>
    /// </summary>
    public static class Eligibility
    {
        /// <summary>
        /// Whether the storage may serve a pull: <see cref="IsEligibleButForSwitch"/> and not
        /// switched off. This is the only predicate the pull path may use.
        /// </summary>
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

            return IsEligibleButForSwitch(c, origin, range, chestsEnabled, drawersEnabled, chestRules);
        }

        /// <summary>
        /// Whether the storage would be a pull source if its per-storage off switch were not set.
        /// Every other exclusion still applies, so a loose container, an out-of-range storage, an
        /// inaccessible one or one whose kind is disabled is <c>false</c> here too — the switch is
        /// the only reason this answer can differ from <see cref="IsEligible"/>.
        /// <para>
        /// Diagnostic only. See the type remarks: the debug readout uses it to name a storage that
        /// holds the requirement while being switched off, which is otherwise indistinguishable from
        /// the requirement simply not being in storage.
        /// </para>
        /// </summary>
        public static bool IsEligibleButForSwitch(
            in StorageCandidate c,
            Vec3 origin,
            float range,
            bool chestsEnabled,
            bool drawersEnabled,
            ChestTypeRules chestRules)
        {
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
        /// Whether the storage is in range, of an enabled kind, otherwise well-formed as storage, and
        /// excluded <b>only</b> because it is locked — and if so, which lock.
        /// <para>
        /// This is the question behind the crafting readout's "nearby but blocked" hint: a true answer
        /// means <i>"this thing is right here, it holds your item, and it will not give it to you"</i>.
        /// Every other exclusion answers false, because it is not a lock: a storage that is out of range
        /// or whose kind is disabled was never a candidate, a loose container or an ignored chest type is
        /// excluded by configuration, and reporting any of them would turn "you have none nearby" into
        /// noise.
        /// </para>
        /// <para>
        /// Usable storage answers false too — it is counted as available instead — so the hint and the
        /// availability count can never describe the same source twice.
        /// </para>
        /// <para>
        /// The switch is checked before access, so a chest that is both switched off and denied reports
        /// <see cref="StorageBlockReason.SwitchedOff" />: the player's own action is the more useful
        /// explanation, and one reason keeps the HUD line short.
        /// </para>
        /// </summary>
        /// <param name="c">The candidate, flattened at the game edge.</param>
        /// <param name="origin">The pull origin the range is measured from.</param>
        /// <param name="range">The configured maximum range; zero or less means unlimited.</param>
        /// <param name="chestsEnabled">Whether vanilla chests may be used at all.</param>
        /// <param name="drawersEnabled">Whether drawers may be used at all.</param>
        /// <param name="chestRules">The chest-type exclusions; a config exclusion is not a lock.</param>
        /// <param name="reason">The lock, when this returns true; otherwise <see cref="StorageBlockReason.None" />.</param>
        public static bool TryBlockReason(
            in StorageCandidate c,
            Vec3 origin,
            float range,
            bool chestsEnabled,
            bool drawersEnabled,
            ChestTypeRules chestRules,
            out StorageBlockReason reason)
        {
            reason = StorageBlockReason.None;

            // Usable storage is availability, not a block; the readout counts it separately.
            if (IsEligible(c, origin, range, chestsEnabled, drawersEnabled, chestRules))
                return false;

            // Not-a-candidate first: these are not locks, and an out-of-range storage must never
            // produce a hint naming it.
            if (!IsWithinRange(range, c.Position.SqrDistanceTo(origin)))
                return false;

            if (c.Kind == StorageKind.Drawer)
            {
                // A drawer carries no access rule in this mod (the mod passes hasAccess=false for one),
                // so the switch is the only lock it can have.
                if (!drawersEnabled || !c.ToggledOff)
                    return false;

                reason = StorageBlockReason.SwitchedOff;
                return true;
            }

            if (!chestsEnabled || !c.HasInventory || !c.IsInsidePiece)
                return false;

            if (chestRules.IsExcluded(c.ObjectName ?? string.Empty, c.ParentIsShip, c.ParentIsWagon))
                return false;

            if (c.ToggledOff)
            {
                reason = StorageBlockReason.SwitchedOff;
                return true;
            }

            if (!c.HasAccess || !c.PassesGuardStone)
            {
                reason = StorageBlockReason.NoAccess;
                return true;
            }

            return false;
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
