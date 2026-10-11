namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// What a build-mode attack press should do, once CraftFromContainers' per-frame guards and its
    /// attack-input test have both passed. Every decision the game-facing
    /// <c>Patches.PullKeyPatches</c> prefix makes from that point on is one of these three, so the
    /// prefix has no branch of its own to get wrong.
    /// </summary>
    public enum BuildPullDecision
    {
        /// <summary>
        /// The press is vanilla's: the prefix returns <c>true</c> and the game runs as if this mod were
        /// not installed. CraftFromContainers reaches this for a repair piece (CFC line 1111-1112).
        /// </summary>
        LetVanillaHandle,

        /// <summary>
        /// The press is consumed — the prefix returns <c>false</c> — but <b>nothing is pulled</b>. The
        /// piece is therefore never placed and no item moves, which is the safe answer when the pull
        /// could not be trusted to complete: no piece is selected, no placement ghost exists, or the
        /// ghost's placement status is not <c>Valid</c>. CraftFromContainers reaches the same
        /// <c>return false</c> for those three cases (CFC lines 1108-1109, 1113, 1116, 1123) but shows
        /// no feedback; this mod keeps the swallow and reports a reason only on the <see cref="Pull"/>
        /// path below.
        /// </summary>
        RefuseWithoutPull,

        /// <summary>
        /// The press is consumed and the selected piece's materials are pulled into the player's
        /// inventory; the piece is <b>never</b> placed. CraftFromContainers' pull branch (CFC lines
        /// 1116-1120).
        /// </summary>
        Pull
    }

    /// <summary>
    /// The pure decisions behind the build-mode pull key: whether an attack press is ours at all, what
    /// to do with it, and the placement-status test that guards the pull. A faithful port of
    /// CraftFromContainers 4.0.7's <c>UpdatePlacement_Patch</c> (CFC <c>BepInExPlugin.cs</c>
    /// lines 1096-1125), which pulls a <i>building piece's</i> materials with the same key that pulls a
    /// recipe's ingredients — CFC's own <c>pullItemsKey</c> description reads "crafting <b>or
    /// building</b>".
    /// <para>
    /// Nothing here touches Unity, Valheim, BepInEx or Jotunn: the game-facing patch reads the live
    /// input, the placement state and the configuration snapshot and feeds them in, so the guard chain
    /// that decides whether a press is a pull can be tested outside the game.
    /// </para>
    /// </summary>
    public static class BuildPullRules
    {
        /// <summary>
        /// The per-frame test for whether an attack press is this mod's to consume at all — the port of
        /// CraftFromContainers' one guard line (CFC line 1102):
        /// <c>!modEnabled || !AllowByKey() || !CheckKeyHeld(pullItemsKey) || !__instance.InPlaceMode()
        /// || !takeInput || Hud.IsPieceSelectionVisible()</c>. The key half is
        /// <see cref="PullKeyRules.ShouldPull"/>, the very gate the ctrl-click pull and the
        /// storage-aware requirement/consume patches already share, so the prevent key cannot mean one
        /// thing here and another there.
        /// <para>
        /// Every term is a reason to keep hands off: the mod is off, the prevent gate refuses, the pull
        /// key is not held, the player is not in placement mode, this frame is not taking player input
        /// (the pause menu, a cutscene), or the build menu's piece selection is open.
        /// </para>
        /// </summary>
        /// <param name="modEnabled">The resolved <c>"Enabled"</c> setting.</param>
        /// <param name="pullKeyHeld">Whether the configured pull key is held this frame.</param>
        /// <param name="preventKeyHeld">Whether the configured prevent key is held this frame.</param>
        /// <param name="switchPrevent">The resolved <c>"Switch prevent"</c> setting.</param>
        public static bool ShouldIntercept(
            bool modEnabled,
            bool pullKeyHeld,
            bool preventKeyHeld,
            bool switchPrevent,
            bool inPlaceMode,
            bool takeInput,
            bool pieceSelectionVisible)
        {
            return modEnabled
                && PullKeyRules.ShouldPull(pullKeyHeld, preventKeyHeld, switchPrevent)
                && inPlaceMode
                && takeInput
                && !pieceSelectionVisible;
        }

        /// <summary>
        /// Whether the placement ghost's status lets a pull happen. CraftFromContainers reflects the
        /// private <c>Player.m_placementStatus</c> field and compares the raw value to <c>0</c>
        /// (CFC lines 1115-1116), and the game's nested <c>Player.PlacementStatus</c> enum has
        /// <c>Valid = 0</c> with every other status — <c>Invalid</c>, <c>BlockedbyPlayer</c>,
        /// <c>NoBuildZone</c>, <c>MoreSpace</c>, <c>WrongBiome</c> and the rest — a reason not to
        /// place. The comparison is done here on the bare number so the port does not depend on a
        /// nested enum the game may not have public.
        /// <para>
        /// This is why the pull only fires on a ghost that reads as placeable, and why the build path
        /// needs a storage-aware material gate to reach it at all: the status the game writes is the
        /// answer <c>Player.HaveRequirements(piece, CanBuild)</c> gave, which this mod's
        /// <c>Patches.CraftPatches</c> postfix already makes count chests and drawers. Without that
        /// gate the ghost of any piece whose materials were not on the player would stay non-valid and
        /// the pull could never run.
        /// </para>
        /// </summary>
        /// <param name="placementStatus">
        /// The raw <c>Player.m_placementStatus</c> value; a negative value means the field could not be
        /// read at all and is deliberately <b>not</b> valid, so an unrecognised game version fails
        /// closed (nothing pulled, nothing placed) rather than placing a piece nothing paid for.
        /// </param>
        public static bool IsPlacementValid(int placementStatus)
        {
            return placementStatus == 0;
        }

        /// <summary>
        /// The numeric placement status from the raw value of <c>Player.m_placementStatus</c>, or the
        /// <c>-1</c> sentinel when it cannot be read. This is the reader half of the placement gate,
        /// kept pure so it can be pinned by a test: the game-facing patch resolves the private field by
        /// reflection (as CraftFromContainers does at its line 1115) and hands the boxed value here.
        /// <para>
        /// A null value means the field was absent (a later game version renamed it), and an
        /// unconvertible value means it was not a number; both read as <c>-1</c>, which
        /// <see cref="IsPlacementValid"/> treats as <b>not</b> valid. So a missing or changed field
        /// fails closed — the press is swallowed with nothing pulled and nothing placed — rather than
        /// silently pulling on an unknown layout. That the sentinel survives the round trip is exactly
        /// the half that would otherwise degrade silently, which is why it has its own test.
        /// </para>
        /// </summary>
        /// <param name="fieldValue">The boxed field value, or null when the field could not be read.</param>
        public static int PlacementStatusOf(object fieldValue)
        {
            if (fieldValue == null)
                return -1;

            try
            {
                return System.Convert.ToInt32(fieldValue);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// The decision for an attack press that <see cref="ShouldIntercept"/> already accepted. Ports
        /// CraftFromContainers' branch order exactly (CFC lines 1108-1123), which is significant
        /// because the branches disagree: a repair piece is handed to vanilla, whereas a missing piece,
        /// a missing ghost and a non-valid placement are all swallowed without a pull.
        /// </summary>
        /// <param name="pieceSelected">Whether the build piece table currently has a selected piece.</param>
        /// <param name="repairPiece">Whether that piece is a repair piece (<c>Piece.m_repairPiece</c>).</param>
        /// <param name="placementGhostPresent">Whether the player's placement ghost exists (<c>Player.m_placementGhost</c>).</param>
        /// <param name="placementValid">Whether the ghost's status is <c>Valid</c>; see <see cref="IsPlacementValid"/>.</param>
        public static BuildPullDecision Decide(
            bool pieceSelected,
            bool repairPiece,
            bool placementGhostPresent,
            bool placementValid)
        {
            if (!pieceSelected)
                return BuildPullDecision.RefuseWithoutPull;

            // A repair piece is not built and consumes nothing, so vanilla keeps the press (CFC 1111-1112).
            if (repairPiece)
                return BuildPullDecision.LetVanillaHandle;

            if (!placementGhostPresent)
                return BuildPullDecision.RefuseWithoutPull;

            if (!placementValid)
                return BuildPullDecision.RefuseWithoutPull;

            return BuildPullDecision.Pull;
        }
    }
}
