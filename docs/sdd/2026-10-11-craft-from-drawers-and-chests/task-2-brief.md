### Task 2: Chest type exclusions

**Files:**
- Create: `CraftFromDrawersAndChests/Logic/ChestTypeRules.cs`
- Test: `Tests/Logic/ChestTypeRulesTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `CraftFromDrawersAndChests.Logic.ChestTypeRules` — public bool fields `IgnoreShipContainers`, `IgnoreWagonContainers`, `IgnoreWoodChests`, `IgnorePrivateChests`, `IgnoreBlackMetalChests`, `IgnoreReinforcedChests`; `bool IsExcluded(string objectName, bool parentIsShip, bool parentIsWagon)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test] public void WoodChest_IsExcludedWhenRuleOn()
{
    var r = new ChestTypeRules { IgnoreWoodChests = true };
    Assert.That(r.IsExcluded("piece_chest_wood(Clone)", false, false), Is.True);
}

[Test] public void WoodChest_IsKeptWhenRuleOff()
{
    var r = new ChestTypeRules();
    Assert.That(r.IsExcluded("piece_chest_wood(Clone)", false, false), Is.False);
}

[Test] public void ReinforcedRule_DoesNotMatchWoodChest()
{
    var r = new ChestTypeRules { IgnoreReinforcedChests = true };
    Assert.That(r.IsExcluded("piece_chest_wood(Clone)", false, false), Is.False);
}

[Test] public void ShipContainer_IsExcludedWhenParentIsShip()
{
    var r = new ChestTypeRules { IgnoreShipContainers = true };
    Assert.That(r.IsExcluded("piece_chest(Clone)", true, false), Is.True);
}
```

Exact prefix rules, copied from CraftFromContainers 4.0.7: wood `piece_chest_wood(`, private `piece_chest_private(`, black metal `piece_chest_blackmetal(`, reinforced `piece_chest(`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter ChestTypeRulesTests`
Expected: FAIL — `ChestTypeRules` not defined.

- [ ] **Step 3: Implement `ChestTypeRules.IsExcluded`**

Returns true if any enabled rule matches: ship when `parentIsShip`, wagon when `parentIsWagon`, otherwise `objectName.StartsWith(prefix, StringComparison.Ordinal)`. Ordering does not matter because the prefixes are disjoint.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter ChestTypeRulesTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Logic/ChestTypeRules.cs Tests/Logic/ChestTypeRulesTests.cs
git commit -m "feat: chest type exclusion rules"
```

---

