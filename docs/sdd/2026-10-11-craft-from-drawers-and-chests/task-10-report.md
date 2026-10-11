# Task 10 Report — Chest registry and vanilla source

Date: 2026-10-11
Branch: `feature/craft-from-drawers-and-chests`
Commit: `9b5ea85` — feat: chest registry and storage collection (4 files, +522)

## Context: a prior attempt was already in the tree

The working tree arrived with an unverified partial attempt: a new
`CraftFromDrawersAndChests/Storage/VanillaChestSource.cs` (~9.9 KB, never built) and three added
`<Compile>` lines in `CraftFromDrawersAndChests.csproj`. `Storage/StorageRegistry.cs` and
`Storage/ContainerTrackingPatches.cs` were missing entirely.

I did **not** rewrite `VanillaChestSource.cs`; I verified it against every ruling below and it
already complied, so it is unchanged (see "Verification of the prior attempt"). The `csproj` already
registered all three files; I re-confirmed the three `<Compile>` items are present and the product
builds them.

## What I implemented

- `CraftFromDrawersAndChests/Storage/ContainerTrackingPatches.cs` (new) — `[HarmonyPatch(typeof(Container))]`
  class with a `[HarmonyPostfix] AwakePostfix(Container)` that adds the instance to a static
  `List<Container>`, and a `[HarmonyPostfix] OnDestroyedPostfix(Container)` that removes it. The
  add is gated on CraftFromContainers 4.0.7's tracking predicate, ported verbatim (see below).
  Exposes `internal static IReadOnlyList<Container> Tracked` and `internal static void Clear()`.
