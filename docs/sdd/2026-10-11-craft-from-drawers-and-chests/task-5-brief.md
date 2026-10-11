### Task 5: Config model

**Files:**
- Create: `CraftFromDrawersAndChests/Logic/ConfigModel.cs`, `CraftFromDrawersAndChests/Logic/FeatureFlags.cs`
- Test: `Tests/Logic/ConfigModelTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `static class ConfigModel` with `const string SectionServer = "1 - Server"`, `SectionKeys = "2 - Keys"`, `SectionDebug = "9 - Debug"`; `const string` key names (`MaxRange = "Maximum range"`, `PullItemsKey = "Pull items key"`, `ToggleStorageKey = "Toggle storage key"`, `EnableChests`, `EnableDrawers`, `LeaveOne`, `PullByDistance`, `PullFuel`, `PullOre`, `PullCooking`, `FuelDisallowTypes`, `OreDisallowTypes`, `ResourceString`, `PulledMessage`, `FlashColor`, `UnFlashColor`, `IgnoreShipContainers`, `IgnoreWagonContainers`, `IgnoreWoodChests`, `IgnorePrivateChests`, `IgnoreBlackMetalChests`, `IgnoreReinforcedChests`, `ModEnabled`, `IsDebug`); `static string[] ParseList(string csv)`. `sealed class FeatureFlags` holding the resolved bool/float/string values.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test] public void ParseList_TrimsSplitsAndDropsEmpties()   // "a, b ,,c" -> ["a","b","c"]
[Test] public void ParseList_EmptyString_IsEmptyArray()
[Test] public void ParseList_Null_IsEmptyArray()
[Test] public void Defaults_MaxRangeIsFour()                 // 4f, matching CFC's usable default
[Test] public void Defaults_BothStorageKindsEnabled()
[Test] public void Defaults_LeaveOneOffAndPullByDistanceOff()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter ConfigModelTests`
Expected: FAIL.

- [ ] **Step 3: Implement `ConfigModel` and `FeatureFlags`**

`ParseList` returns `new string[0]` for null or empty input, otherwise splits on `','`, trims each part and drops empty entries. Key names are the constants above; do not invent others. `FeatureFlags` is a plain mutable class with one property per setting.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj --filter ConfigModelTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Logic/ConfigModel.cs CraftFromDrawersAndChests/Logic/FeatureFlags.cs Tests/Logic/ConfigModelTests.cs
git commit -m "feat: config key model and list parsing"
```

---

