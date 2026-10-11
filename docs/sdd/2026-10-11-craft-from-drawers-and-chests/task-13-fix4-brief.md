# Task 13 — fix round 4

Round 3's R7 is verified: Critical #2 (the destination-capacity hole) is **closed deterministically**.
The reviewer did not take the report's word for it — it decompiled `MultiUserChest` and confirmed the
gate now matches `PossibleDragAmount`'s clamp and `AddItem`'s merge predicate, and independently proved
the building path is gated (`Player.UpdatePlacement` gates on `HaveRequirements(CanBuild)` before
`TryPlacePiece`, and `TryPlacePiece` has exactly two occurrences in the entire Valheim dump: its
definition and that one gated call site).

But the new code added the crafted output to the reservation, and it **under-reserves**. That reopens a
narrower free-craft window in the very dimension round 3 introduced. One Critical, one Important. Both
fixes are small.

## Critical — the crafted output is reserved with `Recipe.GetAmount`, but `DoCrafting` adds more

`CraftPatches.CraftedOutput` returns `piece.GetAmount(...)` as `produced`, and that is what
`DestinationCapacity` reserves room for.

Decompiled `InventoryGui.DoCrafting` does not add `GetAmount`: it computes
`num3 = m_craftRecipe.GetAmount(num, out …, num2)`, then — after the gate, before the add — when the
station has a crafting skill and the stack size allows it, it loops `num2` times and on a
`Random.value < skillFactor * m_craftBonusChance` roll adds `num4` (default 1) to `num3`, and finally
adds `num3`. `m_craftBonusChance` defaults to **0.25**, so this is common, not exotic. The reservation
is therefore **smaller than what will really be added** — under-strict, which is the unsafe direction.

Reachable case: one empty slot, an output stack with free room of exactly `m_amount`, and a shortfall
with no matching stack. The gate places the base output into the output stack (using no slot) and the
shortfall into the slot → yes. If the bonus fires, `AddItem` spills 1 into the slot, the pull's `Take`
is clamped to 0, the line fails, the prefix returns false, and the crafted item is kept unpaid.

**R8 — reserve an upper bound.** Reserve the most the roll can add, not the base amount: the base plus
`num2 * m_craftBonusAmount` (quality level times the per-roll bonus). Check the real field names in the
decompiled assembly rather than guessing. If that per-roll amount is not reachable from where you
compute the reservation, fall back to a value you can *prove* is an upper bound, and say in your report
which you used and why. An over-reservation that declines a marginal craft is recoverable; an
under-reservation is a free item.

The remarks at `CraftPatches.cs:256-259` assert the reservation "is exactly what
`InventoryGui.DoCrafting` does before it calls `ConsumeResources`" — that is factually wrong for the
bonus case, so correct the comment as part of this fix rather than leaving a comment that contradicts
the code.

## Important — the output reservation must not apply to an upgrade

`InventoryGui.DoCrafting` is also reached with `m_craftUpgradeItem != null`. On that path it removes
the item being upgraded and re-adds the upgraded one **at the same position**, so net slot usage is
unchanged — and vanilla deliberately skips its own output guard for upgrades (`m_craftUpgradeItem ==
null && !CanAddItem(...)`).

`CraftedOutput` still reserves `GetAmount` of room, so at the margin (materials that fit a matching
stack, inventory otherwise full) the gate can decline an upgrade that vanilla would have completed.
**R9 — pass no reserved output when `m_craftUpgradeItem != null`**, since an upgrade consumes no new
output room.

## Tests

Both rules belong to the pure `DestinationCapacity` helper, so both are testable and must be covered:
the bonus-sized output reservation, and the upgrade path reserving nothing.

## Scope and verification

Everything from rounds 1–3 still binds: `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; **never write a
`Container`'s inventory** (chest removal only through `MultiUserChestBridge.RemoveFromChest`, drawer
only through `ForceRemove`); `Logic/**` free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`; storage
keyed by **prefab name**; `Deferred` never a count, never summed, never retried; the planner stays
all-or-nothing; new files registered in the legacy csproj's explicit `<Compile>` items; no scratch or
inspection scripts anywhere in the tree; no ctrl-click key handling; no station-feeding; **do not push
to any remote**. Do not revisit R1–R7 — they are verified.

The four Minors from this review are **deferred**, not part of this round: `DestinationCapacity.Matches`
duplicating `DrawerTake`'s predicate; world-level skew between the gate's model and the delivery; the
per-frame cost of the build-HUD scan; and the differential test sharing `NextChunk`.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 127 or more)
3. Commit, then append a **Fix round 4** section to `task-13-fix-report.md` covering: the upper bound
   you reserve and where its value came from; how the upgrade path skips the reservation; the comment
   correction; the new tests; build and test lines; what remains racy. Extend `AUTHOR-VERIFY` with a
   crafting-bonus check (craft from storage with a skilled station and a nearly-full inventory, and
   confirm nothing is ever kept unpaid).

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report
path. Do not narrate your work in the reply.
