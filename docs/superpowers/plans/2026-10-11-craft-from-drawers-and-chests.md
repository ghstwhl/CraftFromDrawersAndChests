# Craft From Drawers And Chests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Valheim mod that lets crafting, building and station-feeding draw materials from chests *and* KG Item Drawers drawers within one range of the player.

**Architecture:** Pure decision logic (range, eligibility, all-or-nothing pull planning, config parsing) lives in `CraftFromDrawersAndChests/Logic/**` with no Unity or Valheim types, so it is unit-testable outside the game. Everything that touches the game lives outside `Logic/` behind two interfaces — `IStorageSource` (one per storage kind) and `MultiUserChestBridge` — so a chest and a drawer are interchangeable to the pull engine. Harmony patches are thin: they translate game state into `Logic` calls and apply the returned plan.

**Tech Stack:** C# `net462`; BepInEx 5.4.2350; HarmonyX (`0Harmony`); Jötunn 2.30.2; MSBuild with legacy `.csproj` + `packages.config` (inherited from the `tools/` scaffold); NUnit + `dotnet test` for the `Logic` tests.

**Spec:** `docs/superpowers/specs/2026-10-11-craft-from-drawers-and-chests-design.md`

## Global Constraints

- **Target framework** `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; HarmonyX via BepInEx's `0Harmony.dll`.
- **Enforcement:** the plugin class carries `[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]` from `Jotunn.Utils` and `[BepInDependency(Jotunn.Main.ModGuid)]`.
- **Config is server-authoritative**, registered with `Jotunn.Managers.SynchronizationManager.Instance.RegisterCustomConfig(config)` so clients receive the server's values and non-admins cannot change them.
- **One range value.** Origin is the player for crafting, building and the ctrl-click pull; the station for the fuel / ore / cooking features.
- **Never write `Container.m_inventory` and never write a drawer's ZDO `"Amount"`.** Chests are mutated only through `MultiUserChest.ContainerHandler.RemoveItemFromChest`; drawers only through the `ForceRemove` and `AddItem_Request` RPCs.
- **Never call the drawer RPC `WithdrawItem_Request`.**
- **Pulls are all-or-nothing:** if the full requirement cannot be secured, nothing is consumed.
- **The storage switch is a shared world property** stored as a boolean on the object's ZDO, flipped by a hotkey while hovering; it does not affect Item Drawers' own automation.
- **Drawer reflection contract:** prefer `API.ItemDrawers_API` in assembly `kg_ItemDrawers`; fall back to `API.ClientSideV2.AllDrawers()`. ZDO keys are `"Prefab"` (string), `"Amount"` (int), `"Quality"` (int). If neither type resolves, the mod runs chest-only without throwing.
- **Declare `aedenthorn.CraftFromContainers` and Azumatt's AzuCraftyBoxes as `[BepInIncompatibility]`.**
- **Thunderstore identity:** team `NorCal_Nerds`, package `CraftFromDrawersAndChests`.
- **Mod identity:** `ModName = "CraftFromDrawersAndChests"`, `ModGuid = "NorCal_Nerds.CraftFromDrawersAndChests"`, starting `Version = "0.1.0"`.
- **`Logic/**` must not reference `UnityEngine`, `Valheim`, `BepInEx` or `Jotunn`** — that is what makes the tests runnable.
- **No Nexus publishing.** There is no `nexusID` config entry and no Nexus-flavoured README. Packaging is Thunderstore format: team `NorCal_Nerds`, package `CraftFromDrawersAndChests`. Hexium is a listing, not a second package format.
- **Odin's QoL compatibility is out of scope** — build no stack-size or weight-modifier shims, and read no Odin's QoL config.

## Review Focus

The spec implies these conditions, and no task's own tests prove them cleanly on their own. Each is pinned to a test or an in-game check in the task that owns the code.

1. **A requirement larger than everything within range.** Nothing may be consumed, and the recipe must not be offered as craftable. (Task 4's infeasible-plan test; Task 13's in-game check)
2. **A player inventory with no free stack space.** Nothing may be pulled, and nothing may land on the ground. (Task 4's infeasible-plan test; Task 14's in-game check)
3. **Two players acting on the same chest at once.** The item must leave storage exactly once, whichever way the request is routed. (Task 11's in-game check)
4. **A drawer whose ZDO reports a prefab but a zero amount.** It contributes nothing and throws nothing. (Task 9's test)
5. **Item Drawers absent, or its API type renamed again.** The mod runs chest-only, throws nothing, and does not silently half-work. (Task 9's test)

---

## File Structure

```
CraftFromDrawersAndChests.sln
Directory.Build.props                          ValheimGameVersion, ReferencesRoot, BepInExVersion
CraftFromDrawersAndChests/
  CraftFromDrawersAndChests.csproj             legacy csproj + packages.config
  packages.config                              JotunnLib
  ModInfo.cs                                   name, GUID, version, Thunderstore identity
  Plugin.cs                                    BepInEx entry point, Jotunn deps + enforcement
  Config.cs                                    ConfigEntry creation + Jotunn sync
  Logic/Geometry.cs                            Vec3                    (pure)
  Logic/StorageKind.cs                         enum                    (pure)
  Logic/ChestTypeRules.cs                      chest-type exclusions  (pure)
  Logic/Eligibility.cs                        StorageCandidate + predicate (pure)
  Logic/PullPlan.cs                            all-or-nothing planner (pure)
  Logic/ConfigModel.cs                         config key names + list parsing (pure)
  Logic/FeatureFlags.cs                        resolved config snapshot (pure)
  Storage/IStorageSource.cs                    one storage, one interface
  Storage/VanillaChestSource.cs                wraps Valheim Container
  Storage/DrawerApi.cs                         reflection into kg_ItemDrawers
  Storage/DrawerSource.cs                      wraps a drawer ZNetView
  Storage/StorageRegistry.cs                   discovery + registry
  MultiUserChestBridge.cs                      chest removal path
  Toggle/StorageToggle.cs                      ZDO flag read/flip
  Toggle/TogglePatches.cs                      hotkey + hover text
  Pull/PullEngine.cs                           plan -> execute
  Patches/CraftPatches.cs                      crafting + building requirement/consume
  Patches/PullKeyPatches.cs                    ctrl-click pull, messages, flash
  Patches/StationPatches.cs                    fireplace, smelter, cooking station, turret
  Patches/TerminalPatch.cs                     console command
Tests/
  CraftFromDrawersAndChests.Tests.csproj       SDK-style, net462, links Logic/**
  Logic/GeometryTests.cs
  Logic/ChestTypeRulesTests.cs
  Logic/EligibilityTests.cs
  Logic/PullPlanTests.cs
  Logic/ConfigModelTests.cs
```

`Tests` links `..\CraftFromDrawersAndChests\Logic\**\*.cs` directly rather than referencing the product assembly, because the product assembly needs the game DLLs loaded at runtime. This is why `Logic/**` must stay dependency-free.

**Scope check:** one subsystem, one plan. Tasks 1–13 produce a working chest-and-drawer crafting mod; tasks 14–15 add the station and convenience features.

---

### Task 1: Build and test harness

**Files:**
- Create: `CraftFromDrawersAndChests.sln`
- Create: `Directory.Build.props`
- Create: `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj`, `CraftFromDrawersAndChests/packages.config`
- Create: `CraftFromDrawersAndChests/Logic/Geometry.cs`
- Create: `Tests/CraftFromDrawersAndChests.Tests.csproj`, `Tests/Logic/GeometryTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `CraftFromDrawersAndChests.Logic.Vec3` — `Vec3(float x, float y, float z)`, `float SqrDistanceTo(Vec3 other)`, `float DistanceTo(Vec3 other)`.

- [ ] **Step 1: Write the failing test**

```csharp
[TestFixture]
public class GeometryTests
{
    [Test]
    public void SqrDistanceTo_IsThreeFourFiveSquared()
    {
        Assert.That(new Vec3(0, 0, 0).SqrDistanceTo(new Vec3(3, 4, 0)), Is.EqualTo(25f));
    }

    [Test]
    public void DistanceTo_IsFive()
    {
        Assert.That(new Vec3(0, 0, 0).DistanceTo(new Vec3(3, 4, 0)), Is.EqualTo(5f).Within(0.0001f));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
Expected: FAIL — the `Tests` project does not exist.

- [ ] **Step 3: Create the projects, then implement `Vec3` in `CraftFromDrawersAndChests/Logic/Geometry.cs`**

`Directory.Build.props` at the repo root, carrying the same three values `New-ValheimRefs.ps1` and the csproj read:

```xml
<Project>
  <PropertyGroup>
    <ValheimGameVersion>1.0.16</ValheimGameVersion>
    <BepInExVersion>5.4.2350</BepInExVersion>
    <ReferencesRoot>$(MSBuildThisFileDirectory).references</ReferencesRoot>
  </PropertyGroup>
</Project>
```

`Tests/CraftFromDrawersAndChests.Tests.csproj` is SDK-style, `net462`, `PackageReference` to `NUnit` 3.14.0, `NUnit3TestAdapter` 4.5.0 and `Microsoft.NET.Test.Sdk` 17.11.1, plus:

```xml
<ItemGroup>
  <Compile Include="..\CraftFromDrawersAndChests\Logic\**\*.cs" LinkBase="Logic" />
</ItemGroup>
```

The product `.csproj` stays legacy (explicit `<Compile>` items, `packages.config`) because `tools/Build.ps1` drives it with `nuget restore` and `-PackagesDirectory`. Reference `BepInEx.dll`, `0Harmony.dll`, `BepInEx.Harmony.dll` from `$(ReferencesRoot)\BepInEx\$(BepInExVersion)\BepInEx\core`, the four `*_publicized.dll` plus `Managed\UnityEngine*.dll` and `Managed\Unity.TextMeshPro.dll` from `$(ReferencesRoot)\Valheim\$(ValheimGameVersion)`, `packages\JotunnLib.2.30.2\lib\net462\Jotunn.dll`, and `$(ReferencesRoot)\Mods\MultiUserChest.dll`. Set `<LangVersion>10</LangVersion>` and `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.

`Vec3` is an immutable struct holding three floats, `SqrDistanceTo` returning the sum of squared component differences, and `DistanceTo` returning `(float)Math.Sqrt(SqrDistanceTo(...))`.

`packages.config` lists `JotunnLib` 2.30.2.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
Expected: PASS, 2 tests.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests.sln Directory.Build.props CraftFromDrawersAndChests Tests
git commit -m "build: scaffold product and Logic test projects"
```

---

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

### Task 6: Mod identity and release packaging

**Files:**
- Create: `CraftFromDrawersAndChests/ModInfo.cs`, `manifest.json`
- Create: `README.md`, `LICENSE.md`, `CHANGELOG.md`, `icon.png` at the repo root — `Build.ps1` stages these into the package
- Modify: `tools/Build.ps1:70-84` if the identity constants do not already line up

**Interfaces:**
- Consumes: nothing.
- Produces: `static class ModInfo` with `public const string ModName`, `ModGuid`, `Version`, `ThunderstoreTeam`, `ThunderstorePackage`, `GitHubRepo` — `Build.ps1` reads `Version`, `ThunderstoreTeam` and `ThunderstorePackage` out of this file by regex, so keep the declarations in the exact form `public const string <Name> = "<value>";`.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug -SkipRestore`
Expected: FAIL — `ModInfo.cs not found at '...\CraftFromDrawersAndChests\ModInfo.cs'`.

- [ ] **Step 2: Implement `ModInfo.cs` and `manifest.json`**

Values: `ModName = "CraftFromDrawersAndChests"`, `ModGuid = "NorCal_Nerds.CraftFromDrawersAndChests"`, `Version = "0.1.0"`, `ThunderstoreTeam = "NorCal_Nerds"`, `ThunderstorePackage = "CraftFromDrawersAndChests"`. `manifest.json` at the repo root carries the same `version_number` as `Version` — `Build.ps1` fails the build when they differ.

- [ ] **Step 3: Add the repo-root files the package stages**

`tools/Build.ps1`'s `New-DocStage` copies `LICENSE.md`, `manifest.json`, `CHANGELOG.md`, `README.md` and `icon.png` from the repo root into the package, skipping any that are missing — so a missing file silently produces an incomplete zip. Ensure `README.md` exists at the repo root (one may already be present), and add `LICENSE.md`, `CHANGELOG.md` with a `0.1.0` entry, and a 256x256 `icon.png`. The README covers what the mod does, that every player and the server need it, and that CraftFromContainers and AzuCraftyBoxes must be removed before installing. No Nexus-flavoured README.

- [ ] **Step 4: Run the build to verify it gets past identity validation**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: progresses past the `manifest.json = 0.1.0` line, then fails at MSBuild because `Plugin.cs` does not exist yet. That is the intended stopping point for this task; full green arrives in Task 7.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/ModInfo.cs manifest.json README.md LICENSE.md CHANGELOG.md icon.png
git commit -m "feat: mod identity, manifest and packaged repo files"
```

---

### Task 7: Plugin bootstrap

**Files:**
- Create: `CraftFromDrawersAndChests/Plugin.cs`

**Interfaces:**
- Consumes: `ModInfo` (Task 6).
- Produces: `class Plugin : BaseUnityPlugin` with `public static Plugin Instance`, `public static Harmony Harmony`, and `internal static ManualLogSource Log`. Later tasks patch from `Plugin.Harmony` and log through `Plugin.Log`.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — `Plugin.cs` missing, MSBuild error `CS2001`/`CS0246`.

- [ ] **Step 2: Implement `Plugin.cs`**

Attributes, exactly:

```csharp
[BepInPlugin(ModInfo.ModGuid, ModInfo.ModName, ModInfo.Version)]
[BepInDependency(Jotunn.Main.ModGuid)]
[BepInDependency("com.maxsch.valheim.MultiUserChest")]
[BepInDependency("kg.ItemDrawers", BepInDependency.DependencyFlags.SoftDependency)]
[BepInIncompatibility("aedenthorn.CraftFromContainers")]
[BepInIncompatibility("Azumatt.AzuCraftyBoxes")]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
public class Plugin : BaseUnityPlugin
```

`Awake` sets `Instance`, assigns `Harmony = new Harmony(ModInfo.ModGuid)`, calls `Harmony.PatchAll()`. MultiUserChest is a **hard** dependency: with it absent the only working chest implementation is the direct mutation this design forbids.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`.

- [ ] **Step 4: Verify in game**

Deploy the DLL to `BepInEx\plugins\` on a client and to the server, launch, and confirm the BepInEx log shows the plugin loading with no Harmony patch errors. Remove MultiUserChest from one client and confirm the connection is refused — that is the enforcement working.
Expected: loads cleanly; the mismatch case refuses the connection.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Plugin.cs
git commit -m "feat: plugin bootstrap with Jotunn enforcement"
```

---

### Task 8: Server-authoritative config

**Files:**
- Create: `CraftFromDrawersAndChests/Config.cs`
- Modify: `CraftFromDrawersAndChests/Plugin.cs` — call `Config.Init(Config)` from `Awake`

**Interfaces:**
- Consumes: `ConfigModel` (Task 5), `Plugin` (Task 7).
- Produces: `static class Config` with `public static ConfigFile File`, `public static FeatureFlags Flags`, `public static void Init(ConfigFile config)`, and `public static void ReadInto(FeatureFlags flags)` which copies every `ConfigEntry` value into the flags object.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — `Config.cs` missing.

- [ ] **Step 2: Implement `Config.cs`**

Bind one `ConfigEntry` per constant in `ConfigModel` using the section and key names from `ConfigModel` (never string literals written here), then call `SynchronizationManager.Instance.RegisterCustomConfig(config)`. Types: `maxRange` float; the two key settings and the two disallow-type lists strings; flash colours `ConfigEntry<Color>`; everything else bool. `ReadInto` assigns each entry into the matching `FeatureFlags` property.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game**

On the server, edit `maxRange` through the ConfigurationManager window and restart. Join from a client with a *different* local value in its own config file.
Expected: the client behaves with the server's value; the entry is read-only for the non-admin client.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Config.cs CraftFromDrawersAndChests/Plugin.cs
git commit -m "feat: server-synced configuration"
```

---

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

### Task 10: Chest registry and vanilla source

**Files:**
- Create: `CraftFromDrawersAndChests/Storage/VanillaChestSource.cs`, `CraftFromDrawersAndChests/Storage/StorageRegistry.cs`, `CraftFromDrawersAndChests/Storage/ContainerTrackingPatches.cs`

**Interfaces:**
- Consumes: `IStorageSource` (Task 9), `Eligibility` (Task 3).
- Produces: `static class StorageRegistry` with `static IReadOnlyList<IStorageSource> Collect(Vec3 origin, FeatureFlags flags)` and `static void Clear()`. `ContainerTrackingPatches` maintains the tracked `Container` set.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — files missing.

- [ ] **Step 2: Implement the three files**

`ContainerTrackingPatches` Harmony-postfixes `Container.Awake` to add the instance to a static list and `Container.OnDestroyed` to remove it. `VanillaChestSource.Count(itemName)` sums `GetInventory().CountItems(itemName)`. `VanillaChestSource.Take` is implemented in Task 11 — for now it throws `NotImplementedException`. `StorageRegistry.Collect` builds a `StorageCandidate` per tracked chest and per `DrawerApi.AllInRange` handle, runs `Eligibility.IsEligible`, and returns the surviving sources cast to `IStorageSource`.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game**

Place two chests, one inside and one outside the configured range, and a drawer inside it. Add temporary debug logging in `Collect` printing each source's name, kind and distance.
Expected: the far chest is absent from the list; the near chest and the drawer are present; no exceptions with zero chests in the world.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Storage
git commit -m "feat: chest registry and storage collection"
```

---

### Task 11: MultiUserChest removal bridge

**Files:**
- Create: `CraftFromDrawersAndChests/MultiUserChestBridge.cs`
- Modify: `CraftFromDrawersAndChests/Storage/VanillaChestSource.cs` — implement `Take`

**Interfaces:**
- Consumes: `ContainerHandler` from `MultiUserChest.dll`.
- Produces: `static class MultiUserChestBridge` with `static int RemoveFromChest(Container chest, ItemDrop.ItemData item, Inventory destination, int amount)` returning how many were actually removed.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — `MultiUserChestBridge.cs` missing.

- [ ] **Step 2: Implement the bridge**

If `chest.m_nview.HasOwner()` is false, call `chest.m_nview.ClaimOwnership()` first (a closed chest has nobody to route to). Then call `ContainerHandler.RemoveItemFromChest(chest, item, destination, to, sender, amount)`, convert the returned `RequestChestRemove` to its response the way `InventoryHandler` does, and return the number of items that ended up removed. Never touch `chest.GetInventory()` directly. `VanillaChestSource.Take` locates the `ItemDrop.ItemData` for `itemName` in the chest, calls the bridge, and returns its result.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game with a second player**

Have a second player open the chest and leave it open. Trigger a manual debug call that removes 1 item from it.
Expected: the item moves exactly once (Review Focus 3); both clients still show consistent chest contents; the second player sees the change.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/MultiUserChestBridge.cs CraftFromDrawersAndChests/Storage/VanillaChestSource.cs
git commit -m "feat: route chest removal through MultiUserChest"
```

---

### Task 12: Per-storage switch

**Files:**
- Create: `CraftFromDrawersAndChests/Toggle/StorageToggle.cs`, `CraftFromDrawersAndChests/Toggle/TogglePatches.cs`

**Interfaces:**
- Consumes: `IStorageSource` (Task 9), `ConfigModel` (Task 5).
- Produces: `static class StorageToggle` with `const string ZdoKey = "CFDAC_NoPull"`, `static int ZdoHash`, `static bool IsOff(ZNetView view)`, `static void Flip(ZNetView view)`, `static string HoverSuffix(bool off)`.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — files missing.

- [ ] **Step 2: Implement the toggle**

`ZdoHash = ZdoKey.GetStableHashCode()`. `IsOff` reads `view.GetZDO().GetBool(ZdoHash, false)`. `Flip` calls `ClaimOwnership()` then writes the negated value, matching AzuCraftyBoxes' `NoCrafty` pattern. `HoverSuffix` returns a coloured `" (pull: off)"` / `" (pull: on)"` string. `TogglePatches` adds a Harmony postfix on `Container.GetHoverText` and on `Player.Update` (the hover path for drawers, via `FindApiContainerView`) to append the suffix, and a prefix that consumes the `ToggleStorageKey` press while hovering and calls `Flip`. The drawer hover path must use `DrawerApi.IsDrawer` rather than assuming a `Container`.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game with a second player**

Hover a chest, press the toggle key, and confirm the hover text flips. Have a second player look at the same chest.
Expected: the second player sees it off too (the state is shared), and it survives a server restart (the ZDO persisted).

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Toggle
git commit -m "feat: per-storage pull switch on the ZDO"
```

---

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

### Task 14: Ctrl-click pull, messages and flash

**Files:**
- Create: `CraftFromDrawersAndChests/Patches/PullKeyPatches.cs`
- Modify: `CraftFromDrawersAndChests/Pull/PullEngine.cs` — add `static bool PullToPlayer(Piece.Requirement[] requirements, int qualityLevel, int multiplier, Player player, FeatureFlags flags)`

**Interfaces:**
- Consumes: `PullEngine` (Task 13).
- Produces: `PullEngine.PullToPlayer(...)` returning whether the pull succeeded.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — `PullKeyPatches.cs` missing.

- [ ] **Step 2: Implement `PullToPlayer` and the patches**

`PullToPlayer` builds the plan against the player's own inventory as the destination and refuses (returning false, consuming nothing) when the inventory has insufficient free stack space for the full requirement — items must never land on the ground (Review Focus 2). `PullKeyPatches` prefixes `InventoryGui` so that holding `PullItemsKey` while clicking a recipe calls `PullToPlayer` instead of crafting, shows `PulledMessage` on success, shows a failure message naming the missing items, and flashes the contributing sources using `FlashColor` / `UnFlashColor`.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game**

Ctrl-click a recipe with materials in a chest and a drawer.
Expected: the recipe is *not* built, the ingredients land in your inventory, the contributing chest and drawer flash, and the pulled message shows. Then fill your inventory completely and repeat.
Expected: nothing is pulled, nothing is on the ground, and a message says why.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Patches/PullKeyPatches.cs CraftFromDrawersAndChests/Pull/PullEngine.cs
git commit -m "feat: ctrl-click ingredient pull with feedback"
```

---

### Task 15: Station pulls and console command

**Files:**
- Create: `CraftFromDrawersAndChests/Patches/StationPatches.cs`, `CraftFromDrawersAndChests/Patches/TerminalPatch.cs`

**Interfaces:**
- Consumes: `PullEngine` (Task 13), `ConfigModel` (Task 5).
- Produces: no new public API; patches only.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — files missing.

- [ ] **Step 2: Implement the patches**

Port CraftFromContainers' behaviour for these, each gated on its `FeatureFlags` setting and each measuring range from **the station's transform**, not the player: `Fireplace` fuel (skip item names listed in `FuelDisallowTypes`), `Smelter` ore (skip names in `OreDisallowTypes`), `CookingStation` food, and `Turret` ammunition. `TerminalPatch` registers a console command that reports how many sources are in range and whether the drawer API resolved.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game**

With materials only in storage, feed a smelter, light a fire, cook food and load a turret.
Expected: each works; an item name in `FuelDisallowTypes` is never taken; the console command reports the source count and the drawer API state.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Patches
git commit -m "feat: station pulls and console command"
```

---

## Self-Review

**1. Spec coverage**

| Spec item | Task |
|---|---|
| Storage abstraction, chests + drawers | 9, 10 |
| Distance is the only spatial rule (except station parity) | 3, 13, 15 |
| Per-storage switch, shared, hotkey + hover | 12 |
| Server-authoritative config + ConfigurationManager | 8 |
| Mod-set enforcement via Jötunn | 7 |
| No hand-rolled inventory mutation | 11, 9 |
| All-or-nothing pulls | 4, 13, 14 |
| Drawer reflection contract, soft dependency | 9 |
| `pullItemsKey` ctrl-click pull | 14 |
| `pullByDistance`, `leaveOne` | 4 |
| `resourceString`, `pulledMessage`, flash colours | 14 |
| Ignore-this-chest-type settings | 2, 3 |
| `pullFuel`, `pullOre`, `pullCooking`, `fuelDisallowTypes`, `oreDisallowTypes`, turret | 15 |
| Terminal command | 15 |
| `isDebug`, `modEnabled` | 5, 8 |
| Requirement checks count storage | 13 |
| Crafting-panel refresh while the key is held | 14 |
| Toolchain re-pointed at this mod | 6 |
| Odin's QoL compatibility | **dropped** — Odin's QoL is not in the mod set |
| `nexusID` | **dropped** — publishing is Thunderstore and possibly Hexium, never Nexus |

Two spec items are **dropped rather than planned**, both settled by the author: Odin's QoL is not in the mod set, and publishing is Thunderstore (and possibly Hexium), never Nexus. Nothing here is waiting on an answer.

**2. Step scan** — every step names a command or a file and one checkable outcome. No step says "handle edge cases" or "write tests for the above".

**3. Type consistency** — `Vec3`, `StorageKind`, `StorageCandidate`, `ChestTypeRules`, `PullRequest`/`StorageStock`/`PullLine`/`PullPlanResult`, `IStorageSource`, `StorageRegistry.Collect`, `PullEngine.PlanFor`/`Execute`/`PullToPlayer`, `MultiUserChestBridge.RemoveFromChest`, `StorageToggle.IsOff`/`Flip` are each defined once and used under the same names in later tasks.

**4. Review Focus** — the five entries in the header each have a named test or in-game check in the task that owns the code (Tasks 4, 9, 11, 14).

**5. Proportion** — the plan is longer than the spec because it pins names, signatures and config keys the spec deliberately left to the builder; code bodies appear only where the algorithm is not determined by the signature and tests (`PullPlan.Plan`, `DrawerApi` resolution order).
