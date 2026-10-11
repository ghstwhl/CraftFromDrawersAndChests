# Task 14b — fix round 1

No Critical. Two Importants with **one root cause**, plus two bundled Minors. The CFC port itself is
verified faithful (trigger, guards, branch order, pull arguments, and the correct call that CFC has no
build-path readout), and the review confirmed with the sanctioned probe that there is **no double-pull**
and **no free placement**: the pull press returns `false` so `TryPlacePiece`/`ConsumeResources` are
skipped, and the later place press pulls only the shortfall.

## Important #1 and #2 — the pull asks for the whole requirement while the gate validated the shortfall

`PullKeysPatches` → `PullEngine.PullToPlayer` → `PlanFor` → `BuildRequests(requirements, qualityLevel,
multiplier)` passes **no held counts** (`PullEngine.cs:154`, the `held: null` path). So it demands the
*entire* requirement of storage. But the gate that let this action happen is **shortfall-based**:
`CraftPatches.HaveRequirementsPiecePostfix` → `CanReach` → `BuildPlan` (`CraftPatches.cs:116,200,219`).

Two user-visible consequences, both on the feature this task exists to add:

1. **The player is told they are missing materials they are holding.** Piece needs 10 Wood, player
   carries 10 Wood, no storage holding Wood → the gate marks the ghost `Valid` → attack with the pull
   key → the plan asks storage for 10 Wood, gets 0, is infeasible → the refusal message reads
   "Not enough materials within range: Wood." and the press is swallowed. CFC shows its pulled message
   in this state, because it never checks feasibility.
2. **Over-pull.** One press takes the whole requirement regardless of what the player already holds —
   a piece needing 10 Wood + 2 Iron pulls 2 Iron from storage even when the player is carrying 2 Iron —
   and every additional press takes another full requirement, draining storage stack by stack. A player
   holding everything and standing next to a stocked chest gets the lot pulled into their inventory.

Both are the same defect: **the executor demands more than the gate validated.** Fix the request, and
both go away.

### R27 — request the shortfall, not the whole requirement

Pass the player's held counts into the request builder so the pull asks only for what is **missing** —
the same notion `BuildPlan`/`CanReach` already use. Reuse the existing pure matching predicate for
"does this held stack count toward this requirement" (name, **quality** and world level — the same
three-part match `DestinationCapacity`/`DrawerTake` already implement); do not invent a second rule.
Put whatever is pure into `Logic/` with tests.

**Apply it to both pull sites, not just the build one.** The recipe path (`InventoryGui.OnCraftPressed`
→ `PullToPlayer`) has the same `held: null` call and the same defect — Task 14's report recorded it as
a concern rather than fixing it. This fix closes it for both, so say so in your report and treat the
recipe path as in scope for this round.

**This deliberately diverges from CraftFromContainers**, which asks storage for the whole requirement
with no held-subtraction (CFC `1175`, `GetAmount(qualityLevel) * multiple`) and therefore over-pulls.
Record the divergence and your reasoning. The direction is the safe one — it matches this project's own
gate, and the whole point of that gate is that a "yes" means the pull can deliver what the action
actually needs.

## Bundled Minors

- **Test coverage.** Add a case for `pullKeyHeld: false` with each `switchPrevent` state (the pull key
  must be required in *both*), and — more importantly — a test that the **reader** yields the `-1`
  sentinel for a null field. Only the predicate side is asserted today (`IsPlacementValid(-1)`); the
  reader half (`PullKeyPatches.PlacementStatus`, `-1` on a missing field) is the half that degrades
  silently if the game renames a field, and nothing pins it.
- **Resourceless piece.** A piece with a null or empty `m_resources` makes `PullToPlayer` return false
  while the plan is feasible-with-zero-lines, so the player sees the "not enough room in your
  inventory" message for a pull that had nothing to fetch. Give it an accurate (or no) message.

## Deferred — do not fix

The fidelity note that the build guard reaches `AllowByKey` via `PullKeyRules.ShouldPull` rather than
the named helper (behaviourally identical to the sibling recipe site); the thin `IsPlacementValid` /
`Decide` restatement tests beyond the two branch-order pins; and the refusal message naming raw prefab
names (`IronScrap`) rather than localized ones.

## R25 — the sanctioned probe now covers fields

It gained a `-Fields` switch, so the field facts no longer need an ad-hoc script — which the review
noted you had to write. **Do not write your own probe.** Verified already, so do not re-derive:
`Player::m_placementStatus : PlacementStatus`, `Player::m_buildPieces : PieceTable`,
`Player::m_placementGhost : GameObject`, and `Player::TryPlacePiece(Piece)` has exactly one caller,
`Player::UpdatePlacement`.

```
powershell -NoProfile -ExecutionPolicy Bypass -File .superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\tools\cecil.ps1 -Assembly <dll> -Method <Name> [-Fields] [-Definitions] [-Callers]
```

## Scope and verification

Everything that bound Task 14b still binds: `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; `Logic/**`
free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`; **never write a `Container`'s inventory or a
drawer's ZDO `"Amount"`** (chest removal only via `MultiUserChestBridge.RemoveFromChest`, drawer only
via `ForceRemove`); storage keyed by **prefab name**; `-1` (`ChestTake.Deferred`) never a count, never
summed, never retried; **items must never end up on the ground and a placement must never cost
nothing**; new files registered in the legacy csproj's explicit `<Compile>` items; no scratch or
inspection scripts in the tree; no Nexus artefacts; no other mod's identity anywhere in this
repository; `MSB3274` expected and untouched; no station-feeding; no console command; **do not push to
any remote**.

Also correct the report's citation error: the `PullResources` line is CFC `1175`, not `1198` (its
`1157`, `1116`, `1119`, `1261` citations are exact), and it defines **19** new `[Test]` methods, not 18.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 176 or more)
3. Commit, then append a **Fix round 1** section to `task-14b-report.md` covering: how the shortfall is
   computed and which matching predicate you reused; why this closes both the false refusal and the
   over-pull; that the recipe path shares the fix; the CFC divergence and its reasoning; the bundled
   Minors; the corrected citations; build and test lines; anything still divergent from CFC.

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report
path. Do not narrate your work in the reply.
