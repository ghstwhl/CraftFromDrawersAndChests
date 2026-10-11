# Task 13 — fix round 1

The task review found one Critical, two Important, and one bundled Minor. Read your original brief
(`task-13-brief.md`), your report (`task-13-report.md`) and the review findings below.

## What the review found, and the mechanism

**Critical #1 — the decline path hands out a free crafted item, deterministically.**

`PullEngine.Execute` treats a `Take` that returns fewer than the line asked for as terminal
(`PullEngine.cs:228`), and `CraftPatches`'s prefix then returns `false` to skip vanilla's consumption
(`CraftPatches.cs:112-117`). But vanilla has **already created the item** by then: decompiled
`InventoryGui.DoCrafting` calls `GetInventory().AddItem(<crafted prefab>, …)` and only afterwards
calls `ConsumeResources`; `Player.UpdatePlacement` likewise calls `TryPlacePiece` before
`ConsumeResources`. Nothing rolls the item back when consumption is skipped.

The window is not a race — it is the ordinary path, because three pieces disagree about what a chest
can give in one call:

- `VanillaChestSource.Count` **sums every matching stack** (`VanillaChestSource.cs:132-134`).
- `VanillaChestSource.Take` removes from **one stack only** and says so in its own docs
  (`VanillaChestSource.cs:168`, contract at `:148-152`).
- `DrawerSource.Take` **loops** across chunks (`DrawerSource.cs:135-200`).

So a chest holding 2 × 30 Wood passes the availability gate for a 50-Wood requirement (60 ≥ 50), the
planner emits one line of 50, vanilla `AddItem`s the crafted item, `Take(…, 50)` returns 30,
`Execute` returns false, the prefix skips consumption → **the player keeps the crafted item for
free**, and if the short line is a later one the earlier lines' materials were also pulled into the
inventory for free. The `IStorageSource.Take` contract already permits this short return ("which may
be fewer than requested, or zero") — so `Execute` is the side that contradicts the contract it
consumes, and the chest source is the side that disagrees with its sibling.

**Important #2** is the same defect seen from the gate: `CanReach`/`CanReachAlmost` are computed from
aggregate `Count`, while `Execute` demands one `Take` call to meet the whole line. The two must use
the same deliverability definition.

**Important #3:** none of this is tested. Only five trivial `PullMath.Shortfall` assertions exist.

**Minor #5 (bundled):** for a `requireOnlyOneIngredient` recipe, `CraftPatches.cs:83-84` requires
*all* ingredients while vanilla returns true on *any* one, so the postfix can offer a recipe that
`DoCrafting` then refuses (`singleReqItem == null` → returns without crafting). It is in the same
gate you are about to fix, so fix it here rather than leaving a known "panel says yes, craft does
nothing" for the author to hit in validation.

Minors #4 (`CanAlmostBuild` beyond the brief) and #6 (`MSB3274`) are deferred; leave them.

## Controller rulings for this round

**R1 — one definition of "what a source can deliver".** A source's `Count` must be the amount its
`Take` can actually hand over. Make `VanillaChestSource.Take` fulfil up to `amount` **across
stacks**, mirroring the loop `DrawerSource.Take` already has, rather than removing one stack and
stopping. Keep `FindLargestStack` for choosing which stack to start with, and keep every removal
going through `MultiUserChestBridge.RemoveFromChest`. Do not write the container's inventory.

**R2 — `Deferred` mid-loop, and how a caller must react to it.** A chest call that returns
`ChestTake.Deferred` **after** some items were already delivered must return the delivered count, not
the sentinel; a sentinel is only correct when nothing at all was delivered. And when a pull does see
`Deferred`, it must treat the line as **satisfied, not failed** — the items have already left the
storage and are on their way, which is exactly what the interface's own remarks say ("A deferred
result is not a failure and must not be retried"). Failing the line instead is what creates the free
item, so optimism is the safe direction here: the player pays with materials that arrive, rather than
receiving a craft for nothing. Log it. `Deferred` is still never a count and is still never summed —
that part of your original instruction stands.

**R3 — a short delivery is not terminal.** `Execute` must retry the same source for the remainder
while that source keeps making progress, and only fail the line when the source is exhausted
(returns 0). Combined with R1 a line the planner emitted should now be fully deliverable, so this is
belt-and-braces against the residual race — a destination that fills between the gate and the pull,
or an ownership change mid-frame. Bound the retry so a misbehaving source cannot spin.

**R4 — the gate must not be more optimistic than the executor.** `CanReach` (the panel's
"available" answer and `UpdatePlacement`'s entry) may only say yes when a pull would actually secure
the full requirement. After R1 this should follow; verify it rather than assuming it.

**R5 — make the orchestration testable and test it.** The three-shape handling, the retry, and the
all-or-nothing outcome are the load-bearing behaviour of this task and currently live only in
game-facing code that needs a live `Player` and `Inventory`. Extract the sequencing into a pure
`Logic/PullRunner.cs` that takes the decide-and-take step as a delegate, so it can be driven from
tests with a plain lambda. Then test it: a count that meets the line; a count that is short then
met (the retry); a source exhausted at 0; `Deferred` treated as satisfied; and the all-or-nothing
outcome where a line cannot be met. `Logic/` stays free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`
and is linked into the test project, and a new `Logic/` file still needs its `<Compile>` item.

**R6 — say plainly what remains racy.** R1–R3 close the deterministic hole. A genuine race still
exists between the availability gate and the pull: another player can take the last stack in that
instant. State that residual in your report and put it in the `AUTHOR-VERIFY` section rather than
claiming it is closed.

## Scope

Fix only what the findings name. Do not add ctrl-click key handling, station-feeding features, or
anything from a later task. Do not push to any remote.

## Verify and report

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK`.
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → all passing, 0 failing, plus your new
   `PullRunner` tests.
3. Commit, then write `task-13-fix-report.md` covering: what you changed for each of R1–R6; the new
   `PullRunner` tests and what each one proves; how `Execute` now reacts to each of `Take`'s three
   return shapes; the residual race from R6; build and test result lines; files changed; concerns.

Then reply with ONLY (under 15 lines): **Status** · commits · one-line build/test summary · concerns ·
the report path.
