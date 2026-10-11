# Task 1 Report: Build and test harness

**Status:** DONE
**Commit:** `3a578c2` — build: scaffold product and Logic test projects
**Date:** 2026-10-11

## What I implemented

Created the repo skeleton every later task builds on, exactly as the brief specifies:

| File | Purpose |
|---|---|
| `CraftFromDrawersAndChests.sln` | Solution with the product project and the Tests project. |
| `Directory.Build.props` | `ValheimGameVersion=1.0.16`, `BepInExVersion=5.4.2350`, `ReferencesRoot=$(MSBuildThisFileDirectory).references` — verbatim from the brief. |
| `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` | Legacy csproj (explicit `<Compile>`, `packages.config`). References BepInEx core (`BepInEx`, `BepInEx.Harmony`, `0Harmony`) from `$(ReferencesRoot)\BepInEx\$(BepInExVersion)\BepInEx\core`; the four `*_publicized.dll` + `Managed\UnityEngine*.dll` + `Managed\Unity.TextMeshPro.dll` from `$(ReferencesRoot)\Valheim\$(ValheimGameVersion)`; Jotunn from `..\packages\JotunnLib.2.30.2\lib\net462\Jotunn.dll`; MultiUserChest from `$(ReferencesRoot)\Mods\MultiUserChest.dll`. `LangVersion=10`, `AllowUnsafeBlocks=true`, `TargetFrameworkVersion=v4.6.2`. |
| `CraftFromDrawersAndChests/packages.config` | `JotunnLib` 2.30.2 (`net462`). |
| `CraftFromDrawersAndChests/Logic/Geometry.cs` | Dependency-free `readonly struct Vec3` with `SqrDistanceTo` and `DistanceTo`. |
| `Tests/CraftFromDrawersAndChests.Tests.csproj` | SDK-style, `net462`, `PackageReference` to NUnit 3.14.0, NUnit3TestAdapter 4.5.0, Microsoft.NET.Test.Sdk 17.11.1, and `<Compile Include="..\CraftFromDrawersAndChests\Logic\**\*.cs" LinkBase="Logic" />`. |
| `Tests/Logic/GeometryTests.cs` | The brief's two tests, verbatim assertions. |

`Logic/Geometry.cs` references only `System` — no `UnityEngine`, `Valheim`, `BepInEx` or `Jotunn`.

## What I tested and results

- Full suite: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
- Result: `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2` (net462). Output is pristine — no warnings, no skipped tests.

## TDD Evidence

**RED (initial — project absent).** After writing only `Tests/Logic/GeometryTests.cs`:
```
$ dotnet test "…\Tests\CraftFromDrawersAndChests.Tests.csproj"
MSBUILD : error MSB1009: Project file does not exist.
Switch: Tests\CraftFromDrawersAndChests.Tests.csproj
```
Expected: the `Tests` project did not exist yet (brief Step 2).

**RED (test-level — `Vec3` absent).** After adding the Tests csproj but before implementing `Geometry.cs`:
```
$ dotnet test "…\Tests\CraftFromDrawersAndChests.Tests.csproj"
  Restored …CraftFromDrawersAndChests.Tests.csproj (in 4.01 sec).
…\Tests\Logic\GeometryTests.cs(1,33): error CS0234: The type or namespace name 'Logic'
  does not exist in the namespace 'CraftFromDrawersAndChests'
  (are you missing an assembly reference?)
```
Expected: the linked `Logic\**\*.cs` glob matched no files, so `CraftFromDrawersAndChests.Logic`
did not exist. This is the genuine failing-test RED.

**GREEN.** After creating `Directory.Build.props`, the product csproj/packages.config and
`Logic/Geometry.cs`:
```
$ dotnet test "…\Tests\CraftFromDrawersAndChests.Tests.csproj"
  CraftFromDrawersAndChests.Tests -> …\bin\Debug\net462\CraftFromDrawersAndChests.Tests.dll
  Test run for …\CraftFromDrawersAndChests.Tests.dll (.NETFramework,Version=v4.6.2)
  Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 79 ms
```

## Files changed

```
A  CraftFromDrawersAndChests.sln
A  CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj
A  CraftFromDrawersAndChests/Logic/Geometry.cs
A  CraftFromDrawersAndChests/packages.config
A  Directory.Build.props
A  Tests/CraftFromDrawersAndChests.Tests.csproj
A  Tests/Logic/GeometryTests.cs
```
7 files, 215 insertions. No build output staged (`bin/`, `obj/`, `packages/` are gitignored).

## Self-review findings

- **Completeness:** every file the brief lists is present; every reference it names is present.
- **Test assertions:** both tests assert concrete values (25f, and 5f within 0.0001f); no
  empty or placeholder test bodies anywhere.
- **Scope:** the product project is deliberately not expected to compile in this task
  (`.references` and `Mods\MultiUserChest.dll` do not exist yet). I did **not** run
  `tools/Build.ps1`, and did **not** point any path at the machine's installed mods.
