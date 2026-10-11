# Task 13 — fix round 5

Round 4's R8 is verified correct: the reservation is the **exact maximum** the crafting-skill bonus can
add (`perRoll · n(n+1)/2`), confirmed against the decompiled `DoCrafting` rather than the brief — the
implementer was right to override the brief's flat formula. One Important remains, and it is the only
thing holding this task open.

## Important — R9's condition is right but is read at the wrong moment

`CraftPatches` decides `isUpgrade` from `InventoryGui.instance.m_craftUpgradeItem`
(`CraftPatches.cs:291-292`). That field is assigned in **exactly one place**: `OnCraftPressed`
(decompiled `InventoryGui` line 2219). At the point the postfix runs, it is still `null`, because the
gate has not been pressed yet:

- `UpdateRecipe` computes `flag4 = player.HaveRequirements(m_selectedRecipe.Recipe, …)`
  (decompiled line 1671) and drives `m_craftButton.interactable` (line 1674) — that is the decision
  that enables the upgrade at all, and it happens *before* any press arms the flag.

So for a first-press upgrade the postfix sees `isUpgrade == false` and reserves `base + bonus` room for
an output that will actually be re-added **in place**. In the round-4 brief's own scenario (materials
only in storage, inventory full apart from room for the material shortfall) the upgrade button is
disabled and vanilla's in-place upgrade never runs — the exact outcome R9 exists to prevent.

The execution path is fine: the reviewer confirmed `DoCrafting`'s own requirement check reads the same
`m_craftRecipe`/`m_craftUpgradeItem` that `OnCraftPressed` set together, and the singleton is the same
one running `DoCrafting`, so a craft that really produces output cannot be gated as an upgrade. **The
execution path is safe; only the panel gate is wrong.** That is why this is Important and not Critical:
it over-reserves, which is the recoverable direction.

### R10 — recognise the upgrade at the panel gate

Derive `isUpgrade` from the recipe actually being evaluated rather than from a field that is only armed
on press. `OnCraftPressed` copies `m_selectedRecipe.ItemData` into `m_craftUpgradeItem`, so
`InventoryGui.instance.m_selectedRecipe.ItemData != null` identifies the same condition one step
earlier — but verify that equivalence against the decompiled assembly rather than accepting it from
this brief, and prefer passing the craft context in from the call site if that is cleaner and the
call site can supply it. The point is that the panel's answer and the execution's answer must agree.

**The direction that matters:** if you make `isUpgrade` true for a craft that will really produce new
output, you reserve nothing and reopen the free-craft window — an **under**-reservation, which is
Critical. So whichever source you use, it must be exactly `OnCraftPressed`'s condition and no wider.
Over-recognising an upgrade is not safe here; under-recognising one is merely the behaviour you are
fixing.

Both the panel gate and the execution gate must end up with the same answer for the same recipe.

## Tests

The panel-versus-execution agreement is the behaviour to pin. If the fix can be expressed as a pure
rule in `DestinationCapacity` (e.g. a predicate taking "is this craft an upgrade") it must be tested;
if it can only be verified against the game, say so plainly rather than implying coverage.

## Scope and verification

Everything from rounds 1–4 still binds: `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; **never write a
`Container`'s inventory** (chest removal only through `MultiUserChestBridge.RemoveFromChest`, drawer
only through `ForceRemove`); `Logic/**` free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`; storage
keyed by **prefab name**; `Deferred` never a count, never summed, never retried; the planner stays
all-or-nothing; new files registered in the legacy csproj's explicit `<Compile>` items; no scratch or
inspection scripts; no ctrl-click key handling; no station-feeding; **do not push to any remote**. Do
not revisit R1–R8 — they are verified.

All seven Minors raised across rounds 3 and 4 are **deferred**, not part of this round: `Matches`
duplicating `DrawerTake`'s predicate; world-level skew between the gate's model and the delivery; the
per-frame build-HUD cost; the differential test sharing `NextChunk`; the bonus reserved when no bonus
can fire; the theoretical clamp-to-`int.MaxValue` chunk loop; and the untested `CraftedOutput` wiring.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 134 or more)
3. Commit, then append a **Fix round 5** section to `task-13-fix-report.md` covering: where `isUpgrade`
   now comes from and the decompiled evidence that it is exactly `OnCraftPressed`'s condition and no
   wider; how the panel and execution answers are kept in agreement; the tests; build and test lines;
   what remains racy. Update the `AUTHOR-VERIFY` upgrade check so it describes the first-press case,
   since that is the one that was broken.

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report
path. Do not narrate your work in the reply.
