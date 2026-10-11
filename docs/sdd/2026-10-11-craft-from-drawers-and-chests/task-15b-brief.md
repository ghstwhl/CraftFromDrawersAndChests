# Task 15b: fill-all key and console command

The remainder of the original Task 15, after the station work was split out as 15a (complete).

## Oracle — read this, do not decompile anything

`.references\Mods\CraftFromContainers\BepInExPlugin.cs` — CraftFromContainers **4.0.7**, 75 KB of plain
C# text. Read it with normal file tools. **Do not run `ilspycmd` and do not decompile any assembly.** Two
workers have already lost an hour to unbounded decompile loops, and four dispatches of the original task
produced no work at all.

For any assembly fact use the sanctioned probe, which now reports **visibility** for methods and fields:

```
powershell -NoProfile -ExecutionPolicy Bypass -File .superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\tools\cecil.ps1 -Assembly <dll> -Method <Name> [-Fields] [-Definitions] [-Callers]
```

## What to build

### 1. `fillAllModKey`

`ConfigModel.FillAllModKey` (`"Fill all mod key"`, default `"left shift"`) is bound in `PluginConfig` but
**unused** — a retained CraftFromContainers feature that is currently inert, the same defect that had to be
fixed twice before (`resourceString`, then the prevent key). Find its real semantics in CFC and implement
them. It is a modifier held while filling, so get the polarity and the trigger from the oracle rather than
assuming.

Where it applies depends on what CFC does with it — verify rather than guess, and say in your report which
call sites you gated and which CFC lines justify each.

### 2. `Patches/TerminalPatch.cs` — the console command

`ModInfo.cs` already carries the constants (committed in 15a):

- `ModInfo.ConsoleCommand` = `"craftfromdrawersandchests"`
- `ModInfo.ConsoleCommandAlias` = `"cfdac"`

They were chosen to mirror CFC's own command naming (`craftfromcontainers reset`, CFC 4.0.7 ~1498-1503),
which implies CFC's command takes **subcommands**. Mirror CFC's command shape — its name form, its
subcommands, and its output — then add this project's own status report: **how many sources are in range
and whether the drawer API resolved.**

`StorageRegistry.Clear()` exists and is documented as "for tearing a world down (a reload or a return to the
main menu) rather than for routine use" — if CFC's `reset` subcommand does the equivalent, wire ours to it,
and preserve that warning's intent (the patch re-adds containers as they awake, so a reset mid-session
leaves the tracked set incomplete until objects re-awake — say so in the report and in the command's help
text if you expose it).

Register the new file in the legacy csproj's explicit `<Compile>` items — a file left out of a legacy csproj
compiles out silently while build and tests stay green, which already happened once on this task.

### 3. R29/R30 — non-public members

**Call only members that are `public` in the unpublicized assembly
(`.references\Valheim\1.0.16\Managed\assembly_valheim.dll`) directly.** Anything else goes through
reflection, and prefer removing the dependency entirely when a public equivalent exists (that is how the
last two rounds were closed). Check **every** vanilla member and field you touch with the probe — methods
via `-Definitions`, fields via `-Fields` — and put the resulting table in your report. Sampled checks are
what the previous two rounds were correcting. `Player::m_placementStatus` is `private` (which is why
`PullKeyPatches.cs:64` reflects it); the station fields are mostly `public`.

## Constraints

- `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`.
- `Logic/**` must not reference `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`. Put anything pure into `Logic/`
  with tests — that is where this project's defects have been caught.
- **Never write a `Container`'s inventory or a drawer's ZDO `"Amount"`.** Chest removal only through
  `MultiUserChestBridge.RemoveFromChest`; drawer removal only through `ForceRemove`.
- Storage is keyed by **prefab name** (`"Wood"`), never the localized `m_shared.m_name`.
- `-1` (`ChestTake.Deferred`) is never a count, never summed, never retried.
- **Items must never end up on the ground, and nothing may be pulled that cannot be delivered.**
- Leave no scratch or inspection scripts in the tree. No Nexus artefacts; no other mod's identity, feature
  description, path or URL anywhere in this repository.
- `MSB3274` is expected — do not fix or suppress it.
- **Do not push to any remote.** The repository owner pushes commits in batches; just commit locally and
  say so.

## Verify and report

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 205 or more)
3. **Skip in-game verification** — write those steps as `AUTHOR-VERIFY` in your report.
4. Commit, then write `.superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\task-15b-report.md`:
   `fillAllModKey`'s real semantics from CFC and each call site you gated; the console command's name,
   subcommands and output with the CFC lines mirrored; how the status report counts sources and reports the
   drawer API; the csproj registration; the **complete** visibility table for every vanilla member and field
   you touched; what went into `Logic/` with tests; build and test result lines; files changed;
   `AUTHOR-VERIFY`; self-review; concerns.

Then reply with ONLY (under 15 lines): **Status** · commit · one-line build/test summary · concerns · the
report path. Do not narrate your work in the reply.
