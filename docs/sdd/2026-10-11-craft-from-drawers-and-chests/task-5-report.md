# Task 5 report — Config model

## What I implemented

Two pure-logic files plus their product-csproj `<Compile>` entries, and one test file.

**`CraftFromDrawersAndChests/Logic/ConfigModel.cs`** — `static class ConfigModel`, the single
source of truth for the config surface:

- Sections: `SectionServer = "1 - Server"`, `SectionKeys = "2 - Keys"`, `SectionDebug = "9 - Debug"`.
- All 24 key-name constants named by the brief. Three carry the brief's pinned strings
  (`MaxRange = "Maximum range"`, `PullItemsKey = "Pull items key"`, `ToggleStorageKey = "Toggle storage key"`);
  the other 21 follow the same Sentence-case pattern (confirmed with the controller — see Concerns).
- Defaults the brief pins, as constants: `DefaultMaxRange = 4f`, `DefaultEnableChests = true`,
  `DefaultEnableDrawers = true`, `DefaultLeaveOne = false`, `DefaultPullByDistance = false`.
- `ZdoKey = "CFDAC_NoPull"` — the per-storage off-switch ZDO key, placed here per ledger Ruling 3
  so Tasks 9/10/12 share one literal. Name and value are taken verbatim from the plan's Task 12
  interface; confirmed with the controller.
- `static string[] ParseList(string csv)` — total: null/empty → `new string[0]`; otherwise split on
  `','`, `Trim()` each part, drop empty (incl. whitespace-only) entries; never returns null.

**`CraftFromDrawersAndChests/Logic/FeatureFlags.cs`** — `sealed class FeatureFlags`, a plain mutable
class with one auto-property per setting (24 in total, mirroring the key list). No initializers and
no BepInEx/Unity types. `FlashColor`/`UnFlashColor` are `string`, because `Logic` may not reference
`UnityEngine.Color`. The game-facing `Config.cs` (Task 8) fills this from `ConfigEntry` values.

No `nexusID`, no Odin's QoL keys — both out of scope by constraint.

## What I tested and the results

`Tests/Logic/ConfigModelTests.cs`, one concrete assertion per brief-named test:

| Test | Assertion |
|---|---|
| `ParseList_TrimsSplitsAndDropsEmpties` | `"a, b ,,c"` → `["a","b","c"]`; whitespace-only entry `" x ,  , y "` → `["x","y"]` |
| `ParseList_EmptyString_IsEmptyArray` | result `Is.Not.Null` and `Is.Empty` |
| `ParseList_Null_IsEmptyArray` | result `Is.Not.Null` and `Is.Empty` |
| `Defaults_MaxRangeIsFour` | `ConfigModel.DefaultMaxRange == 4f` |
| `Defaults_BothStorageKindsEnabled` | `DefaultEnableChests == true` and `DefaultEnableDrawers == true` |
| `Defaults_LeaveOneOffAndPullByDistanceOff` | `DefaultLeaveOne == false` and `DefaultPullByDistance == false` |

Focused run: **6/6 passed, 0 skipped.** Full suite: **40/40 passed, 0 skipped** (34 previous + 6 new).
No build warnings.

## TDD evidence

**RED** — `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter ConfigModelTests`

```
CraftFromDrawersAndChests.Tests net462 failed with 9 error(s) (0.2s)
  ...ConfigModelTests.cs(13,25): error CS0103: The name 'ConfigModel' does not exist in the current context
  ... (8 more identical CS0103s)
Build failed with 9 error(s) in 1.1s
```

Expected failure: the Tests project globs `..\CraftFromDrawersAndChests\Logic\**\*.cs` directly, so
before the type exists the new test file cannot compile. For a brand-new type a compile error *is*
the correct RED — there is no way for the assertions to run against a type that does not exist yet.

**GREEN** — `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter ConfigModelTests`

```
NUnit3TestExecutor discovered 6 of 6 NUnit test cases using Current Discovery mode, Non-Explicit run
Test summary: total: 6, failed: 0, succeeded: 6, skipped: 0, duration: 1.4s
Build succeeded in 2.6s
```

