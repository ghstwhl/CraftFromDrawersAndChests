# Task: fix the total in-game failure (root cause verified)

## What happened

The owner loaded the 0.1.0 Release build into Valheim and **nothing worked**: no pulling from chests, no
pulling from drawers, and the per-storage switch did nothing. All three symptoms have **one** cause, read
straight out of the session log
(`…\profiles\Modheim-interim\BepInEx\LogOutput.log`):

```
[Info : BepInEx] Loading [CraftFromDrawersAndChests 0.1.0] (NorCal_Nerds.CraftFromDrawersAndChests)
[Error: Unity Log] ArgumentException: Config file must not be a default mod config:
  NorCal_Nerds.CraftFromDrawersAndChests. It is already synchronized
  Jotunn.Managers.SynchronizationManager.RegisterCustomConfig (ConfigFile customFile) (SynchronizationManager.cs:166)
  CraftFromDrawersAndChests.PluginConfig.Init (ConfigFile config)
  CraftFromDrawersAndChests.Plugin.Awake ()
...
[Message: BepInEx] Chainloader startup complete (28 loaded, 0 skipped, 0 failed)
```

`Plugin.Awake` is `PluginConfig.Init` → `ReadInto` → `Harmony.PatchAll()`. The exception aborts `Awake`
before `PatchAll`, so **not a single patch was applied** — which is why every feature is dead rather than
one or two. Note BepInEx reported `0 failed`, so the failure was **silent**.

## Critical fix — `PluginConfig.cs:152`

```csharp
SynchronizationManager.Instance.RegisterCustomConfig(config);   // <-- throws, delete it
```

Jötunn's own documentation for that method (`.nuget\packages\jotunnlib\2.30.2\lib\net462\Jotunn.xml`):

> Registers a **non default** config file for possible synchronisation with all clients. Entries still
> need the IsAdminOnly attribute in order to be synchronized.
> **Exception: `ArgumentException` — The config file is a default mod config and is already implicitly
> synchronized.**

A plugin's own `Config` **is** a default mod config, so this call can never be correct for it. Jötunn
already synchronizes it. **Delete the call.**

Everything else Task 8 built is right and must stay: every entry carries
`ConfigurationManagerAttributes { IsAdminOnly = true }` (`PluginConfig.cs:216`), which is exactly what
Jötunn requires, and `SynchronizationManager.OnConfigurationSynchronized += RefreshFlags` (`:150`) is the
correct way to re-read the snapshot after the server's values are pushed (`:204`).

Add a comment at the deletion site recording *why* the call must not come back — a future reader
"restoring" it would silently kill every feature again.

## Verify — is the Task 8 feature still intact?

Removing the call is only correct if implicit synchronisation actually delivers what Task 8 promised
(clients inherit the server's values, non-admin entries locked). Confirm from the Jötunn XML docs and say
what you found:

- Does a default mod config get synchronised without any registration call?
- What does `Jotunn.Utils.SynchronizationModeAttribute` / `AdminOnlyStrictness` (`Always`,
  `IfOnServer`) control, and does the plugin need the attribute on its `Plugin` class for the
  "non-admin clients see the entry read-only" requirement? If it does, add it — **that requirement is
  the whole point of Task 8.**
- Report which Jötunn member you relied on for each answer. Do not decompile.

## Important — make this class of failure loud, and unable to kill everything

Two separate problems made this worse than it needed to be:

1. **One config error disabled the entire mod.** Guard the config initialisation in `Plugin.Awake` so a
   throw there cannot prevent `Harmony.PatchAll()`. Log an error via `Log.LogError` naming what failed.
2. **It was silent.** BepInEx said `0 failed`; the owner had to report it in game. Make the guard log
   loudly enough to be found in `LogOutput.log`.

Be careful about `FeatureFlags`: it is a **non-defaulted** DTO, so if initialisation fails partway every
flag reads `false` and the mod is inert-but-alive. Decide and state what the guard should do — patch
anyway with a loud error, or fail loudly *and* visibly — and make sure the chosen behaviour cannot
produce a silent no-op.

## Pre-flight — audit every Harmony patch target while you are here

We know the config call throws. **We do not yet know whether `PatchAll()` would have succeeded**, because
execution never reached it. If any `[HarmonyPatch]` target is missing or mis-typed and is not guarded by
`[HarmonyPrepare]`, `PatchAll` throws and we get a second total failure.

Check **every** patch in the product and report a table: declaring class, target type, target member,
whether that member exists in the publicized assembly, and whether a `[HarmonyPrepare]` guard protects
it. Use the sanctioned probe:

```
powershell -NoProfile -ExecutionPolicy Bypass -File .superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\tools\cecil.ps1 -Assembly .references\Valheim\1.0.16\assembly_valheim_publicized.dll -Method <Name> -Definitions
```

Note the recurring trap: **a member that is `private` in the unpublicized assembly may still be a valid
Harmony patch target** (patching does not call it), but it must never be *called* directly — that is the
R29/R30 rule from Task 15a. Report both facts per member: patchable, and whether we call it.

## Constraints

- `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`.
- `Logic/**` stays free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`.
- Never write a `Container`'s inventory or a drawer's ZDO `"Amount"`.
- Storage keyed by **prefab name**. `-1` (`ChestTake.Deferred`) never a count, never summed, never retried.
- Register new files in the legacy csproj's explicit `<Compile>` items, and **verify new types on the
  built DLL** — a file omitted from a legacy csproj compiles out silently while build and tests stay green.
- No scratch or inspection scripts in the tree. No Nexus artefacts or other mods' identity anywhere.
- `MSB3274` is expected — do not touch it. **Do not decompile anything; do not run `ilspycmd`.** Use
  `tools\cecil.ps1` only, never an ad-hoc probe.
- **Do not push to any remote** — the owner pushes commits in batches.

## Verify and report

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing
3. Then build the package: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1` (Release) → confirm
   `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Release)` and report the DLL size and the zip path, so the
   owner has something to install.
4. Commit, then write a report covering: the deleted call and the comment replacing it; what you confirmed
   about implicit synchronisation and `AdminOnlyStrictness`, with the Jötunn member cited; the `Awake`
   guard's behaviour and why it cannot be silent; and the **complete patch-target audit table**.

Reply with ONLY (under 15 lines): **Status** · commit · build/test/package summary · concerns · report path.