- `CraftFromDrawersAndChests/Storage/StorageRegistry.cs` (new) — `public static IReadOnlyList<IStorageSource>
  Collect(Vec3 origin, FeatureFlags flags)` and `public static void Clear()`.
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` — the three `<Compile>` items for
  the new/untracked files (already present in the tree; verified).

## Rulings applied

| Ruling | Where | How |
|---|---|---|
| Key by **prefab name**, never `CountItems` | `VanillaChestSource.Count` | Sums `item.m_stack` for stacks whose `item.m_dropPrefab.name` equals the requested prefab name. `GetInventory().CountItems(...)` appears nowhere; the brief's wording is deliberately **not** followed. |
| Build `ChestTypeRules` from the six `FeatureFlags` ignore settings | `StorageRegistry.BuildChestRules` | Object-initializer copying all six, then passed to `Eligibility.IsEligible`. |
| `ToggledOff` reads the ZDO under `ConfigModel.ZdoKey`, hashed with `GetStableHashCode()` | `VanillaChestSource.ToggledOff` | `private static readonly int NoPullHash = ConfigModel.ZdoKey.GetStableHashCode();` then `zdo.GetBool(NoPullHash, false)`. No second key string invented. |
| An **in-use chest stays eligible** | `StorageRegistry` | No `IsInUse()` call anywhere. CFC's `GetNearbyContainers` *does* call `container.IsInUse()`; that filter is deliberately omitted. |
| Port CFC 4.0.7's other filters | `StorageRegistry` + `VanillaChestSource` + `Eligibility` | distance, chest-type rules, `Container.CheckAccess(long)` (reflection), `GetInventory() != null` → via `Eligibility`; the container-inside-`Piece` and guard-stone checks → applied in `StorageRegistry` (see deviation 1). |
| `VanillaChestSource.Take` is Task 11's | `VanillaChestSource.Take` | Throws `NotImplementedException` naming `MultiUserChestBridge.RemoveFromChest` and stating `Container.m_inventory` must never be written directly. Nothing calls it. |
| `StorageRegistry.Clear()` | `StorageRegistry.Clear()` | Delegates to `ContainerTrackingPatches.Clear()`, dropping every tracked container so the next `Collect` starts empty. |

### Evidence for the ported behaviour (primary artefacts, not web summaries)

I decompiled the **installed CraftFromContainers 4.0.70** (`UnderHeiz-CraftFromContainers`) with
Mono.Cecil in a child process, because the brief pins CFC 4.0.7 as the behavioural baseline:

- `BepInExPlugin/Container_Awake_Patch` is `[HarmonyPatch(Container, Awake)]` with a **postfix**
  `(Container __instance, ZNetView ___m_nview)` that starts the `AddContainer` coroutine.
- `BepInExPlugin/Container_OnDestroyed_Patch` is `[HarmonyPatch(Container, OnDestroyed)]` with a
  prefix that does `containerList.Remove(__instance)`.
- The `AddContainer` predicate is, in IL: `container.GetInventory() != null && nview?.GetZDO() != null
  && (name.StartsWith("piece_") || name.StartsWith("Container") || nview.GetZDO().GetLong("creator".GetStableHashCode(), 0L) != 0L)`.
  (`cgt.un` against `0L` is `!= 0`.) This is `ShouldTrack` verbatim.
- `BepInExPlugin.GetNearbyContainers` filters each tracked container by:
  `container != null && transform != null`; `distance < maxRange` (skipped when `maxRange <= 0`);
  `AllowContainerType`; `CheckAccess(localPlayer.GetPlayerID())` via cached `AccessTools.Method`;
  `!container.IsInUse()`; `container.GetComponentInParent<Piece>() != null`;
  `container.GetInventory() != null`; `!m_checkGuardStone || PrivateArea.CheckAccess(pos, 0f, false, false)`.
- `AllowContainerType` is `container.transform.parent?.GetComponent<Ship>()` for the ship rule,
  `container.m_wagon != null` for the wagon rule, and the four `piece_chest*` name prefixes — the same
  four `ChestTypeRules` already encodes.

I also dumped the **publicized** and the **real** (unpublicized) `Container` from
`.references/Valheim/1.0.16/assembly_valheim_publicized.dll` and the installed
`Valheim_Data/Managed/assembly_valheim.dll`. The real type confirms which members are safe to touch
at runtime without reflection: `m_rootObjectOverride`, `m_wagon`, `m_checkGuardStone` are **public**
(so `VanillaChestSource` may read them), while `m_nview`, `m_inventory`, `Awake`, `OnDestroyed` and
`CheckAccess(long)` are **private** — `CheckAccess` is reached by reflection (as CFC does), `Awake`/
`OnDestroyed` are patched by Harmony (which handles private targets), and `m_nview` is never read
directly. `Container.Awake`'s IL is
`m_nview = m_rootObjectOverride ? m_rootObjectOverride.GetComponent<ZNetView>() : GetComponent<ZNetView>()`,
which is exactly `VanillaChestSource.ViewOf`, so the patch and the source resolve the same ZDO.

## Verification of the prior attempt (`VanillaChestSource.cs`)

Checked against each ruling; **no change required**:

- **Prefab keying** — `Count` matches `item.m_dropPrefab.name`; no `CountItems` call exists.
- **Toggle key** — `ConfigModel.ZdoKey.GetStableHashCode()`, identical to `DrawerSource`.
- **FC filters** — exposes `HasInventory` (`GetInventory() != null`), `HasAccess` (reflection
  `CheckAccess`), `ParentIsShip` (`transform.parent.GetComponent<Ship>()`), `ParentIsWagon`
  (`m_wagon != null`), `IsInsidePiece` (`GetComponentInParent<Piece>() != null`) and
  `PassesGuardStone` (`!m_checkGuardStone || PrivateArea.CheckAccess(pos, 0f, false, false)`), all
  exactly CFC's.
- **Runtime safety** — reads only members that are public in the *real* assembly (above), plus one
  reflective private call. No direct private field/`m_inventory` write.
- **`Take`** — throws a descriptive `NotImplementedException`, no MultiUserChest call.

## Deviation from the ruling's literal wording (flagged for the reviewer)

**1. Two of CFC's chest filters are applied in `StorageRegistry`, not inside `Eligibility`.**
The ruling says to port all of CFC's filters "through `Eligibility`". `StorageCandidate` (Task 3,
frozen by `Tests/Logic/EligibilityTests.cs`) has **no field** for "is inside a `Piece`" or "passes
the guard stone", and `Eligibility.IsEligible` has no way to express them. `Tests` links only
`Logic/**`, so `Eligibility`'s shape cannot be extended without editing Task 3's already-committed
file and all eight of its tests. The two checks are therefore applied at the call site in
`StorageRegistry.Collect`, using the `internal` helpers the prior attempt had already placed on
`VanillaChestSource` for exactly this purpose. Net behaviour is identical to CFC; only the layer
differs. If the reviewer prefers, the alternative is to extend `StorageCandidate`/`Eligibility` (a
Task 3 change) — I did not, to keep this task's diff to `Storage/` + `csproj`.

**2. The postfix resolves the ZNetView via `VanillaChestSource.ViewOf`, not by injecting
`___m_nview`.** `Container.Awake`'s IL proves `ViewOf` reproduces `m_nview`, and this avoids depending
on Harmony private-field injection. CFC injected the field; the effect is the same.

**3. "Harmony-postfixes ... `Container.OnDestroyed`"** — the brief says postfix, and I used a
postfix for both patches. CFC used a *prefix* on `OnDestroyed`. A postfix is safe here (the object is
still alive when `OnDestroyed` returns) and `Collect` additionally skips any destroyed container
found in the set, so a missed removal cannot cause a throw.

**4. No debug logging was added to `Collect`.** The brief's Step 4 asks for *temporary* logging
"printing each source's name, kind and distance"; that is an in-game verification action, not shipped
behaviour, and `isDebug` defaults `true`, so permanent per-call logging would spam. The step is
reproduced for the author in `AUTHOR-VERIFY` below.

## Build

Command: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`

Final lines:
```
  CraftFromDrawersAndChests -> ...\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll
    Built ...\CraftFromDrawersAndChests.dll (39,424 bytes)

BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```

`MSB3274` (MultiUserChest is `net472` against the project's `net462`) prints as expected and is not
mine to fix. The DLL grew from 30,720 B (Task 9) to 39,424 B.

## Tests

Command: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`

Output (tail):
```
NUnit3TestExecutor discovered 74 of 74 NUnit test cases using Current Discovery mode, Non-Explicit run
Test summary: total: 74, failed: 0, succeeded: 74, skipped: 0, duration: 1.2s
Build succeeded in 2.9s
```

74 = the Tasks 1–9 suite, all still green. **No tests were added**: every file this task touches is
game-facing (`UnityEngine`/Valheim types), and the `Tests` project links only `Logic/**`, so these
paths cannot be exercised outside the game. The pure decisions they lean on
(`Eligibility.IsEligible`, `ChestTypeRules.IsExcluded`) are already covered by Tasks 2–3.

## Self-review findings

- **`Logic/**` purity** — untouched; no new file added under `Logic/`. `StorageRegistry` and
  `ContainerTrackingPatches` live in `Storage/` and reference game types, as intended.
- **No `Container` inventory write, no toggle write, no pull execution, no MultiUserChest call** —
  none present. `ContainerTrackingPatches` only reads game state.
- **A chest with no inventory or a null ZDO is skipped, never thrown** — `ShouldTrack` returns false
  for a null container/destroyed object, a null inventory, a null view or a null ZDO;
  `StorageRegistry.Collect` skips `container == null` and `!source.IsValid` before touching
  `transform.position`.
- **Zero chests must not throw** — with an empty `Tracked` list the chest loop does not run and
  `Collect` returns an empty list. On a machine without Item Drawers, `DrawerApi.AllInRange` returns
  empty (Task 9's soft-dependency fallback).
- **No Nexus-flavoured artefact** — no URL, no path from another mod, no README/manifest change.
  Mod names appear only in doc/exception text, consistent with the existing `ChestTypeRules` and
  `DrawerSource` (both already name the projects they port from).
- **Single source of truth for literals** — the four chest prefixes stay in `ChestTypeRules`; the
  toggle key stays in `ConfigModel.ZdoKey`; `ContainerTrackingPatches` adds only the two *name*
  prefixes and the game's `"creator"` key, none of which exists elsewhere in the repo.
- **`Tracked` is exposed as `IReadOnlyList` over a mutable list** — the same pattern Task 9 used for
  `DrawerApi.NoHandles`; it is `internal` with one consumer, and callers are told not to mutate it.

## Concerns

1. The two filters applied at the call site rather than inside `Eligibility` (deviation 1). Behaviour
   matches CFC; only the layer differs. A reviewer who insists on the literal wording would need a
   Task 3 change.
2. `ContainerTrackingPatches` is only as good as `Container.OnDestroyed`; a container removed
   without firing it (a world unload) leaves a stale entry. `Collect` tolerates that by skipping
   destroyed objects, and `StorageRegistry.Clear()` exists for teardown, but nothing calls `Clear()`
   yet — Task 13/14 decides whether it is needed.
3. The `"creator"` ZDO key is a game key, not a mod constant, so it is a literal here (as in CFC).
   The game also exposes `ZDOVars.s_creator`; I kept the literal for exact baseline parity.

## AUTHOR-VERIFY

Cannot be checked without a live Valheim server and client. The steps are appended to
`.superpowers/sdd/2026-10-11-craft-from-drawers-and-chests/author-verify.md` (Task 10 section):

1. **Range filtering (brief Step 4).** Place two chests — one inside `Maximum range`, one outside —
   and a drawer inside range; add temporary logging in `Collect` printing each source's `ObjectName`,
   `Kind` and distance. Expected: the far chest is absent; the near chest and the drawer are present.
2. **Empty world.** With no chests in the world, `Collect` returns an empty list and logs no exception.
3. **Tracking predicate.** A normally built chest (`piece_chest(Clone)`) is tracked; a container that
   is neither `piece_`/`Container`-named nor carries a non-zero `creator` is not.
4. **Loose container.** A container not inside a `Piece` is skipped, not thrown on.
5. **Guard stone.** A guard-stone chest inside another player's guarded area is skipped where
   `PrivateArea` denies access, and included where it does not.
6. **Chest-name-prefix spot-check (deferred from Task 2).** Confirm `piece_chest_wood(`,
   `piece_chest_private(`, `piece_chest_blackmetal(` and `piece_chest(` match live Valheim prefabs.

---

# Task 10 Fix Report — review findings 1–3

Date: 2026-10-11
Branch: `feature/craft-from-drawers-and-chests`
Commit: `73c7a93` — fix: move Piece/guard-stone gate into the pure eligibility predicate
(5 files, +142/−46)

## Finding 1 (Important) — the eligibility gate is now whole, in the predicate

The two filters that were applied at the call site are now part of
`Logic/Eligibility.StorageCandidate` and enforced inside the pure predicate, so the predicate's
"single predicate" contract is true again.

- `StorageCandidate` gained `IsInsidePiece` and `PassesGuardStone` (two new constructor arguments,
  appended). These are the *observations* the game-facing caller computes; the *decision* is the
  predicate's.
- `Eligibility.IsEligible` enforces them: after the existing chest-only
  `!chestsEnabled || !HasAccess || !HasInventory` guard, `if (!c.IsInsidePiece || !c.PassesGuardStone)
  return false;`. A drawer returns `drawersEnabled` **before** that line, so both clauses are
  vacuously satisfied for `StorageKind.Drawer` — a drawer has no `Piece` requirement and no guard
  stone.
- `Storage/StorageRegistry.CollectChests` no longer tests `source.IsInsidePiece`/
  `source.PassesGuardStone` itself; it feeds them into the candidate. The only call-site check left is
  `source.IsValid` (a destroyed-object safety check before touching `transform.position`), which is
  not an eligibility decision. The two `internal` helpers on `VanillaChestSource` remain the
  game-facing observations.
- The predicate's doc comment now lists the two clauses, so it is a complete description of the
  decision, and the `StorageRegistry` class comment no longer describes the old split.

Covering tests (`Tests/Logic/EligibilityTests.cs`):

- `ChestOutsidePiece_IsNotEligible` — a chest with `IsInsidePiece == false` is not eligible.
- `ChestDeniedByGuardStone_IsNotEligible` — a chest with `PassesGuardStone == false` is not eligible.
- `DrawerIsUnaffectedByPieceOrGuardStone` — a drawer with both fields false is still eligible.
- The shared `Chest` fixture now defaults `isInsidePiece`/`passesGuardStone` to `true`, so every
  pre-existing chest fixture (and every other chest test) stays eligible; the `Drawer` fixture
  defaults them to `false`, which is what the exemption test relies on.

## Finding 2 (Minor) — unused `using` removed

`using UnityEngine;` was deleted from `Storage/StorageRegistry.cs` (line 3). After Finding 1 the file
references no UnityEngine type, so the build stays warning-free.

## Finding 3 (Minor) — `BuildChestRules` moved into `Logic/` and covered

- The six-field `FeatureFlags`→`ChestTypeRules` mapping is now
  `ChestTypeRules.FromFlags(FeatureFlags)` in `Logic/ChestTypeRules.cs`, next to the fields it fills.
  `StorageRegistry.Collect` calls it; the private `BuildChestRules` in the game-facing layer is
  deleted. No new file was created, so no `<Compile>` registration was required, and the `Tests`
  project's `Logic/**` link picks the change up automatically.
- Test `ChestTypeRulesTests.FromFlags_MapsEachIgnoreFlagToItsField` is parameterised over all six
  flags: each case enables exactly one `FeatureFlags` flag and asserts **all six** `ChestTypeRules`
  fields, so a dropped field, a swapped source flag or a copy-paste duplicate fails the case rather
  than silently disabling a chest-type rule.

## RED→GREEN evidence

RED (before the code changes; the new tests reference members that did not exist):

```
Tests\Logic\EligibilityTests.cs(25,24): error CS1729: 'StorageCandidate' does not contain a constructor that takes 10 arguments
Tests\Logic\EligibilityTests.cs(42,24): error CS1729: 'StorageCandidate' does not contain a constructor that takes 10 arguments
Tests\Logic\ChestTypeRulesTests.cs(132,47): error CS0117: 'ChestTypeRules' does not contain a definition for 'FromFlags'
```

GREEN (after):

```
Command: dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj
Passed!  - Failed:     0, Passed:    83, Skipped:     0, Total:    83, Duration: 109 ms - CraftFromDrawersAndChests.Tests.dll (net462)
```

83 = the 74 from Tasks 1–10 plus the 9 new (3 Eligibility cases + 6 `FromFlags` cases). 0 failing.

## Build

Command: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`

```
    Built ...\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll (39,936 bytes)

BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```

`MSB3274` prints as expected and is not mine to fix. DLL 39,424 B → 39,936 B. No `CS` warnings.

## Constraints re-checked

- `Logic/**` purity — greedy grep for `UnityEngine|Valheim|BepInEx|Jotunn` matches only doc-comment
  prose; no code reference.
- Storage stays keyed by **prefab name**; no `CountItems` call anywhere (the only mention is the
  comment explaining why it is avoided).
- An in-use chest stays eligible — no `IsInUse` test anywhere.
- `VanillaChestSource.Take` is still the descriptive `NotImplementedException`; no `Container`
  inventory write (the only `m_inventory` reads are `DrawerSource`'s *destination* inventory), no
  MultiUserChest call, no toggle-write or pull-execution code.
- No new file, so the legacy product project needs no new `<Compile>` item; `Tests` globs `Logic/**`.

## Still only verifiable in game

Unchanged from the original AUTHOR-VERIFY list. Items 4 and 5 are now *partly* covered: the decision
for a loose container and for a guard-stone denial is pinned in `EligibilityTests`, but that the
game-facing `VanillaChestSource.IsInsidePiece` / `PassesGuardStone` **observe** Valheim's `Piece` and
`PrivateArea` correctly can still only be confirmed in a live world (as can the range, empty-world and
tracking-predicate checks).
