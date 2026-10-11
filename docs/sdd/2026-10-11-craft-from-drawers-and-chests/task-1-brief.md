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

