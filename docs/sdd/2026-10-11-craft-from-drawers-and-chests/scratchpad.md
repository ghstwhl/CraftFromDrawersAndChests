# scratchpad — Task 15 is stuck (controller, 2026-10-11)

The project protocol says: *"Do not guess. If stuck, write a theory in `scratchpad.md`."* Kept here
rather than at the repo root so it does not add an untracked file to the shipped repository
(`.superpowers/` is gitignored).

## Facts

- **Five dispatches of Task 15 have produced nothing**: four implementer attempts and one
  recovery attempt. No files created, no commits, no report, no scratch scripts, no leftover
  processes (both shells idle; the persistent one ~406s CPU over 8.8h ≈ 1%), no error surfaced.
  Each returned "Agent completed with no output".
- **One earlier attempt did leave a partial draft**, still uncommitted: `ModInfo.cs` (+11, the
  console-command constants — reviewed, legitimate), `PullKeyPatches.cs` (+7), `PullEngine.cs` (+126),
  and untracked `Logic/StationFeedRules.cs`. `StationPatches.cs` and `TerminalPatch.cs` were never
  written. A later recovery dispatch did not advance it at all.
- **Other tasks in this session succeeded through the same dispatch path**, including several that
  returned no reply but left complete, committed work (Task 13 fix round 5, Task 14b). So "no output"
  has previously meant "reply lost, work done" — for Task 15 it consistently means no work at all.

## What is different about Task 15

It is the only task in the plan whose **original brief framed it as porting a subsystem that already
exists** ("port CraftFromContainers' behaviour for Fireplace/Smelter/CookingStation/Turret"). The first
three dispatches repeated that framing. After I verified the engine was already in place and rewrote the
scope (`task-15-scope.md`), the next two dispatches still produced nothing — so the framing alone does
not explain it, though it plausibly caused the first three.

## Hypotheses

1. **Large-artefact trap.** The natural first move for this task is to inspect CFC's station code and the
   Valheim station types. Evidence that agents in this project do run full decompiles:
   `%TEMP%\cfdac_full\assembly_valheim_publicized.decompiled.cs` (6.2 MB) and
   `%TEMP%\ab_decomp\AdventureBackpacks.decompiled.cs` (1.04 MB, from *this* session). If an agent starts
   a big decompile with output captured — or one of the unbounded probes that cost an hour earlier — it
   may exhaust its budget and die before writing anything. This is the only hypothesis consistent with
   "zero work" rather than "partial work".
2. **Adapter/harness fault specific to this workstream.** "No output" has been observed both with and
   without work; for Task 15 it is consistently without. Possibly an early tool call errors and the
   harness swallows the failure.
3. **Scope paralysis.** The brief's size (four stations + `fillAllModKey` + a console command) may lead an
   agent to plan extensively and never commit to writing. Weak: it does not explain producing no text.

## Distinguishing test (cheap, do this before another real attempt)

Dispatch a **trivial** task through the same path — "create `file.txt` containing `hello`, commit it,
reply with the status line". If that also returns no output, the dispatch path itself is broken and no
amount of re-scoping will help. If it succeeds, the task content is implicated and hypothesis 1 or 3
holds, which points at splitting the task and forbidding decompiles.

## Recommended next steps

1. Run the distinguishing test above.
2. Regardless of its result, **split Task 15**: 15a = the `Inventory`-variant refactor plus the four
   station patches; 15b = `fillAllModKey` plus the console command. Smaller, independently verifiable
   units, as was done successfully for 14b.
3. Forbid decompilation in Task 15 dispatches outright — the station semantics can be read from CFC's
   source under `.references`, and the Valheim station types can be confirmed with the sanctioned probe
   (`tools\cecil.ps1 -Fields`), neither of which needs a full decompile.
4. If the test shows the path is broken, implement Task 15 directly in the main session instead.
