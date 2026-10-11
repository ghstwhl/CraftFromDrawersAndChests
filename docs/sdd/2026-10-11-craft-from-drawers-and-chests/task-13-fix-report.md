# Task 13 — fix round 1 — Report

**Status:** DONE
**Branch:** `feature/craft-from-drawers-and-chests`
**Commit:** `8187394` — *fix: close the free-craft hole by making chest Take span stacks and pulls retry* (parent `cc2d0e1`)
**Files changed (7, +371 / −63):**
- `CraftFromDrawersAndChests/Logic/PullRunner.cs` (new) — the pure pull sequencing
- `Tests/Logic/PullRunnerTests.cs` (new) — 10 tests
- `CraftFromDrawersAndChests/Pull/PullEngine.cs` — `Execute` now delegates to `PullRunner`
- `CraftFromDrawersAndChests/Storage/VanillaChestSource.cs` — `Take` fulfils across stacks
- `CraftFromDrawersAndChests/Storage/IStorageSource.cs` — contract notes for the short/Deferred shapes
- `CraftFromDrawersAndChests/Patches/CraftPatches.cs` — single-ingredient recipes no longer offered on storage
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` — `+ <Compile Include="Logic\PullRunner.cs" />`

## The mechanism, re-derived before touching anything

I re-decompiled the game assembly in a child `powershell` process (so the probe never locks the build
output) and confirmed the review's premise rather than assuming it:

- `InventoryGui.DoCrafting` calls `Inventory.AddItem(<crafted prefab>, …)` at IL offset 1489/1531 and
  only calls `Player.ConsumeResources` at offset **1646** — i.e. the crafted item exists before the
  materials are consumed, so a consume-first prefix that returns `false` leaves the item in hand for
  free.
- `Player.HaveRequirementItems` for a `requireOnlyOneIngredient` recipe returns `true` as soon as
  **any one** requirement is satisfied; otherwise it requires all of them.
- `Recipe.GetAmount` picks the `singleReqItem` through `Player.GetFirstRequiredItem(player.GetInventory(), …)`
  — the **player's own inventory** — and `DoCrafting` removes that item with `Inventory.RemoveItem`
  (offset 1591–1623) **instead of** calling `ConsumeResources`. That is the root of Minor #5 (below).

## R1 — one definition of "what a source can deliver"

`VanillaChestSource.Count` sums every matching stack, but its `Take` removed from one stack only, so a
chest holding 2 × 30 passed a 50-item gate (60 ≥ 50) and then delivered 30. `Take` now **loops**,
mirroring `DrawerSource.Take`: it re-runs `FindLargestStack` (kept for choosing where to start) each
pass, calls `MultiUserChestBridge.RemoveFromChest` for the remaining amount, and repeats until the
request is met or the chest has no matching stack left. Every removal still goes through the bridge;
the container's inventory is still only read, never written. Each iteration either delivers a positive
number or breaks, so the loop always terminates. `Count` is now exactly what `Take` can hand over.

## R2 — `Deferred` mid-loop, and how a caller reacts to it

Two changes, exactly as ruled:

1. **Source side.** When a removal is routed after some items were already delivered, `Take` returns
   the delivered count, **not** the sentinel; the sentinel is returned only when nothing at all was
   delivered (`return delivered > 0 ? delivered : ChestTake.Deferred;`).
2. **Caller side.** `PullRunner` checks the sentinel **before** any comparison and treats it as the
   line being **satisfied**, logging it. It is still never a count, never summed, and never retried —
   the request is already in flight, so a retry would duplicate it. Failing the line here is precisely
   what created the free item, so optimism is the safe direction.

## R3 — a short delivery is not terminal

`PullRunner` no longer fails on a short line. A **positive short** answer (`0 < taken < remaining`)
retries the same source for the remainder and repeats while it keeps making progress; the line fails
only when the source answers **0** (exhausted). The retry is bounded because each pass asks for a
strictly smaller remainder — at most `line.Amount` iterations, so a misbehaving source cannot spin.
With R1 in place a line the planner emitted should now be fully deliverable, so the retry is
belt-and-braces against the residual race (a destination filling between gate and pull, or an
ownership change mid-frame).

## R4 — the gate is not more optimistic than the executor

`CanReach` returns `PullPlan` feasibility, and feasibility is computed from `Count`. After R1, a
source's `Count` equals what its `Take` can deliver, and `PullPlan` allocates at most a source's
`Count` (minus one when `LeaveOne`), so **every line the planner emits is one the executor can
secure** — the two now use the same deliverability definition. This is verified, not assumed:
`GateAndExecutorAgree_ALineBiggerThanOneStack_IsRetriedAndSecured` runs a plan whose single line (50)
exceeds one stack (30) against a source of 60 and asserts the executor secures it in exactly two
single-stack removals. No code change was needed beyond R1; the invariant is now pinned by a test.

## R5 — `PullRunner` and its tests

The sequencing is extracted into pure `Logic/PullRunner.cs` (`Run(lines, take, warn)`): the
decide-and-take step is a `Func<PullLine,int,int>` delegate, so it is driven from tests with a plain
lambda and no game types. `PullEngine.Execute` supplies the game-facing step (resolve the line's
source, call `IStorageSource.Take`, log). `Logic/` still references no `UnityEngine`/`Valheim`/
`BepInEx`/`Jötunn`; the new file is an explicit `<Compile>` item and is auto-linked into the test
project by the existing `Logic\**\*.cs` glob.

**How `Execute` reacts to each of `Take`'s three shapes** (all in `PullRunner.Run`):

| `Take` returns | Meaning | Reaction |
|---|---|---|
| `>= amount` | full count | line satisfied, move on |
| `0 < n < amount` | short but progressing | **retry the same source for the remainder**; repeat while it progresses |
| `0` | source exhausted | **fail the line** → all-or-nothing → prefix returns `false`, vanilla consumes nothing |
| `-1` (`Deferred`) | routed to another player | **line satisfied** (items already left storage); logged; never counted, never retried |

`-1` is tested before any arithmetic, so it can never be summed or read as a shortfall.

**The new tests (`PullRunnerTests`, 10) and what each proves:**
1. `CountMeetsTheLine_Satisfies` — a full delivery satisfies with a single call.
2. `ShapeShortThenMet_RetriesForTheRemainder_AndSatisfies` — 30 then 20 for a 50 line satisfies, and
   the retry asks only for the remainder (20). *This is the Critical's shape.*
3. `ShapeZero_SourceExhausted_Fails` — a short answer is retried, a `0` fails.
4. `ShapeDeferred_IsTreatedAsSatisfied_AndNeverRetried` — the sentinel satisfies in one call, no retry.
5. `ShapeDeferred_AfterAPartialDelivery_IsStillSatisfied` — 30 then sentinel satisfies.
6. `OneLineCannotBeMet_FailsTheWholeRun` — all-or-nothing: line 1 exhausted fails the run, and line 0's
   items were already taken (they stay in the inventory, which is recoverable).
7. `GateAndExecutorAgree_ALineBiggerThanOneStack_IsRetriedAndSecured` — R4: feasibility and the outcome
   agree across stacks.
8. `EveryLineMet_Satisfies` — the happy multi-line path.
9. `RetryIsBoundedByProgress_AndCannotSpin` — a source dribbling 1 at a time finishes a 5 line in
   exactly 5 calls; the retry is bounded.
10. `NoLines_SatisfiesVacuously` — an empty/null plan is satisfied (the "player already has it all" case).

## Minor #5 — `requireOnlyOneIngredient` recipes

The review's symptom was "the panel says yes, the craft does nothing". The ground truth above shows the
deeper reason: for such a recipe `DoCrafting` **never calls `ConsumeResources`** — it removes the
`singleReqItem` chosen from the player's inventory directly — so the pull path this mod installs cannot
supply it at all. Any storage-aware answer therefore re-creates the lie, so
`HaveRequirementItemsPostfix` now returns early for `piece.m_requireOnlyOneIngredient`, deferring to
vanilla's own any-one answer (which is exactly what `DoCrafting` will act on). This also removes the
old "requires *all* ingredients" behaviour, which vanilla never had. No behaviour is lost: the postfix
only runs when vanilla already returned `false`, i.e. when no single ingredient is present in the
player's inventory, which is exactly the case in which `DoCrafting` would refuse.

## R6 — what remains racy

R1–R3 close the **deterministic** hole: a chest whose aggregate `Count` exceeds one stack can now
genuinely deliver it, so a declined consume no longer follows. A **genuine race remains** between the
availability gate and the pull: another player can empty the source in that instant, in which case
`PullRunner` fails the line, the prefix returns `false`, and — because vanilla already created the item
— the player keeps a crafted item without paying. I have **not** claimed this closed; it is in
`AUTHOR-VERIFY` below. (A narrower sub-case of the same window: if a chest changes hands *mid-loop*,
`Take` returns the partial count per R2, the retry is answered `Deferred`, and the line is settled —
the routed remainder is bounded by what the chest still holds.)

## Build and tests

- **Build:** `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
  ```
    Built …\CraftFromDrawersAndChests.dll (53,248 bytes)
  BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
  ```
  `MSB3274` (MultiUserChest 4.7.2 vs this project's 4.6.2) printed once, expected and pre-existing,
  left unsuppressed.
- **Tests:** `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
  ```
  Test summary: total: 117, failed: 0, succeeded: 117, skipped: 0, duration: 1.3s
  ```
  117 = 107 baseline + 10 new `PullRunnerTests`.

## Constraints honoured

`net462` / BepInEx 5.4.2350 / Jötunn 2.30.2 unchanged. No `Container` inventory is ever written (chest
removal only through `MultiUserChestBridge.RemoveFromChest`; drawer only through `ForceRemove`).
`Logic/**` stays pure. Storage stays prefab-keyed. Pulls stay all-or-nothing. The new `Logic/` file is
registered in the legacy csproj. Only the named findings were touched — no ctrl-click key handling and
no station-feeding. Nothing was pushed (`origin/feature/…` is still at `1786216`).

## AUTHOR-VERIFY (in-game — not runnable here)

1. **The ordinary path (the Critical).** Put 2 × 30 Wood in one chest, require 50 in a recipe, carry
   none. Craft it: it must succeed, 50 must leave the chest, and **no free item** must appear. Repeat
   with 3 × 20 so the request spans three stacks.
2. **Residual race (R6).** With a second player, empty a chest between the panel showing the recipe as
   available and the craft landing. Confirm what actually happens and whether any item is awarded
   unpaid — this is the residual this fix does **not** close.
3. **Single-ingredient recipe (Minor #5).** A `requireOnlyOneIngredient` recipe whose ingredients are all
   in a chest (none carried) must **not** be shown as available and must craft nothing; with the
   ingredient carried it still crafts.
4. **Split chest + drawer.** Materials split between a chest and a drawer, carrying none: crafts, and the
   materials leave both.
5. **Nothing in range.** A requirement larger than everything in range: the recipe is not marked
   available and crafting consumes nothing.

## Concerns

- **The residual race in R6 is a real, if narrow, "costs nothing" window.** The controller ruled it
  residual and asked for it to be documented rather than closed; it is, above.
- **Mid-loop handover double-route.** Per R2 a partial-then-routed `Take` returns the count, so
  `PullRunner`'s retry calls the now-foreign chest again; that second routed request can over-deliver
  the remainder. This is inside the same mid-frame handover window R6 names, and R2 was applied exactly
  as ruled; flagged so the reviewer can weigh the count-vs-sentinel trade-off.
- **`PlanFor` (full-requirement semantics)** remains provided for the ctrl-click pull (T14) and is still
  not called by Task 13, intentionally.

---

# Task 13 — fix round 2 — Report

**Status:** DONE
**Branch:** `feature/craft-from-drawers-and-chests`
**Commit:** `f4dbedf` — *fix: return the Deferred sentinel from any routed mid-loop chest removal* (parent `8187394`)
**Files changed (2, +21 / −14):**
- `CraftFromDrawersAndChests/Storage/VanillaChestSource.cs` — the `Take` loop returns the sentinel on any routed removal; the `Take` remarks now state the sentinel rule
- `CraftFromDrawersAndChests/Storage/IStorageSource.cs` — the `Take` contract note corrected from the partial-count rule to the sentinel rule

## The corrected rule

Round 1's **R2** was wrong and is inverted here. The loop asks each removal for the whole un-returned
remainder (`amount - delivered`), so a routed request covers **exactly** that remainder and
`delivered + routed == requested`. The sentinel's meaning is therefore *"the entire un-returned
remainder of this request is in flight"*, not *"nothing moved"*. When any call in the loop defers,
`Take` now returns `ChestTake.Deferred` **immediately, discarding the accumulated count**, regardless
of how much was already delivered:

- removal returns the sentinel → **return the sentinel at once** (the delivered items are already in
  the player's inventory, so no count is needed — `PullRunner` needs only the verdict);
- removal returns `0` → break, return the accumulated count (the source is exhausted — a genuine
  shortfall);
- otherwise accumulate and continue.

The two contract texts that described the old rule were both corrected: `VanillaChestSource.Take`'s
remarks and the `IStorageSource.Take` note. The caller side is **unchanged**: `PullRunner` checks the
sentinel before any comparison and treats it as the line being satisfied — never counted, never
summed, never retried.

## Why propagating the sentinel cannot over-deliver

Returning the sentinel makes the caller **stop**, so the line is settled on the strength of the routed
request, which already covers `amount - delivered`. Nothing is asked of the source again, so the
remainder cannot be requested a second time. The round-1 rule did the opposite: returning the partial
count made `PullRunner` (correctly, per R3) treat a positive short as "ask again", retrying for a
remainder that was already in flight.

Worked example — a 50-item line against a chest holding 2 × 30:

- **Round 1:** `take 50` removes 30, routes the remaining 20, returns **30**; the runner retries for
  20; the retry removes another 20 → **30 + 20 (routed) + 20 (retry) = 70** for a 50-item line.
- **Round 2:** `take 50` removes 30, routes the remaining 20, returns **`Deferred`**; the runner
  treats the line as settled and stops → **30 + 20 = 50**. No retry, no duplication.

## What else in round 1 assumed the old rule

I searched the whole tree for the old rule's wording and shape (`delivered > 0`, "delivered count",
"after some items", "nothing at all", routed/partial/sentinel). Only two code sites encoded it — the
`VanillaChestSource.Take` branch and the `IStorageSource.Take` note — and both are corrected here.
Everything else was already caller-side and stays valid, so nothing else needed changing:

- `PullRunner.Run` and `PullEngine.Execute` — unchanged; the sentinel is still checked first, treated
  as satisfied, never summed or retried.
- `ChestTake.Result` / `Deferred` / `WasRouted` — unchanged; `Result(routed: true, …)` already returned
  `Deferred` regardless of `delivered`, which is the **per-call** rule the loop now simply forwards.
- `MultiUserChestBridge.RemoveFromChest` — a single removal call; unchanged.
- `CraftPatches` and `PullEngine.Execute` docs — describe only the caller-side reaction, still accurate.
- `DrawerSource` / `DrawerTake` — no routed concept, so nothing there depends on the rule.

`PullRunnerTests` needed **no assertion changes** (the runner does not move, exactly as the brief
predicted). I re-checked the one test whose name touches the window,
`ShapeDeferred_AfterAPartialDelivery_IsStillSatisfied`: it still describes a reachable `PullRunner`
scenario — a first call that delivers 30 locally (no route *in that call*), then a retry whose call
routes — so its name and comment do not misdescribe the source's contract, and the wording is left
as-is. **No assertion was weakened.**

## Build and tests

- **Build:** `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
  ```
    Built …\CraftFromDrawersAndChests.dll (53,248 bytes)
  BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
  ```
  `MSB3274` (MultiUserChest 4.7.2 vs this project's 4.6.2) printed once, expected and pre-existing.
- **Tests:** `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
  ```
  Test summary: total: 117, failed: 0, succeeded: 117, skipped: 0, duration: 1.3s
  ```

## Superseded round-1 text

This section supersedes round 1's **R2** (its source-side bullet: "returns the delivered count, not
the sentinel") and the second **Concerns** bullet ("Mid-loop handover double-route"). The round-1
report is otherwise left intact as the record of that round.

## What remains racy

Unchanged from **R6**: the residual race between the availability gate and the pull — another player
emptying a source in that instant — is still open and remains an `AUTHOR-VERIFY` item (item 2 below).
The narrower **mid-frame handover** window is now safe in the *accounting* rather than duplication-prone:
if the chest changes hands mid-loop, `Take` returns the sentinel, the line is settled, and the routed
remainder is bounded by the amount the loop asked for (`amount - delivered`) — it can no longer be
requested twice. What is still not knowable synchronously is whether the routed removal will *actually*
be applied by the new owner; the caller optimistically treats the line as paid, exactly as the
interface specifies, so the safe (no-free-item) direction is preserved.

## AUTHOR-VERIFY (unchanged from round 1)

The five in-game checks from the round-1 report still stand; item 2 (the residual gate-to-pull race)
is the one this round does not close.

---

# Task 13 — fix round 3 — Report

**Status:** DONE
**Branch:** `feature/craft-from-drawers-and-chests`
**Commit:** `2eab476` — *fix: gate craft and build on the destination's free space (Task 13 R7)* (parent `f4dbedf`)
**Files changed (7, +654 / −22):**
- `CraftFromDrawersAndChests/Logic/DestinationCapacity.cs` (new) — the pure destination-acceptance model
- `CraftFromDrawersAndChests/Patches/CraftPatches.cs` — both gates now also require destination room; the crafting gate reserves the crafted output's room
- `CraftFromDrawersAndChests/Storage/IStorageSource.cs` — the `Take` contract no longer asserts the unconditional invariant (the previous worker's half; reviewed and kept)
- `CraftFromDrawersAndChests/Storage/VanillaChestSource.cs` — the matching `Take` remark reworded (the half the previous worker had not reached)
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` — `+ <Compile Include="Logic\DestinationCapacity.cs" />`
- `Tests/Logic/DestinationCapacityTests.cs` (new) — 10 tests
- `Tests/Logic/PullRunnerTests.cs` — two descriptions corrected (executor is simulated; Deferred-after-partial is a runner tolerance check)

## Audit: what the previous worker had done, and what I completed or corrected

The uncommitted tree was **substantially right**: `DestinationCapacity` and the `CraftPatches` wiring
were complete and correct in substance, and the `IStorageSource` half of the text correction was done.
What it had **not** finished or needed correcting:

1. **`VanillaChestSource.Take` remark** (its declared stopping point) — still asserted
   `delivered + routed == requested` unconditionally. Reworded (below).
2. **Both test-description corrections** — the rename and the Deferred-after-partial wording were
   untouched.
3. **`scratchpad-decompile.ps1`** — still present at the repo root. Deleted; a repo-wide search confirms
   only `tools/Build.ps1` and `tools/New-ValheimRefs.ps1` remain.
4. **A capacity-rule duplication in the draft.** `DestinationCapacity` re-implemented the matching
   predicate privately (a `Space` struct + a private `MatchingFree`) instead of using the helper the
   brief names. I rewrote it to delegate to the three existing helpers — `DrawerTake.NextChunk`
   (chunking), `DrawerTake.CanDeliver` (the gate) and **`DrawerTake.MatchingFreeStackSpace`** (the
   matching room) — exactly what `Storage.DrawerSource.Take` uses, so the two agree by construction. The
   rewrite is behaviour-preserving: `AgreesWithAStackByStackAddItemSimulation` still passes.
I also re-derived the game API facts the draft depends on by decompiling the publicized assembly
(`Recipe.m_item` is an `ItemDrop`, not a `GameObject`; `Recipe.GetAmount(int, out int, out ItemDrop.ItemData, int)`;
`ItemData.m_worldLevel` is an `int`; `Inventory.m_inventory`/`m_width`/`m_height`) before trusting them; all held.

## R7 — how each gate now accounts for destination capacity

Both postfixes answer yes only when storage covers the shortfall **and** the player's inventory can
accept it. The shared entry point is `CraftPatches.CanReach`:

```
return plan.Feasible && DestinationAllows(player, resources, plan, reserved);
```

- **Crafting gate** — `HaveRequirementItemsPostfix` calls
  `CanReach(instance, piece.m_resources, qualityLevel, amount, CraftedOutput(piece, qualityLevel, amount))`.
  `reserved` is the crafted **output** as a `Delivery`, listed **first** because vanilla adds it before
  consuming. That ordering is not assumed: `InventoryGui.DoCrafting` IL calls
  `Inventory.CanAddItem(recipe.m_item.gameObject, amount)` at offset 374 with `brtrue`→`ret` at 379/381
  (it refuses to craft at all when the output cannot be placed), then `AddItem` at 1480/1531, then
  `Player.ConsumeResources` at 1646 — so the output is in the inventory before the pull runs and competes
  with the shortfall for the same room.
- **Building gate** — `HaveRequirementsPiecePostfix` for `Mode == CanBuild` calls
  `CanReach(instance, piece.m_resources, 0, 1, null)`; a piece is placed into the world, not the
  inventory, so only the pulled shortfall is charged.

`DestinationAllows` reduces the destination the way `DrawerSource` does — `DrawerTake.InventoryStack`s
from `inventory.m_inventory` (shared name, quality, world level, stack, max stack) plus
`inventory.m_width * m_height - m_inventory.Count` empty slots — turns each plan line into a `Delivery`
built from the requirement's item (`m_shared.m_name`, `m_quality`, `m_worldLevel`,
`m_shared.m_maxStackSize`), and calls `DestinationCapacity.CanAccept`. It **fails closed**: a null
inventory, or a line whose item has no resolvable `ItemData` or a non-positive stack size, declines
rather than assumes deliverable.

`DestinationCapacity.CanAccept` applies the deliveries to one shared model of the destination (so two
items cannot both count the same empty slot) chunk by chunk: for each chunk it measures the matching
free space with `DrawerTake.MatchingFreeStackSpace`, requires `DrawerTake.CanDeliver(chunk, maxStack,
free, slots)` — the very gate the drawer path removes behind — then merges the chunk into matching
stacks and spills only the leftover into one empty slot (whose remaining room joins the matching space
for the next chunk). That is the same arithmetic `DrawerSource.Take` runs live, so the chest/craft/build
paths agree with the drawer path instead of diverging from it.

## Can the building path be honestly gated? Yes — with evidence

Confirmed from `assembly_valheim_publicized.dll`:

- `Player.UpdatePlacement` calls `Player.HaveRequirements(Piece, RequirementMode)` at IL offset **1099**,
  `brfalse IL_058c` (skip on false) at **1104**, `Player.TryPlacePiece(Piece)` at **1112**, and only then
  `Player.ConsumeResources(...)` at **1184**. So the patched `HaveRequirements` gate is a real
  precondition of placement: a false postfix result skips `TryPlacePiece`, and the piece is never built.
  (The mode argument is `ldc.i4.0`, i.e. `CanBuild`.)
- `Hud.UpdatePieceBuildStatus` and `Hud.UpdatePieceBuildStatusAll` also call
  `Player.HaveRequirements(Piece, RequirementMode)`, so the build HUD's "buildable / selectable" status
  flows through the same gate.

Therefore the building path is gated honestly and no approximation was needed; this is **not** a finding
for the controller.

## The two consequences the brief names

- **The panel can now decline a craft vanilla's own consumption would have made room for.** The pull
  needs the destination room *transiently*, before `ConsumeResources` frees the materials the player
  already carries. This is deliberate and it is the safe direction — over-strict is recoverable, a free
  item is not — and it mirrors what the drawer path already did.
- **A failed pull is again a genuine race only.** With the gates honest, the deterministic
  full-inventory hole is closed; what remains is another player emptying a source in the same instant.

## Text corrections

- `IStorageSource.Take` (previous worker's half) and `VanillaChestSource.Take` (mine) no longer state
  `delivered + routed == requested` as an invariant. The routed request covers the whole un-returned
  remainder only *up to* MultiUserChest's clamp `min(request, located stack, destination free space)`,
  so `delivered + routed == requested` can under-cover. The caller's behaviour is unchanged: a short
  positive count still means "ask again", a sentinel still means "settled, never retry".
- `PullRunnerTests.GateAndExecutorAgree_…` → **`GateAndSimulatedTakeStep_ALineBiggerThanOneStack_IsRetriedAndSecured`**,
  with a comment stating the take step is a hand-written **model**, not the real `VanillaChestSource`
  loop, so a regression in the real cross-stack loop would not fail it (and must be caught in-game).
- `ShapeDeferred_AfterAPartialDelivery_IsStillSatisfied` → **`RunnerToleratesASentinelArrivingAfterAShortAnswer`**,
  presented as a runner-contract tolerance check, with a comment that the post-round-2 chest source
  cannot produce the sequence (it returns the sentinel from the very call that routes). Assertions
  unchanged — no assertion was weakened.

## Scratch removed

`scratchpad-decompile.ps1` deleted and not committed; a repo-wide `*.ps1` search returns only
`tools/Build.ps1` and `tools/New-ValheimRefs.ps1`. No scratch or inspection scripts remain.

## Build and tests

- **Build:** `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
  ```
    Built …\CraftFromDrawersAndChests.dll (55,808 bytes)
  BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
  ```
  `MSB3274` (MultiUserChest 4.7.2 vs this project's 4.6.2) printed once, expected and pre-existing.
- **Tests:** `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
  ```
  Test summary: total: 127, failed: 0, succeeded: 127, skipped: 0, duration: 1.2s
  ```
  127 = 117 baseline + 10 new `DestinationCapacityTests`. The new rule is pure and covered.

## What remains racy

Unchanged from R6: the window between an availability check and the pull. Another player can empty a
source in that instant, so `PullRunner` fails the line, the prefix returns false, consumption is
skipped — and because vanilla already created the crafted item or placed the piece, the player keeps it
unpaid. The gates are now honest about *destination* capacity, so the **deterministic** free-craft and
free-place holes are closed; this residual is a genuine race and is **not** claimed closed.

## AUTHOR-VERIFY (extended for round 3 — in-game, not runnable here)

The five round-1 checks still stand. Add:

6. **Full inventory, no room for the shortfall (Critical #2 — the check that reproduced the defect before
   this round).** Carry none of a recipe's materials and fill the player's inventory so that no slot is
   empty and no partial stack holds the required material at the same quality and world level. **Before
   this round** the recipe still showed as available, crafting succeeded, and the pull's `Take` returned
   0 while the prefix skipped consumption — **a free crafted item**. After this round the recipe must
   **not** be offered, and crafting must consume nothing. Repeat for the building path: select a piece
   whose materials are in a chest while the inventory has no room, and confirm it is not placeable.
7. **One empty slot, shortfall that fits it.** With exactly one empty slot and a shortfall no larger
   than one stack, the craft/placement must still succeed (the gate must not be over-strict for a
   shortfall that genuinely fits).
8. **Output reservation.** Fill the inventory so the pulled shortfall *would* fit but the crafted output
   has nowhere to go; the craft must be declined (vanilla's own `CanAddItem` guard would refuse it too).

---

# Task 13 — fix round 4 — Report

**Status:** DONE
**Branch:** `feature/craft-from-drawers-and-chests`
**Commit:** `be8b71d` — *fix: reserve the crafting-skill bonus output and skip output room for upgrades (Task 13 R8/R9)* (parent `2eab476`)
**Files changed (3, +175 / −5):**
- `CraftFromDrawersAndChests/Logic/DestinationCapacity.cs` — new pure helpers `MaxCraftBonus` and `ReservedOutputAmount` (both rules)
- `CraftFromDrawersAndChests/Patches/CraftPatches.cs` — `CraftedOutput` reserves the bonus upper bound and passes nothing for an upgrade; the false comment is corrected
- `Tests/Logic/DestinationCapacityTests.cs` — 7 new tests

## Ground truth first — the real field names and the real loop

I re-decompiled `InventoryGui.DoCrafting` (and `Recipe.GetAmount`) with Mono.Cecil in a **child
`powershell` process**, so the probe never held the build output open. The brief says not to guess the
names; here is what the assembly actually holds:

- Fields: `InventoryGui.m_craftBonusChance` = `System.Single` (**0.25** in the prefab) and
  `InventoryGui.m_craftBonusAmount` = `System.Int32` (**1** in the prefab). Both are instance fields,
  reachable through the static singleton `InventoryGui.instance` (backing field `m_instance`).
- `Recipe.GetAmount(int quality, out int need, out ItemData singleReqItem, int craftMultiplier)`
  returns `num * craftMultiplier`.
- The bonus block (IL `0x00E1`–`0x0156`) is exactly:

```
V_7 = 0                                             // running bonus
if (station != null && station.m_craftingSkill != None && output.m_maxStackSize > 1) {
    V_18 = Player.m_localPlayer.GetSkillFactor(station.m_craftingSkill)
    for (V_19 = 0; V_19 < V_3; V_19++) {            // V_3 = craftMultiplier = the craft amount
        if (Random.value < V_18 * m_craftBonusChance) {
            V_7 += m_craftBonusAmount               // bonus grows every success
            V_4 += V_7                              // <-- the addition itself is CUMULATIVE
        }
    }
}
...
AddItem(recipe.m_item.name, V_4, quality, variant, V_9 /*gridPos*/, ...)   // IL 0x05D1
```

So `DoCrafting` adds **`GetAmount` plus a bonus that is added cumulatively**: `k` successes add
`perRoll * (1 + 2 + … + k)`, and the loop runs `craftMultiplier` (= `amount`) times.

## R8 — the upper bound I reserve, and why it is the value I chose

`DestinationCapacity.MaxCraftBonus(perRollBonus, craftMultiplier)` returns
`perRollBonus * n * (n + 1) / 2` (`0` when either argument is non-positive; clamped to `int.MaxValue`),
and `ReservedOutputAmount(baseAmount, perRollBonus, craftMultiplier, isUpgrade)` returns
`baseAmount + MaxCraftBonus(...)` (`0` when the base is non-positive).

- **Where the value came from:** `perRollBonus` is read live from `InventoryGui.instance.m_craftBonusAmount`
  (the real per-roll field, default 1); `craftMultiplier` is the same `amount` the gate already receives;
  `baseAmount` is `Recipe.GetAmount`.
- **Why `n(n+1)/2` and not `n`:** the brief's formula is `base + num2 * m_craftBonusAmount`. The IL shows
  the add is the *running* bonus (`V_4 += V_7`, with `V_7` accumulating), not a flat per-roll constant, so
  the true maximum for `n` rolls is `perRoll * (1 + 2 + … + n)`, which is `perRoll * n` for the ordinary
  single craft (`n = 1`, the normal case, so the two agree) but strictly larger for a multi-craft. The
  brief explicitly allows "a value you can prove is an upper bound"; this is the exact cumulative maximum,
  so it can never under-reserve. The roll's **chance** (`m_craftBonusChance`) only decides whether a roll
  succeeds, so it cannot bound the maximum and is deliberately not needed — the bound assumes every roll
  succeeds.
- **Why the gate does not also test the station's crafting skill / `m_maxStackSize > 1`:** those are the
  loop's pre-conditions, so reserving the bonus unconditionally is still an upper bound; skipping the test
  removes a (theoretical) gate-vs-craft skew if the station changed between the two. The cost is one
  reserved item in the rare case where no bonus could fire, which is the recoverable direction.

## R9 — how the upgrade path skips the reservation

`CraftedOutput` reads `InventoryGui gui = InventoryGui.instance;` and
`bool isUpgrade = gui != null && gui.m_craftUpgradeItem != null;`, passing it to
`ReservedOutputAmount`, which **short-circuits to `0`**. This mirrors `DoCrafting`: with
`m_craftUpgradeItem != null` it skips its own output guard (`m_craftUpgradeItem == null &&
!CanAddItem(...)`) and removes the item at `m_gridPos` (IL `0x021C`) before re-adding the upgraded one at
the **same** `gridPos` (IL `0x05D1`), so net slot usage is unchanged. Because a zero-amount delivery is
skipped by `CanAccept`, no output room is charged — only the pulled materials.

## The comment correction

The `CraftedOutput` remark (`CraftPatches.cs:256-259`) asserted the reserved amount "is exactly what
`InventoryGui.DoCrafting` does before it calls `ConsumeResources`". That is false for the bonus case, and
the remark now says so: it explains that `DoCrafting` adds a crafting-skill bonus on top of
`Recipe.GetAmount`, that the reservation is the base plus the maximum that bonus can add (deliberately an
upper bound), and that an upgrade reserves nothing.

## The new tests (7) and what each proves

1. `MaxCraftBonus_IsTheCumulativeRollMaximum` — the value is `perRoll * (1 + … + n)`, and is `0` for a
   non-positive per-roll or roll count. *(R8)*
2. `MaxCraftBonus_NeverUnderestimatesThePerRollBound` — for `n ≥ 1` the cumulative maximum is ≥ the flat
   `perRoll * n`, so it can never under-reserve relative to the brief's linear bound. *(R8)*
3. `ReservedOutputAmount_ChargesBasePlusBonusForANormalCraft` — base + bonus for `(1,1)`, `(2,2)`, `(1,3)`.
4. `ReservedOutputAmount_IsZeroForAnUpgrade` — an upgrade reserves nothing regardless of the other
   arguments. *(R9)*
5. `ReservedOutputAmount_IsZeroWhenThereIsNothingToPlace` — a non-positive base asks for no room.
6. `CanAccept_DeclinesWhenOnlyTheBaseOutputFitsButNotTheBonus` — the decisive R8 case end-to-end: a
   matching stack with exactly the base amount free and no empty slot accepts the base output but
   **declines** the base + bonus reservation. This is the free-craft window R8 closes.
7. `CanAccept_ANormalCraftDeclinesWhereAnUpgradeSucceeds` — the R8/R9 interaction: with one empty slot and
   a one-stack material shortfall, a normal craft declines (output + material cannot share the slot) while
   an upgrade (output reserved nothing) succeeds.

`MaxCraftBonus` and `ReservedOutputAmount` are pure and live in `Logic/`; no `UnityEngine`/`Valheim`/
`BepInEx`/`Jötunn` type crosses into `Logic/`.

## Build and tests

- **Build:** `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
  ```
    Built …\CraftFromDrawersAndChests.dll (56,320 bytes)
  BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
  ```
  `MSB3274` (MultiUserChest 4.7.2 vs this project's 4.6.2) printed once, expected and pre-existing.
- **Tests:** `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
  ```
  Test summary: total: 134, failed: 0, succeeded: 134, skipped: 0, duration: 1.3s
  ```
  134 = 127 baseline + 7 new.

## What remains racy

Unchanged from R6: the window between the availability gate and the pull — another player emptying a
source in that instant — is still open (`PullRunner` fails the line, the prefix returns false, and vanilla
has already created the output). R8/R9 make the **reservation honest** about what the game itself will
add; they do not close that race.

One narrower point this round surfaced, and its bounds: `Player.HaveRequirements(Recipe, …)` is also
called from `InventoryGui.UpdateRecipeList`, which iterates many recipes while `m_craftUpgradeItem` may be
set for the **selected** one. For a non-selected recipe the gate would then read `isUpgrade == true` and
reserve no output, which can mark that recipe available a little too eagerly in the list. This is
**cosmetic and self-correcting, not a free item**: selecting the recipe runs `InventoryGui.UpdateRecipe`,
and `DoCrafting` itself re-runs `HaveRequirements` and reads the *same* `m_craftUpgradeItem` it acts on
(IL `0x008B`), so the craft that actually executes is gated consistently — a real (non-upgrade) craft is
still refused by vanilla's own `CanAddItem` guard when the output cannot be placed. It is left as the
brief ruled (reserve nothing whenever `m_craftUpgradeItem != null`) and flagged here for the reviewer to
weigh.

## AUTHOR-VERIFY (extended for round 4 — in-game, not runnable here)

The round-1 and round-3 checks still stand. Add:

9. **Crafting-bonus check (R8).** At a station that has a crafting skill, with a **nearly-full** inventory
   (one empty slot or a matching partial stack with only the base output's room free) and the materials
   only in storage, craft the recipe. Repeat until the crafting-skill bonus actually fires (the
   `m_craftBonusChance` roll — you will see the "+N" bonus text at the station). Confirm the craft either
   is not offered, or is offered and completes with **nothing kept unpaid** — the pulled materials always
   leave storage for what was produced, and no output is ever spilled for free.
10. **Upgrade check (R9/R10 — corrected in round 5 to the first-press case).** With the crafting panel
    **not yet pressed** — the first time the upgrade recipe is selected after opening the station —
    carry the materials for the upgrade in storage and fill the inventory so it has room only for those
    materials (no room for the extra output). The upgrade must be **offered (the craft button
    interactable) and succeed** — R9/R10 must not decline a legitimate upgrade. Re-open the station
    between repeats so every attempt is a fresh first press: `m_craftUpgradeItem` is null until
    `OnCraftPressed` runs, so a repeat in the same panel session would exercise the already-armed flag
    and could pass even with the defect.

## Constraints honoured

`net462` / BepInEx 5.4.2350 / Jötunn 2.30.2 unchanged. No `Container` inventory is written (chest removal
only through `MultiUserChestBridge.RemoveFromChest`; drawer only through `ForceRemove`). `Logic/**` stays
pure. Storage stays prefab-keyed. No new files, so no csproj change was needed. No scratch or inspection
scripts remain (a repo-wide `*.ps1` search returns only `tools/Build.ps1` and `tools/New-ValheimRefs.ps1`;
the two decompile probes were written under `%TEMP%` and deleted). Only R8/R9 and the named comment were
touched; the four Minors were left deferred. Nothing was pushed (`origin/feature/…` is still at `1786216`).

---

# Task 13 — fix round 5 — Report

**Status:** DONE
**Branch:** `feature/craft-from-drawers-and-chests`
**Commit:** `19484f8` — *fix: recognise an upgrade craft from the call-site quality level, not the
press-armed flag (Task 13 R10)* (parent `be8b71d`)
**Files changed (3, +57 / −7):**
- `CraftFromDrawersAndChests/Logic/DestinationCapacity.cs` — new pure `IsUpgrade(int qualityLevel)` predicate
- `CraftFromDrawersAndChests/Patches/CraftPatches.cs` — `CraftedOutput` derives `isUpgrade` from the craft
  context the caller passed, not from `InventoryGui.m_craftUpgradeItem`
- `Tests/Logic/DestinationCapacityTests.cs` — 2 new tests

No new files, so no csproj change.

## R10 — where `isUpgrade` now comes from

`CraftPatches.CraftedOutput` no longer reads `InventoryGui.instance.m_craftUpgradeItem`. It derives the
upgrade condition from the **quality level the requirement check was made at** — the craft context the
call site already hands the patched method:

```csharp
bool isUpgrade = DestinationCapacity.IsUpgrade(qualityLevel);   // qualityLevel > 1
```

`qualityLevel` is the third argument of `Player.HaveRequirementItems(Recipe piece, bool discover, int
qualityLevel, int amount)`, the method the postfix runs on. The only thing still read from the singleton
is the live per-roll bonus value (`m_craftBonusAmount`), unchanged from round 4.

## Decompiled evidence — it is exactly `OnCraftPressed`'s condition, and no wider

Re-decompiled `assembly_valheim_publicized` (1.0.16) with Mono.Cecil in a **child `powershell` process**,
so the probe never held the build output open.

**1. `m_craftUpgradeItem` is written in exactly one place.** Scanning every method of every type in the
module for a `stfld` to the field named `m_craftUpgradeItem` returns exactly one hit:
`InventoryGui::OnCraftPressed`. Its IL_0033–003F is

```
ldarg.0 ; ldflda m_selectedRecipe ; call RecipeDataPair::get_ItemData ; stfld m_craftUpgradeItem
```

i.e. `m_craftUpgradeItem = m_selectedRecipe.ItemData`, after an early-return guard (IL_0000–0012) that
tests only `m_selectedRecipe.Recipe != null`. So, once a craft is in flight,
`m_craftUpgradeItem != null` ⟺ `m_selectedRecipe.ItemData != null`. Inside `OnCraftPressed` the value is
then used for the quality level (IL_0089–00A1) and for **vanilla's own output guard** (IL_00CF–010C:
`if (m_craftUpgradeItem == null && !inventory.CanAddItem(m_craftRecipe.m_item.gameObject, amount)) {
Message($inventory_full); return; }`) — the guard R9 mirrors.

**2. Every craft path builds `qualityLevel` the same way.** All three sites compute
`upgradeItem != null ? upgradeItem.m_quality + 1 : 1`:

- `OnCraftPressed` IL_0089–00A1, from `m_craftUpgradeItem`;
- `InventoryGui.DoCrafting` IL_000F–0027, from `m_craftUpgradeItem`; then
  `player.HaveRequirements(m_craftRecipe, false, qualityLevel, amount)` at IL_008B–0095;
- `InventoryGui.UpdateRecipe` IL_0102–011F, from `m_selectedRecipe.ItemData`; then
  `player.HaveRequirements(m_selectedRecipe.Recipe, false, qualityLevel, amount)` at IL_04AC–04CC, whose
  result drives `m_craftButton.interactable` (`set_interactable`) at IL_051D — **before any press**.

**3. The postfix's `qualityLevel` always comes from such a call site.** `Player.HaveRequirements(Recipe,
bool, int, int)` forwards all four arguments verbatim to `Player.HaveRequirementItems` (IL_0078–007E), and
`HaveRequirementItems` uses `qualityLevel` (its `ldarg.3`) for `req.GetAmount(qualityLevel) * amount`
(IL_00BC–00C6). Scanning the whole module, **`Player.HaveRequirementItems` has exactly one caller**:
`Player.HaveRequirements(Recipe, bool, int, int)`. That overload's callers are:

| caller | IL | discover | qualityLevel |
|---|---|---|---|
| `Player.UpdateKnownRecipesList` | IL_008A | **true** | 0 |
| `InventoryGui.UpdateRecipeList` (list row) | IL_0089 | false | 1 |
| `InventoryGui.UpdateRecipeList` (selected row) | IL_0200 | false | `ItemData.m_quality + 1` |
| `InventoryGui.UpdateRecipe` (the panel gate) | IL_04C7 | false | `ItemData.m_quality + 1` |
| `InventoryGui.DoCrafting` (the execution gate) | IL_0095 | false | `m_craftUpgradeItem.m_quality + 1` |

(The other `HaveRequirements` hits in the module are the `(Piece, RequirementMode)` overload —
`UpdatePlacement`, `UpdateKnownRecipesList`, `BuildUiPieceButton`, two `Hud` methods, `PieceTable` — and are
untouched.) The postfix early-returns on `discover == true`, so of the paths that can reach it, all but
the list row are upgrades at `qualityLevel = itemQuality + 1` and the list row is a normal craft at exactly
1. An item's quality is never below 1, so an upgrade is answered at ≥ 2 and every normal craft at exactly
1: **`qualityLevel > 1` is the upgrade condition for every path that reaches the gate**, and it is the same
value `OnCraftPressed` would copy — no wider.

## Panel and execution agree

Because the value is computed *by the caller* rather than read from an ambient field, each gate gets its
own correct answer, at the moment it runs:

- panel (`UpdateRecipe`, IL_04C7): `m_selectedRecipe.Recipe` at `m_selectedRecipe.ItemData.m_quality + 1`;
- execution (`DoCrafting`, IL_0095): `m_craftRecipe` at `m_craftUpgradeItem.m_quality + 1`.

`m_craftRecipe`/`m_craftUpgradeItem` are the pair `OnCraftPressed` sets together from `m_selectedRecipe`,
so for the same recipe the two answers are identical. This is what a read of `m_selectedRecipe.ItemData`
inside the postfix would *not* give, and it matters here: `OnCraftPressed` does not craft immediately — it
sets `m_craftTimer = 0` and returns (IL_017F–0184) — and `DoCrafting` is called later from
`InventoryGui.UpdateRecipe` itself when the timer elapses (IL_07BC–07C8: `if (m_craftTimer >=
m_craftDuration) { DoCrafting(player); m_craftTimer = -1; }`). During that craft-duration window the player
can change the selected recipe, and then `m_selectedRecipe.ItemData` would answer for a recipe that is not
the one being crafted. Deriving from the value `DoCrafting` itself passes makes that divergence
impossible — and it is the safe direction the brief demands: the ambient read could have said
`isUpgrade == true` for a craft that really produces output, an **under**-reservation (a free-craft
window); the call-site value cannot.

## Tests

`IsUpgrade` is a pure rule in `Logic/`; it is tested (`DestinationCapacityTests`, +2):

1. `IsUpgrade_IsTrueAtTheUpgradeQualityLevel` — 2, 3 and 10 are upgrades (an upgrade is always
   `quality + 1 ≥ 2`).
2. `IsUpgrade_IsFalseForANormalCraft` — 1, 0 and −1 are not, so the output room is still reserved.

The panel-versus-execution agreement itself is **not** unit-testable: it is the identity of the
`qualityLevel` the two game call sites pass, which exists only in the decompiled assembly. It is pinned by
the table above and by the `AUTHOR-VERIFY` upgrade check (item 10), not by a unit test — no coverage is
implied.

## Build and tests

- **Build:** `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
  ```
    Built …\CraftFromDrawersAndChests.dll (56,320 bytes)
  BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
  ```
  `MSB3274` (MultiUserChest 4.7.2 vs this project's 4.6.2) printed once, expected and pre-existing.
- **Tests:** `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
  ```
  Test summary: total: 136, failed: 0, succeeded: 136, skipped: 0
  ```
  136 = 134 baseline + 2 new.

## What remains racy

Unchanged from R6: the window between the availability gate and the pull — another player emptying a
source in that instant — is still open (`PullRunner` fails the line, the prefix returns false, and vanilla
has already created the output). R10 makes the *reservation* honest about whether a craft produces new
output; it does not close that race.

One window this round re-checked and found **safe**: after the press, `DoCrafting` runs only when the
craft timer elapses (up to `m_craftDuration` later), so the inventory can change in between. That is
covered because `DoCrafting` re-runs `HaveRequirements` — our execution gate — before it adds anything
(IL_008B, `ret` on false at IL_00B2, ahead of the `AddItem`), so a state change in that window refuses the
craft before an item exists rather than after.

## AUTHOR-VERIFY (round 5 — in-game, not runnable here)

No new in-game step. Round-4's check 10 (the upgrade check) has been **corrected in place** to the
**first-press** case, which is the one R10 was broken for: it now makes the attempt with the panel not yet
pressed (the first time the upgrade recipe is selected after opening the station) and re-opens the station
between repeats, because `m_craftUpgradeItem` is null until `OnCraftPressed` runs — a repeat in the same
panel session would exercise the already-armed flag and could pass even with the defect. Checks 1–9 stand
unchanged.

## Constraints honoured

`net462` / BepInEx 5.4.2350 / Jötunn 2.30.2 unchanged. No `Container` inventory is written (chest removal
only through `MultiUserChestBridge.RemoveFromChest`; drawer only through `ForceRemove`). `Logic/**` stays
pure — `IsUpgrade` references no game type. Storage stays prefab-keyed. No new files, so no csproj change
was needed. No scratch or inspection scripts remain (a repo-wide `*.ps1` search returns only
`tools/Build.ps1` and `tools/New-ValheimRefs.ps1`; the decompile probes were written under `%TEMP%` and
deleted). Only R10 was touched; the seven deferred Minors were left alone. Nothing was pushed
(`origin/feature/…` is still at `1786216`).
