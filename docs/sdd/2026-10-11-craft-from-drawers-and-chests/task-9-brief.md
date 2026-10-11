### Task 9: Drawer reflection API and drawer source

**Files:**
- Create: `CraftFromDrawersAndChests/Storage/DrawerApi.cs`, `CraftFromDrawersAndChests/Storage/IStorageSource.cs`, `CraftFromDrawersAndChests/Storage/DrawerSource.cs`

**Interfaces:**
- Consumes: `Vec3` (Task 1), `StorageKind` (Task 3).
- Produces:
  - `interface IStorageSource { StorageKind Kind { get; } Vec3 Position { get; } string ObjectName { get; } bool ToggledOff { get; } bool IsValid { get; } int Count(string itemName); int Take(string itemName, int amount, Inventory destination); }`
  - `static class DrawerApi` with `static bool IsAvailable`, `static IReadOnlyList<DrawerHandle> AllInRange(Vec3 origin, float range)`, `static bool IsDrawer(ZNetView view)`.
  - `readonly struct DrawerHandle { ZNetView View; string Prefab; int Amount; int Quality; }`
  - `sealed class DrawerSource : IStorageSource`

- [ ] **Step 1: Write the failing test**

```csharp
[Test] public void DrawerApi_WhenItemDrawersAbsent_IsAvailableIsFalseAndAllInRangeIsEmpty()
{
    Assert.That(DrawerApi.IsAvailable, Is.False);
    Assert.That(DrawerApi.AllInRange(new Vec3(0,0,0), 10f), Is.Empty);
}
```

This test runs in the `Tests` project, which has no Item Drawers assembly loaded — so it pins the soft-dependency fallback (Review Focus 5) without needing the game.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter DrawerApi`
Expected: FAIL — `DrawerApi` not defined.

- [ ] **Step 3: Implement the three files**

`DrawerApi` is `internal`-friendly but public for the test. Its static constructor resolves, in order, `Type.GetType("API.ItemDrawers_API, kg_ItemDrawers")` (use `AllDrawersInRange(Vector3, float)` if present) then `Type.GetType("API.ClientSideV2, kg_ItemDrawers")` (use `AllDrawers()`, filter by distance here). Cache the resolved `MethodInfo`s. If neither resolves, set `IsAvailable = false` and return an empty list — never throw. `DrawerSource.Count(itemName)` compares the drawer's ZDO `"Prefab"` to the item name's prefab and returns the ZDO `"Amount"`; a drawer with `Amount == 0` returns 0 (Review Focus 4). `DrawerSource.Take` calls `ClaimOwnership()` then `InvokeRPC("ForceRemove", amount)` and returns the amount requested. It never calls `WithdrawItem_Request`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
Expected: PASS, all tests including the earlier tasks.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Storage Tests
git commit -m "feat: item drawers reflection api and storage source"
```

---

