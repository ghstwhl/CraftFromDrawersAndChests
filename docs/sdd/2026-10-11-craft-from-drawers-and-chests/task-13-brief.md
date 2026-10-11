### Task 13: Crafting and building pull through storage

**Files:**
- Create: `CraftFromDrawersAndChests/Pull/PullEngine.cs`, `CraftFromDrawersAndChests/Patches/CraftPatches.cs`

**Interfaces:**
- Consumes: `PullPlan` (Task 4), `StorageRegistry` (Task 10), `MultiUserChestBridge` (Task 11), `FeatureFlags`/`Config` (Tasks 5, 8).
- Produces: `static class PullEngine` with `static PullPlanResult PlanFor(Piece.Requirement[] requirements, int qualityLevel, int multiplier, Vec3 origin, FeatureFlags flags)` and `static bool Execute(PullPlanResult plan, IReadOnlyList<IStorageSource> sources, Player player)`.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — files missing.

- [ ] **Step 2: Implement `PullEngine` and `CraftPatches`**

`PlanFor` turns each `Piece.Requirement` into a `PullRequest` (`Amount = requirement.GetAmount(qualityLevel) * multiplier`, `ItemName = requirement.m_resItem.m_itemData.m_shared.m_name`, `IsUpgraderResource = requirement.m_upgraderResource`), collects `StorageStock`s from the sources, and calls `PullPlan.Plan`. `Execute` walks `plan.Lines` and calls `IStorageSource.Take` per line, aborting if any source returns fewer items than planned. `CraftPatches` postfixes the requirement-check methods so the player's "do I have the materials" answer includes storage, and prefixes the consume path so it pulls into the player inventory first and lets vanilla consume from there — CraftFromContainers' model, kept.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game**

Craft an item whose materials are split between a chest and a drawer, with the player carrying none.
Expected: it crafts; the materials come from both; the crafting panel shows the recipe as available before you craft. Then repeat with a requirement larger than everything in range.
Expected: nothing is consumed and the recipe is not marked available (Review Focus 1).

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Pull CraftFromDrawersAndChests/Patches
git commit -m "feat: craft and build from chests and drawers"
```

---