- **Toolchain:** verified the sibling `XPortalNetworks` csproj (the source the `tools\`
  scaffold was lifted from) to confirm conventions before writing the product csproj.

## Decisions (where the brief left latitude)

1. **Jotunn `HintPath`:** the brief writes `packages\JotunnLib.2.30.2\lib\net462\Jotunn.dll`.
   A `<HintPath>` is relative to the project directory, so the correct value is
   `..\packages\JotunnLib.2.30.2\lib\net462\Jotunn.dll` (this is exactly what the sibling
   `XPortalNetworks.csproj` uses, and it resolves after `nuget restore -PackagesDirectory
   <repo>\packages`, which is what `tools/Build.ps1` does). I used the `..\`-prefixed form;
   the brief's literal substring is preserved.
2. **`Managed\UnityEngine*.dll`:** expressed as a single wildcard reference
   `<Reference Include="$(ReferencesRoot)\Valheim\$(ValheimGameVersion)\Managed\UnityEngine*.dll" />`
   rather than enumerating ~79 modules.
3. **Tests `LangVersion`:** set to `10` as well, so the linked `Logic\**` sources compile under
   the mandated language level.
4. **No extra NuGet plumbing:** I did not import `JotunnLib.props` or add the
   `EnsureNuGetPackageBuildImports` target — its `JotunnLibRefsCorlib.props` points at a
   `$(VALHEIM_MANAGED)/publicized_assemblies/...` layout that does not match this repo's
   `.references` layout, so importing it would add unresolved references. The brief does not
   ask for it.

## Concerns

- None blocking. The product project will not compile until a later task provides
  `.references` (via `tools/New-ValheimRefs.ps1`) and stages `Mods\MultiUserChest.dll`. That
  is the expected contract for this task.

---

# Task 1 Fix Report (post-review)

**Status:** DONE
**Commit:** `2dc0629` — fix(tools): drop foreign mod description and Docs/Nexus pipeline docs
**Date:** 2026-10-11

Closes the two Important review findings left open against Task 1. Both live in the inherited
`tools/` scaffold, which Task 1 did not author. Nothing outside `tools/` changed.

A previous attempt had already made both edits but never committed or reported. I inspected the
working tree, verified the edits against the task brief and the governing ruling, confirmed they
were complete, and committed them — I did not redo them.

## What I changed

**Finding 1 — foreign mod description in `tools/Build.ps1`.** The `<# ... #>` help block carried
another mod's feature sentence, "Portal networks live in the BepInEx config, not in a separate
file." Removed it, leaving the true sentence intact:

```
    The mod DLL is byte-identical on the server and the client, so a single build is
    deployable to both.
```

**Finding 2 — `Docs/` T4 pipeline + Nexus README in `tools/README.md`.** Per the governing
ruling (do not adopt the `Docs/` pipeline; no Nexus anything), replaced the section documenting
the `Docs/` T4 (`TextTemplatingFileGenerator`) pipeline and the generated
`Docs/README.Nexus.bbcode` table with a short, accurate note. The true `Build.ps1` and
`New-ValheimRefs.ps1` descriptions were kept untouched.

Removed:

```markdown
## Documentation (`Docs/`)

Most README files are **generated** by T4 templates ... (`TextTemplatingFileGenerator` entries
in `Docs/Docs.csproj`) ...
| `Docs/README.Nexus.bbcode` | `Docs/README.Nexus.tt` ... |
...
To regenerate, open the solution in Visual Studio and run the custom tool on the `.tt` files ...
```

Replaced with:

```markdown
## Documentation

There is no generated-documentation or template build step in this repository. All docs
are plain, hand-maintained Markdown: the repository `README.md`, this `tools/README.md`,
and the design and plan notes under `docs/superpowers/`.
```

## Commands run and output

```
$ git grep -ni -e xportal -e vapok
grep exit=1 (1 = no matches, expected)          # printed nothing

$ [System.Management.Automation.Language.Parser]::ParseFile("tools\Build.ps1", ...)
Build.ps1 parse errors: 0
$ [System.Management.Automation.Language.Parser]::ParseFile("tools\New-ValheimRefs.ps1", ...)
New-ValheimRefs.ps1 parse errors: 0
```

- `git grep -ni -e xportal -e vapok` → no matches (exit 1). The required constraint holds.
- Both PowerShell scripts parse with 0 errors.
- A full-`tools/` scan for `nexus` / `xportal` / `vapok` (any casing): none present.

## Files changed

```
M  tools/Build.ps1
M  tools/README.md
```

2 files changed, 5 insertions(+), 16 deletions(-).

## Concerns (fix round)

- `tools/Build.ps1` still contains the (vestigial) `Docs/SolutionDir/Package/Release/manifest.json`
  validation code, and the validation bullet in `tools/README.md` still names that path. That is
  runtime behaviour of `Build.ps1`, not the T4 *documentation* the ruling scoped out, and the
  check is already `Test-Path`-guarded so it skips silently when the path is absent. I left it
  as-is; flagging only in case the controller wants that dead path stripped in a follow-up.
