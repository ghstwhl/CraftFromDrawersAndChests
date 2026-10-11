### Task 4: All-or-nothing pull planner

**Files:**
- Create: `CraftFromDrawersAndChests/Logic/PullPlan.cs`
- Test: `Tests/Logic/PullPlanTests.cs`

**Interfaces:**
- Consumes: `StorageKind` (Task 3).
- Produces:
  - `readonly struct PullRequest { string Prefab; string ItemName; int Amount; bool IsUpgraderResource; }`
  - `readonly struct StorageStock { string SourceId; string ItemName; int Amount; float Distance; }`
  - `readonly struct PullLine { string SourceId; string ItemName; int Amount; }`
  - `sealed class PullPlanResult { bool Feasible; IReadOnlyList<PullLine> Lines; IReadOnlyList<string> MissingItemNames; }`
  - `static PullPlanResult PullPlan.Plan(IReadOnlyList<PullRequest> requests, IReadOnlyList<StorageStock> stocks, bool leaveOne, bool byDistance, bool allowUpgraderResources)`

`stocks` is one entry per *(source, item name)* pair, `Amount` being how many that source holds of that name. `SourceId` is opaque to `Logic` — the caller uses it to map a line back to an `IStorageSource`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test] public void RequirementExactlyMet_CreatesOneLine()
[Test] public void RequirementSpreadAcrossSources_SplitsIntoLines()
[Test] public void RequirementShortByOne_IsInfeasibleAndHasNoLines()   // Review Focus 1
[Test] public void InfeasiblePlan_ConsumesNothing()                     // no lines at all
[Test] public void LeaveOne_SkipsALastItemWhenOthersCanCover()
[Test] public void LeaveOne_CannotBeSatisfied_IsInfeasible()            // all-or-nothing beats leaveOne
[Test] public void ByDistance_OrdersLinesNearestFirst()
[Test] public void TwoRequestsForSameItemName_ShareSources()            // no double-counting a stock
[Test] public void UpgraderResource_IsSkippedWhenDisallowed()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter PullPlanTests`
Expected: FAIL.

- [ ] **Step 3: Implement `PullPlan.Plan`**

Sort `stocks` by `Distance` when `byDistance`. For each request in order, walk the matching stocks (`ItemName` equality) and draw until the request is met, decrementing a working copy so a stock is never drawn twice. When `leaveOne` is set, a stock may contribute at most `Amount - 1`; if that makes the plan infeasible, return a failing result. Return `Feasible = false` with an empty `Lines` and the unfilled item names in `MissingItemNames` as soon as any request cannot be met. The whole plan is computed before anything is returned — no partial result exists.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter PullPlanTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Logic/PullPlan.cs Tests/Logic/PullPlanTests.cs
git commit -m "feat: all-or-nothing pull planner"
```

---

