# Task 3 Report: Storage eligibility predicate

## What I implemented

Two new pure-logic files under `CraftFromDrawersAndChests/Logic/`:

- **`StorageKind.cs`** — `public enum StorageKind { Chest, Drawer }`.
- **`Eligibility.cs`** —
  - `public readonly struct StorageCandidate` with the eight public readonly fields
    `Kind`, `Position` (`Vec3`), `ObjectName`, `ParentIsShip`, `ParentIsWagon`,
    `ToggledOff`, `HasAccess`, `HasInventory`, plus **one constructor taking all eight
    values in the brief's declaration order** (resolution #2 — required because C#
    object-initializer syntax cannot assign to `readonly` fields).
  - `public static class Eligibility` with
    `bool IsEligible(in StorageCandidate c, Vec3 origin, float range, bool chestsEnabled,
    bool drawersEnabled, ChestTypeRules chestRules)`.

Predicate logic (order-independent conjunction, factored so the shared guards run once):

1. `ToggledOff` → `false`.
2. `Position.SqrDistanceTo(origin) > range*range` → `false` (i.e. `distance == range`
   is eligible; no `Math.Sqrt` is called per candidate).
3. `Kind == Drawer` → return `drawersEnabled` (chest type, access and inventory are
   irrelevant to a drawer).
4. Chest: `chestsEnabled && HasAccess && HasInventory && !chestRules.IsExcluded(name,
   parentIsShip, parentIsWagon)`.

Also added the two `<Compile>` entries to the legacy product csproj
(`Logic\StorageKind.cs`, `Logic\Eligibility.cs`) as required by resolution #1.

### Resolution #5 — null `objectName`

`ChestTypeRules.IsExcluded` throws `NullReferenceException` on a null name when a name
rule is enabled (Task 2's deferred minor). I **guarded at the call site** in
`Eligibility.IsEligible` with `c.ObjectName ?? string.Empty` — a missing name matches no
name-based rule — and left Task 2's file untouched. I added one extra test,
`ChestWithNullName_DoesNotThrow`, to lock that guard in (see "Concerns").

## What I tested and results

`EligibilityTests.cs` implements a real, concretely-asserting test for each of the eight
named tests in the brief, plus the null-name guard test:

| Test | Assertion |
|---|---|
| `ChestAtExactlyRange_IsEligible` | chest 10 away, range 10 → `True` |
| `ChestBeyondRange_IsNotEligible` | chest 10.5 away, range 10 → `False` |
| `ChestToggledOff_IsNotEligible` | favourable chest, `ToggledOff` → `False` |
| `ChestWithoutAccess_IsNotEligible` | favourable chest, `HasAccess=false` → `False` |
| `ChestWithNullInventory_IsNotEligible` | favourable chest, `HasInventory=false` → `False` |
| `ChestOpenByAnotherPlayer_IsEligible` | fully-favourable chest → `True`, with a load-bearing comment explaining `StorageCandidate` has no in-use field by design |
| `ChestsDisabled_DrawerStillEligible` | drawers on + chests off: drawer → `True`, and the same candidate as a chest → `False` |
| `DrawerIgnoresChestTypeRulesAndAccess` | drawer named `piece_chest_wood(Clone)`, `HasAccess=false`, `HasInventory=false`, all four chest rules on → `True` |
| `ChestWithNullName_DoesNotThrow` | chest with null name, name rules on → `True` (guard) |

Results:
- Focused: `--filter EligibilityTests` → **9/9 passed**.
- Full Tests project → **25/25 passed, 0 failed, 0 skipped, no warnings**.

## TDD Evidence

**RED** — after writing only `Tests/Logic/EligibilityTests.cs`:
```
dotnet test "…\Tests\CraftFromDrawersAndChests.Tests.csproj" --filter EligibilityTests
  CraftFromDrawersAndChests.Tests net462 failed with 2 error(s) (0.2s)
    EligibilityTests.cs(12,24): error CS0246: The type or namespace name 'StorageCandidate' could not be found
    EligibilityTests.cs(27,24): error CS0246: The type or namespace name 'StorageCandidate' could not be found
Build failed with 2 error(s) in 0.9s
```
Expected: the tests reference a type that does not exist yet, so the only possible red for a
brand-new API is a compile failure — assertion-level red is impossible before the symbol
exists.

**GREEN** — after adding `StorageKind.cs`, `Eligibility.cs` and the csproj entries:
```
dotnet test "…\Tests\CraftFromDrawersAndChests.Tests.csproj" --filter EligibilityTests
  NUnit3TestExecutor discovered 9 of 9 NUnit test cases
Test summary: total: 9, failed: 0, succeeded: 9, skipped: 0, duration: 1.3s
Build succeeded in 2.2s
```

Full run:
```
Test summary: total: 25, failed: 0, succeeded: 25, skipped: 0, duration: 1.2s
Build succeeded in 2.1s
```

## Files changed

- `CraftFromDrawersAndChests/Logic/StorageKind.cs` (new)
- `CraftFromDrawersAndChests/Logic/Eligibility.cs` (new)
- `Tests/Logic/EligibilityTests.cs` (new)
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` (2 `<Compile>` lines)

## Self-review findings

- `Logic/**` references only `System` (none of the new files import anything game-related);
  no `UnityEngine`, `Valheim`, `BepInEx` or `Jotunn` references — verified by reading the
  new files and the existing `Geometry.cs`/`ChestTypeRules.cs`.
- `net462` unchanged in both projects.
- No `Math.Sqrt` call in the predicate; comparison is on squared distances, and the
  `>`-then-return-false ordering makes `distance == range` inclusive.
- Constructor assigns every field in the brief's declaration order; fields are
  `public readonly` exactly as specified.
- Every test asserts something and the suite reports 0 skipped.
- No Nexus/Odin's QoL/`nexusID` anything.
- Git diff staged only the four intended files.

## Concerns

1. **One test beyond the brief's eight.** The brief's eight names are the contract and all
   eight exist verbatim; I added `ChestWithNullName_DoesNotThrow` because resolution #5
   made the null-name guard my call and I did not want to ship untested guard code. It is
   additive and does not alter any named test. Flagging it for the reviewer in case the
   controller wants it folded into an existing named test instead.
2. **Guard semantics.** `?? string.Empty` means a null name is *not* excluded by any
   name-based rule, so a malformed chest candidate could still be pulled from rather than
   crashing the scan. I judged leniency correct given the design says candidates are
   assembled from possibly-missing components, but a stricter reading is defensible.
3. The product csproj itself was not built (it needs the game/`.references` DLLs and is out
   of scope for this task); the compile entries were verified only by inspection and by the
   Tests project globbing the same files. Nothing else would catch a typo in those entries,
   which is exactly the risk resolution #1 called out.
