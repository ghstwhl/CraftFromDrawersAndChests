# Task 14b: build-piece pull key

**Why this task exists.** Task 14's review found that CraftFromContainers also pulls a **building
piece's** materials with the pull key, and that no task in the plan covered it. CFC's own
`pullItemsKey` description reads "crafting **or building**", so this is a retained feature that was
about to be silently dropped. The controller inserted this task between Tasks 14 and 15.

**Files:**
- Modify: `CraftFromDrawersAndChests/Patches/PullKeyPatches.cs` (preferred — same feature, the pull key)
- Create (only if the above becomes unwieldy): `CraftFromDrawersAndChests/Patches/BuildPullPatches.cs`

**Interfaces:**
- Consumes: `PullEngine.PullToPlayer(requirements, qualityLevel, multiplier, player, flags)` (Task 14),
  `PullKeyPatches.AllowByKey()` (Task 14), `PluginConfig.Flags`, `Logic/PullRunner.cs`,
  `Logic/DestinationCapacity.cs`.
- Produces: nothing new for later tasks.

## Read the reference implementation first — do not reconstruct it from this brief

**CFC's source is the oracle for this task, not this document.** It is available under `.references`;
the Task 14 review cited the build-pull at CFC `BepInExPlugin.cs:1096-1128` as
`UpdatePlacement_Patch`, pulling `selectedPiece.m_resources` on attack while the pull key is held.
Read that method (and CFC's placement-status guards around it) and mirror its behaviour and trigger
conditions. Where this brief and CFC disagree, **CFC wins** and you say so in your report. The last two
briefs I wrote both got a CFC behaviour wrong (the flash, and `SwitchPrevent`'s semantics), so treat
my prose as a starting point only.

## Requirements

1. **Mirror CFC's trigger.** Whatever CFC's `UpdatePlacement` patch keys on — the attack input, the
   pull key, the placement status, whether a piece is selected — reproduce it, including its guards.
   Report the exact conditions you implemented and the CFC lines they came from.
2. **Pull the selected piece's requirements**, via `PullEngine.PullToPlayer` — the same entry point
   Task 14 uses. Do not add a second pull path.
3. **Honour the prevent key** (`PullKeyPatches.AllowByKey()`), exactly as CFC's `AllowByKey()` gates
   its own paths. CFC's is `{ if (CheckKeyHeld(preventModKey)) return switchPrevent; return !switchPrevent; }`.
4. **Items must never end up on the ground**, and a placement must never cost nothing. If the pull
   cannot complete, pull nothing and drop nothing — refuse and say why, as Task 14's refusal path does.
   Note the build path already has a storage-aware gate: `Patches/CraftPatches.cs:114` postfixes
   `Player.HaveRequirements(Piece, Player.RequirementMode)`, which `Player.UpdatePlacement` calls
   before `TryPlacePiece`. Your change must not weaken that gate or let placement proceed unpaid.
5. **Feedback as CFC does it**: the pulled message on success, a failure message naming the missing
   items (de-duplicated), and the readout behaviour if CFC applies it here.
6. **`IStorageSource.Take`'s `-1` (`ChestTake.Deferred`)** is a routed removal whose outcome is
   unknowable synchronously. Never treat it as a count, never sum it, never retry it — `PullRunner`
   already handles it.

## Constraints

- Target framework `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`.
- `Logic/**` must not reference `UnityEngine`, `Valheim`, `BepInEx` or `Jötunn`. Put anything pure you
  can into `Logic/` with tests — that is where this project's defects have been caught.
- **Never write to a `Container`'s inventory or a drawer's ZDO `"Amount"`.** Chest removal only through
  `MultiUserChestBridge.RemoveFromChest`; drawer removal only through `ForceRemove`.
- Storage stays keyed by **prefab name** (`"Wood"`), never the localized `m_shared.m_name` (`"$item_wood"`).
- Register any new file in the legacy csproj's explicit `<Compile>` items.
- No scratch or inspection scripts in the tree. Decompile probes go under `%TEMP%` and get deleted.
- No Nexus-flavoured artefacts; no other mod's identity, feature description, path or URL may appear in
  this repository.
- `MSB3274` prints on every build and is expected — do not fix or suppress it.
- No station-feeding and no console command — that is Task 15.
- **Do not push anything to any remote.**

## R25 (standing ruling) — assembly inspection is mandatory-path

If you need to inspect an assembly, use the sanctioned probe and **do not write your own**:

```
powershell -NoProfile -ExecutionPolicy Bypass -File .superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\tools\cecil.ps1 -Assembly <dll> -Method <Name>
```

It fails fast, resolves its Cecil types before any loop, buffers output into a bounded summary, and
disposes the module. Verified at 1.3s end-to-end. An ad-hoc probe that tests an enum type literal
inside a per-instruction loop throws a **non-terminating** error and is what a previous worker lost an
hour to — it looks exactly like a hang.

**R26 (standing ruling):** a scan that matches on method name only cannot distinguish overloads. Always
report parameter types at each call site.

## Verify and report

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)` (note: `tools\Build.ps1` needs `-ExecutionPolicy Bypass`)
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 157 or more)
3. **Skip in-game verification.** Write the steps a human would run as an `AUTHOR-VERIFY` section in
   your report.
4. Commit, then write `.superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\task-14b-report.md`
   covering: CFC's actual build-pull behaviour and the lines you mirrored, and anywhere this brief
   disagreed with it; the exact trigger conditions you implemented; how you kept items off the ground
   and placement paid-for; the prevent-key wiring; what you moved into `Logic/` and its tests; build
   and test result lines; files changed; `AUTHOR-VERIFY`; self-review; concerns.

Then reply with ONLY (under 15 lines): **Status** · commits · one-line build/test summary · concerns ·
the report path. Do not narrate your work in the reply.
