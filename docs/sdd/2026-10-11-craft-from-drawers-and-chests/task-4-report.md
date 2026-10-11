# Task 4 Report: All-or-nothing pull planner

## What I implemented

Two new files (plus one product-csproj entry):

- **`CraftFromDrawersAndChests/Logic/PullPlan.cs`** — pure logic, in namespace
  `CraftFromDrawersAndChests.Logic`:
  - `public readonly struct PullRequest` — `Prefab`, `ItemName`, `Amount`,
    `IsUpgraderResource` + one constructor in declaration order (Ruling 10).
  - `public readonly struct StorageStock` — `SourceId`, `ItemName`, `Amount`, `Distance`
    + constructor in declaration order.
  - `public readonly struct PullLine` — `SourceId`, `ItemName`, `Amount` + constructor.
  - `public sealed class PullPlanResult` — ctor
    `(bool feasible, IReadOnlyList<PullLine> lines, IReadOnlyList<string> missingItemNames)`,
    exposing `Feasible`, `Lines`, `MissingItemNames` as read-only properties. `Lines` /
    `MissingItemNames` are coalesced to `Array.Empty<T>()`, never null.
  - `public static PullPlanResult PullPlan.Plan(requests, stocks, leaveOne, byDistance,
    allowUpgraderResources)`.
- **`Tests/Logic/PullPlanTests.cs`** — the nine named tests, each with a concrete
  assertion, plus three private helpers.
- **`CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj`** — added
  `<Compile Include="Logic\PullPlan.cs" />` beside the other `Logic\` entries (resolution #1).

### Algorithm (binding resolutions reflected)

1. Build an index order over `stocks` (given order, or **stable** insertion-sorted by
   ascending `Distance` when `byDistance`) — resolution #8. Requests are still processed in
   their given order.
2. One shared `int[] remaining` working copy for the whole plan: `Amount`, or
   `Math.Max(0, Amount - 1)` when `leaveOne` — resolution #7 (no double-counting; negative
   cap impossible).
3. For each request in order: if `IsUpgraderResource && !allowUpgraderResources`, **skip**
   it entirely (no line, not missing) — resolution #6. Otherwise walk the ordered stocks,
   matching on `ItemName` only via `StringComparison.Ordinal` (never reads
   `PullRequest.Prefab`) — resolution #5, Ruling 12 — and draw `min(needed, remaining)` per
   stock, appending a `PullLine` and decrementing `remaining`.
4. If a request comes up short, record its `ItemName` and **keep going** — no short-circuit
   (resolution #4, Ruling 11). Only after every request is evaluated: if anything was short,
   return `Feasible = false` with an **empty** `Lines` and the full ordered
   `MissingItemNames`; otherwise return `Feasible = true` with the accumulated lines.

The whole plan is computed from the local `remaining` copy before anything is returned, so
no partial result ever exists and nothing external is mutated.

## What I tested and the results

`PullPlanTests.cs` implements a real assertion for each of the brief's nine test names:

| Test | Assertion |
|---|---|
| `RequirementExactlyMet_CreatesOneLine` | 1 line, `c1`/`Wood`/5, feasible, no missing |
| `RequirementSpreadAcrossSources_SplitsIntoLines` | 2 lines, per-source sums 2 + 3 |
| `RequirementShortByOne_IsInfeasibleAndHasNoLines` | 4 available vs 5 required → infeasible, `Lines` empty, missing `["Wood"]` |
| `InfeasiblePlan_ConsumesNothing` | Wood fully covered but Stone short → `Lines` empty (even Wood yields no line), missing `["Stone"]` |
| `LeaveOne_SkipsALastItemWhenOthersCanCover` | c1 holds one (untouchable), c2 covers 2 → `c1` draws 0 |
| `LeaveOne_CannotBeSatisfied_IsInfeasible` | lone item under `leaveOne` → infeasible, `Lines` empty |
| `ByDistance_OrdersLinesNearestFirst` | `near` (d=1) precedes `far` (d=10) |
| `TwoRequestsForSameItemName_ShareSources` | `a` draws exactly 2, `b` draws 2, total 4 = total requested |
| `UpgraderResource_IsSkippedWhenDisallowed` | 1 line (Coal only), `Flint` absent from lines and from missing |

Results:
- Focused: `--filter PullPlanTests` → **9/9 passed**.
- Full Tests project → **34/34 passed, 0 failed, 0 skipped**.
- Build: **0 Warning(s)**.

## TDD Evidence

**RED** — command:
`dotnet test "C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\Tests\CraftFromDrawersAndChests.Tests.csproj" --filter PullPlanTests`

Output (excerpt):
```
CraftFromDrawersAndChests.Tests net462 failed with 6 error(s) (0.2s)
  Tests\Logic\PullPlanTests.cs(10,24): error CS0246: The type or namespace name 'PullRequest' could not be found ...
  Tests\Logic\PullPlanTests.cs(15,24): error CS0246: The type or namespace name 'StorageStock' could not be found ...
  Tests\Logic\PullPlanTests.cs(20,24): error CS0246: The type or namespace name 'PullPlanResult' could not be found ...
Build failed with 6 error(s) in 1.2s
```
Expected failure: the tests reference types that do not exist yet, so the test assembly
cannot compile. This is the correct "failing test" for a brand-new type (compilation error),
and it names exactly the missing planner types.

**GREEN** — same focused command:
```
CraftFromDrawersAndChests.Tests test net462 succeeded (1.3s)
NUnit3TestExecutor discovered 9 of 9 NUnit test cases ...
Test summary: total: 9, failed: 0, succeeded: 9, skipped: 0, duration: 1.3s
```
Full project: `total: 34, failed: 0, succeeded: 34, skipped: 0`.

## Files changed

- `CraftFromDrawersAndChests/Logic/PullPlan.cs` (new)
- `Tests/Logic/PullPlanTests.cs` (new)
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` (+1 `<Compile>` line)

Commit: `4b3996e` — `feat: all-or-nothing pull planner` (3 files, +387). Working tree clean.

## Self-review findings

- Read my own diff: only the three intended files changed; `Logic/PullPlan.cs` uses only
  `System` and `System.Collections.Generic` — no UnityEngine/Valheim/BepInEx/Jotunn.
- `byDistance` uses a **stable** insertion sort, so equal distances keep the given order —
  deterministic, and it also leaves the request-walk order untouched when `byDistance` is
  false.
- `PullRequest.Prefab` is stored but never read by `Plan` (verified by grep of the
  implementation), matching Ruling 12.
- The `take <= 0` guard means a zero-amount or `leaveOne`-capped-to-zero stock is silently
  skipped rather than emitting a zero-quantity line.
- No extra tests were added beyond the nine named ones.

## Concerns

- **`MissingItemNames` may contain duplicates.** Resolution #4 says to collect "each one
  that cannot be filled, in request order", so two unfilled requests for the same item name
  append that name twice. I followed the literal wording (and request order) rather than
  de-duplicating to a true set. No brief test pins this either way; if Task 14's message
  wants unique names it can de-duplicate, or I can change `Plan` to de-duplicate while
  preserving first-seen order.
- `Amount` of zero or negative on a request is treated as already satisfied (no line, not
  missing). Not specified in the brief; noted for completeness.