Full suite (`dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`):
`total: 40, failed: 0, succeeded: 40, skipped: 0`.

## Files changed

Commit `b8c07d3` — `feat: config key model and list parsing` (4 files, +182):

- `CraftFromDrawersAndChests/Logic/ConfigModel.cs` (new)
- `CraftFromDrawersAndChests/Logic/FeatureFlags.cs` (new)
- `Tests/Logic/ConfigModelTests.cs` (new)
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` (+2 `<Compile>` entries, kept alphabetical)

## Self-review findings

- `Logic/**` dependency constraint holds: the only `BepInEx`/`UnityEngine`/`Valheim`/`Jotunn`
  occurrences in the new files are inside XML doc comments — no real references (grep-verified).
- csproj entries placed alphabetically beside the existing `Logic\` items; the Tests glob discovery
  is proven by the run (the 6 new cases executed).
- `FeatureFlags` property count (24) matches the brief's key list exactly; no extra settings.
- No file outside the task's scope was touched (`git status` clean after commit).

## Concerns

1. **Two brief/ledger gaps were resolved by asking the controller before coding** (both confirmed):
   - *ZDO key constant.* Ruling 3 puts the off-switch ZDO key constant in `ConfigModel` (T5), but
     the task brief never names it and says "do not invent others". The controller confirmed I should
     add it; the name/value (`ZdoKey = "CFDAC_NoPull"`) are taken verbatim from the plan's Task 12
     interface (line 632), not invented here.
   - *Defaults placement.* The brief pins the default **values** but not which type owns them. The
     controller chose **`ConfigModel` default constants** (not `FeatureFlags` initializers), so
     `FeatureFlags` is a plain, non-defaulted DTO and the `Defaults_*` tests read `ConfigModel`.

2. **Only 3 of the 24 key strings are brief-pinned.** The brief lists identifier + string for
   `MaxRange`, `PullItemsKey`, `ToggleStorageKey` (and the 3 sections), but for the other 21 it lists
   only the identifier. Per the controller's confirmation I used Sentence-case human phrases matching
   the pinned pattern. These strings are user-visible, and they are now convention-derived rather
   than brief-pinned. If any should match CraftFromContainers 4.0.7's exact wording, they are a
   literal-only change (no identifier is affected). As written:
   `Enable chests`, `Enable drawers`, `Leave one`, `Pull by distance`, `Pull fuel`, `Pull ore`,
   `Pull cooking`, `Fuel disallow types`, `Ore disallow types`, `Resource string`, `Pulled message`,
   `Flash color`, `Unflash color`, `Ignore ship containers`, `Ignore wagon containers`,
   `Ignore wood chests`, `Ignore private chests`, `Ignore black metal chests`,
   `Ignore reinforced chests`, `Enable mod`, `Debug`.

3. **`FlashColor`/`UnFlashColor` are `string` in `FeatureFlags`,** because `Logic` may not reference
   `UnityEngine.Color` while the brief says Task 8 binds them as `ConfigEntry<Color>`. Task 8 must
   convert `Color` → string when it assigns into `FeatureFlags` (and the consumer must parse it back).
   Flagged for Task 8.

4. **Only the 5 brief-pinned defaults are constants in `ConfigModel`.** Task 8 owns the defaults for
   the remaining entries. A caller that skips `Config.ReadInto` sees an all-zero/false `FeatureFlags`
   — intentional given the controller's choice, but worth noting for Tasks 8/10.
---

# Fix round (2026-10-11) — review findings 1–3

## What I changed

**Finding 1 — three retained CraftFromContainers settings were missing from the config surface.**
Added to `Logic/ConfigModel.cs`, with the other key/input settings rather than in a new section:

- Key names, Sentence case to match the three pinned strings:
  `PreventModKey = "Prevent mod key"`, `FillAllModKey = "Fill all mod key"`,
  `SwitchPrevent = "Switch prevent"`.
- Defaults, following the existing `Default*` pattern: `DefaultPreventModKey = "left alt"`,
  `DefaultFillAllModKey = "left shift"`, `DefaultSwitchPrevent = false`. These three are CFC 4.0.7's
  own defaults, read from its source rather than guessed:

  ```
  110: preventModKey = Config.Bind<string>("Hot Keys", "PreventModKey", "left alt", ...)
  111: pullItemsKey  = Config.Bind<string>("Hot Keys", "PullItemsKey",  "left ctrl", ...)
  112: fillAllModKey = Config.Bind<string>("Hot Keys", "FillAllModKey", "left shift", ...)
  108: switchPrevent = Config.Bind<bool>  ("Hot Keys", "SwitchPrevent", false, ...)
  ```
  Source: `raw.githubusercontent.com/aedenthorn/ValheimMods/master/CraftFromContainers/BepInExPlugin.cs`;
  its `[BepInPlugin(..., "4.0.7")]` confirms that branch is the 4.0.7 the brief pins.

Added the matching members to `Logic/FeatureFlags.cs`, alongside `PullItemsKey`/`ToggleStorageKey`:
`string PreventModKey`, `string FillAllModKey`, `bool SwitchPrevent` — both keys are `string`,
consistent with the existing key members. No behaviour implemented: Task 14 reads the first two,
Task 15 reads `FillAllModKey`. The new defaults are regression-locked by extending the defaults
coverage in `Tests/Logic/ConfigModelTests.cs` with `Defaults_PreventAndFillAllKeysAndSwitch`.

**Finding 2 — flash colours removed from `FeatureFlags`.**
Deleted `FeatureFlags.FlashColor` and `FeatureFlags.UnFlashColor` (the lossy `string` seam; Task 8
binds them as `ConfigEntry<Color>`). Kept the `ConfigModel.FlashColor` / `ConfigModel.UnFlashColor`
key-name constants that Task 8 still needs to bind the entries. No `ConfigModel.Default*` existed for
a flash colour, so nothing was removed there.

**Finding 3 — single-writer contract documented.**
Added an `<remarks>` block to `FeatureFlags` recording that `Config.ReadInto` (Task 8) is its only
writer, that it runs from `Plugin.Awake` before any Harmony patch is applied, and that consumers read
the shared instance rather than constructing their own. Comment only; no code change.

## Commands and output

Covering tests (new case included):

```
> dotnet test "Tests\CraftFromDrawersAndChests.Tests.csproj" --filter ConfigModelTests --nologo
NUnit3TestExecutor discovered 7 of 7 NUnit test cases using Current Discovery mode, Non-Explicit run
Test summary: total: 7, failed: 0, succeeded: 7, skipped: 0, duration: 1.3s
Build succeeded in 3.5s
```

Full suite, once:

```
> dotnet test "Tests\CraftFromDrawersAndChests.Tests.csproj" --nologo
NUnit3TestExecutor discovered 41 of 41 NUnit test cases using Current Discovery mode, Non-Explicit run
Test summary: total: 41, failed: 0, succeeded: 41, skipped: 0, duration: 1.1s
Build succeeded in 2.0s
```

41 = the previous 40 plus the 1 new defaults test. No build warnings, no skipped tests.

No files were added, so `CraftFromDrawersAndChests.csproj` needed no new `<Compile>` entry. Confirmed
with `git status --short` (only the three files below are modified) and
`git ls-files --others --exclude-standard` (empty).

## Commit

`a4f9772` — `fix: complete CFC key config surface, drop flash colours from FeatureFlags`
(3 files changed, +29 −2).

## Concerns

1. The three new key-name strings (`"Prevent mod key"`, `"Fill all mod key"`, `"Switch prevent"`) are
   convention-derived Sentence case matching the file's existing pattern; the brief pins no literal
   for them. They are user-visible, and they are a literal-only change if the wording should differ.
2. `ConfigModel` still carries `FlashColor`/`UnFlashColor` key names with no `FeatureFlags`
   counterpart (by design — Task 8 exposes the colours through a game-facing accessor). The
   `FeatureFlags` summary line "one property per setting" is therefore now loose; I left it untouched
   to keep the Finding-3 diff to the added `<remarks>` only.