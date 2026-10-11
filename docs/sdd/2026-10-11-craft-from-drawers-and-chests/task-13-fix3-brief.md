# Task 13 — fix round 3

Rounds 1 and 2 are verified correct: the original Critical (a line larger than one stack being
undeliverable) is closed, gate and executor now share one source-deliverability definition, the
`Execute` sequencing is extracted and tested, and the `requireOnlyOneIngredient` deferral matches
vanilla. One Critical remains, plus three text corrections to bundle.

## Critical #2 — the gates do not model destination capacity, so a failed pull still costs nothing

`MultiUserChest.ContainerHandler.PossibleDragAmount` clamps a routed `dragAmount` to the destination's
free space when it has no empty slot. So with a player inventory that is full and has no room in a
matching stack of the item, the removal is declined, `Take` returns `0`, `PullRunner` fails the line,
and `CraftPatches`'s prefix returns `false` to skip consumption. Vanilla has **already** created the
crafted item (`GetInventory().AddItem`) or placed the piece (`TryPlacePiece`) by that point, so the
craft or placement is free — and because the player carries none of the materials, `ConsumeResources`
would have had nothing to eat anyway.

This is deterministic, not a race, and it is the same class of defect as the original Critical: a gate
saying "yes" for an action the pull then cannot deliver. It is present at the task's base, so it is not
a regression from rounds 1–2 — but it is squarely inside what this task implements, and it is the
mirror of the project's top anti-goal, so this task cannot be approved over it.

### R7 — a storage-aware "yes" must mean the pull can be delivered

Make the availability gates honest about **destination capacity**, not just storage availability. A
gate may only answer "yes" when both hold:

1. storage can cover the shortfall (already true), and
2. the player's inventory can **accept** that shortfall — matching free stack space plus empty slots,
   per item, matching quality and world level.

Use the pure helpers that already exist for exactly this in `Logic/DrawerTake.cs`
(`MatchingFreeStackSpace`, `CanDeliver`, and `NextChunk` if you need stack granularity) rather than
inventing a second capacity rule; the drawer pull path already gates on them, so this makes the chest
path agree with the drawer path instead of diverging from it. If a small new pure helper is needed,
put it in `Logic/` with the others and register it in the csproj.

Apply this to **both** paths, because vanilla creates the object before consuming on both:

- the crafting path — the `HaveRequirements` postfix that decides whether the panel offers the recipe;
- the building path — the build-affordability gate that decides whether the piece can be selected and
  placed.

If you find that the building path cannot be gated this way — that placement is reachable without any
gate you can honestly influence — **say so plainly in your report with the evidence** rather than
approximating it. That would be a finding for the controller, not something to paper over.

Two consequences worth stating in your report rather than hiding:

- It is now possible for the panel to decline a craft that vanilla's own consumption would have made
  room for, because the pull needs room *transiently*, before the recipe frees any. That is the safe
  direction and it is deliberate: over-strict is recoverable, a free item is not. It also mirrors what
  the drawer path already does.
- With the gates honest, a failed pull should again be a genuine race only (another player emptying a
  source in the same instant). Do not claim the race is closed.

Do **not** try to fix this by rolling back the created item or by un-placing a piece.

## Bundled text corrections (from the same review)

- `IStorageSource.cs` (and the matching `VanillaChestSource.Take` remarks) state
  `delivered + routed == requested` as an unconditional invariant. MultiUserChest clamps the routed
  amount to `min(request, located stack, destination free space)`, so it does not hold when a line
  exceeds the located stack or the destination is short of room — the very case R1 targets. Reword to
  say the routed request covers the remainder, clamped by the located stack and the destination.
  The caller's behaviour is unaffected.
- `PullRunnerTests.GateAndExecutorAgree_ALineBiggerThanOneStack_IsRetriedAndSecured` exercises the real
  planner but a hand-written model of the executor, so its name overstates what it proves and a
  regression in the real cross-stack loop would not fail it. Rename it to say the executor side is
  simulated, and keep a comment stating the real loop cannot be covered in a pure test project.
- `ShapeDeferred_AfterAPartialDelivery_IsStillSatisfied` describes a source sequence the post-round-2
  chest source cannot produce. Correct the wording to present it as a runner-contract tolerance check.
  Fix wording only — never weaken an assertion to make a change pass.

## Scope and verification

Everything from rounds 1–2 still binds: `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; **never write a
`Container`'s inventory** (chest removal only through `MultiUserChestBridge.RemoveFromChest`, drawer
only through `ForceRemove`); `Logic/**` free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`; storage
keyed by **prefab name**; the planner stays all-or-nothing; new files registered in the legacy csproj's
explicit `<Compile>` items; no ctrl-click key handling; no station-feeding; **do not push to any
remote**.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 117 or more; add tests for the new capacity rule if it is pure, which it should be)
3. Commit, then append a **Fix round 3** section to `task-13-fix-report.md` covering: how each gate now
   accounts for destination capacity and which helper it uses; whether the building path could honestly
   be gated, with evidence; the two consequences above; the text corrections; build and test lines;
   what remains racy. Extend the `AUTHOR-VERIFY` section with a full-inventory check that would have
   reproduced Critical #2 before this round.

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report path.
