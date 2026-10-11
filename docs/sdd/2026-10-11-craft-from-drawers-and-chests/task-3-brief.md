### Task 3: Storage eligibility predicate

**Files:**
- Create: `CraftFromDrawersAndChests/Logic/StorageKind.cs`, `CraftFromDrawersAndChests/Logic/Eligibility.cs`
- Test: `Tests/Logic/EligibilityTests.cs`

**Interfaces:**
- Consumes: `Vec3` (Task 1), `ChestTypeRules` (Task 2).
- Produces: `enum StorageKind { Chest, Drawer }`; `readonly struct StorageCandidate` with public readonly fields `Kind`, `Position` (`Vec3`), `ObjectName`, `ParentIsShip`, `ParentIsWagon`, `ToggledOff`, `HasAccess`, `HasInventory`; `static bool Eligibility.IsEligible(in StorageCandidate c, Vec3 origin, float range, bool chestsEnabled, bool drawersEnabled, ChestTypeRules chestRules)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test] public void ChestAtExactlyRange_IsEligible()      // distance == range counts as in range
[Test] public void ChestBeyondRange_IsNotEligible()
[Test] public void ChestToggledOff_IsNotEligible()
[Test] public void ChestWithoutAccess_IsNotEligible()
[Test] public void ChestWithNullInventory_IsNotEligible()
[Test] public void ChestOpenByAnotherPlayer_IsEligible()   // there is no in-use field at all
[Test] public void ChestsDisabled_DrawerStillEligible()
[Test] public void DrawerIgnoresChestTypeRulesAndAccess()  // name/access fields are irrelevant for a drawer
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter EligibilityTests`
Expected: FAIL.

- [ ] **Step 3: Implement `StorageKind` and `Eligibility.IsEligible`**

Drawer path: `drawersEnabled && !ToggledOff && SqrDistanceTo(origin) <= range*range`. Chest path: `chestsEnabled && !ToggledOff && HasAccess && HasInventory && !chestRules.IsExcluded(...) && SqrDistanceTo(origin) <= range*range`. Compare squared distances; do not call `Math.Sqrt` per candidate.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter EligibilityTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Logic Tests/Logic/EligibilityTests.cs
git commit -m "feat: storage eligibility predicate"
```

---

