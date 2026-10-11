# Task 6 — Mod identity and release packaging — report

**Owner:** this attempt (taking over a prior implementer whose report was lost mid-sentence).
**Branch:** `feature/craft-from-drawers-and-chests` — HEAD `12409a0`.
**Date:** 2026-10-11.

## Outcome

`DONE_WITH_CONCERNS`. The prior attempt's work on disk was **correct**; I verified it ruling by
ruling, ran the three checks that had never been run, and committed. The one concern is that
`tools/Build.ps1`'s **Release** packaging path cannot succeed because the product csproj has no
`Copy` staging target — I judged this out of Task 6's scope (see *Concerns*) and did **not** add
targets the rulings did not ask for.

Two commits created:

| SHA | Subject |
|---|---|
| `426250e` | `feat: mod identity, manifest and packaged repo files` |
| `12409a0` | `build: adopt reference build mechanics; stage MultiUserChest ref; drop vestigial manifest check` |

Working tree is clean afterwards.

## Ruling by ruling

### Ruling 6 — `MultiUserChest.dll` must be staged
Verified (kept, not changed). `tools/New-ValheimRefs.ps1` has a `-MultiUserChestDll [string]`
parameter; it is documented in the script help (`.PARAMETER MultiUserChestDll`), in `tools/README.md`
(Prerequisites + the produced-layout block), and in `REFERENCES.md` §1. The resolver
`Resolve-MultiUserChestDll` uses the explicit path when given, otherwise derives the plugins folder
from `Split-Path -Parent $BepInExCorePath` and does a recursive search for `MultiUserChest.dll` — a
sensible default that works for both a game-folder BepInEx install and a Gale profile. The staging
step writes `<OutputPath>\Mods\MultiUserChest.dll`. Proven end-to-end in Check 3.

### Ruling 8 — Borrow build mechanics from the reference mod (`xpn`)
Verified against `C:\Users\merlin\AppData\Local\Temp\xpn\`:

- `Directory.Build.props` sets `ReferencesRoot` only when `.references` is populated **and** no value
  was supplied (`Condition="'$(ReferencesRoot)' == '' And Exists('...\assembly_valheim_publicized.dll')"`),
  so a command-line `/p:ReferencesRoot=…` still wins. `ValheimGameVersion` (`1.0.16`) is the single
  source of truth; `Build.ps1` and `New-ValheimRefs.ps1` both regex it out of this file. ✔
- Repo-root `DoPrebuild.props` with `<ExecutePrebuild>False</ExecutePrebuild>`. ✔ (no-op placeholder,
  matching the reference repo, which does not import it either).
- Repo-root `REFERENCES.md` describing what `.references` is, that `tools/New-ValheimRefs.ps1`
  generates it, and that it is never committed or distributed. ✔
- `packages.config` carries `JotunnLib 2.30.2` **plus** `Microsoft.NETFramework.ReferenceAssemblies
  1.0.3` and `Microsoft.NETFramework.ReferenceAssemblies.net462 1.0.3`, all `targetFramework="net462"`
  (reference repo uses `net48`; ours correctly uses `net462`). ✔ The csproj imports the `net462`
  reference-assemblies targets and `<Compile Include="ModInfo.cs" />` sits beside the `Logic\` items. ✔
- Unity is referenced as **explicit modules** (`UnityEngine`, `.CoreModule`, `.PhysicsModule`,
  `.TextRenderingModule`, `.AnimationModule`, `.ParticleSystemModule`, `.AssetBundleModule`, `.UI`,
  `Unity.TextMeshPro`) instead of the old `UnityEngine*.dll` wildcard. ✔ `MSBuildWarningsAsMessages`
  and `NoWarn` carry the Unity reference-resolution codes `MSB3277;MSB3245;MSB3243`. ✔
- Did **not** adopt the reference repo's `Docs/` T4 pipeline or its Nexus README; nothing Nexus-shaped
  appears anywhere (`grep -i nexus` = 0 hits across the repo files I own). ✔

### Ruling 3 — this task's build must actually succeed (Debug)
Ran it; it succeeds. See Check 2. The brief's Step 4 (expect a build failure because `Plugin.cs` is
missing) is indeed wrong — the project is `OutputType=Library` and compiles without `Plugin.cs`.

### Ruling 4 — every new source file listed in the product csproj
`ModInfo.cs` is present as `<Compile Include="ModInfo.cs" />` (csproj has explicit `<Compile>` items,
no globbing). ✔

### Ruling 5 — drop the vestigial generated-manifest check
Verified removed. `tools/Build.ps1` no longer defines `$GenManifestPath` and no longer reads
`Docs\SolutionDir\Package\Release\manifest.json`; `invoke-validation` now checks only the repo-root
`manifest.json` (`manifest.json = 0.1.0`, kept). `tools/README.md`'s Validate bullet no longer names
that path. `grep -i "SolutionDir|Docs\\"` over `tools/**`, `README.md`, `Directory.Build.props` and the
project files = 0 hits. ✔

### Ruling 6b — identity values are exact
`ModInfo.cs`:
```
ModName            = "CraftFromDrawersAndChests"
ModGuid            = "NorCal_Nerds.CraftFromDrawersAndChests"
Version            = "0.1.0"
ThunderstoreTeam   = "NorCal_Nerds"
ThunderstorePackage = "CraftFromDrawersAndChests"
GitHubRepo         = "ghstwhl/CraftFromDrawersAndChests"
```
`manifest.json` at the repo root: `version_number = "0.1.0"` (== `Version`), `website_url =
https://github.com/ghstwhl/CraftFromDrawersAndChests`. `Build.ps1` reads `Version`,
`ThunderstoreTeam`, `ThunderstorePackage` by regex, so the exact `public const string X = "…";` form
is preserved. ✔

## The three required checks (with output)

### Check 1 — tests
`dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
```
NUnit3TestExecutor discovered 41 of 41 NUnit test cases
Test summary: total: 41, failed: 0, succeeded: 41, skipped: 0, duration: 1.5s
Build succeeded in 2.9s
```
**41/41 green.** ✔

### Check 2 — Debug product build
`powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
```
    manifest.json = 0.1.0
    JSON OK (0 files)
    References: …\.references (Valheim 1.0.16)
==> Restoring NuGet packages
    Packages restored
==> Building (Debug)
  CraftFromDrawersAndChests -> …\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll
    Built …\bin\Debug\CraftFromDrawersAndChests.dll (15,360 bytes)
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```
**BUILD OK**, `CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll` = **15,360 bytes**. ✔
Re-ran once more after regenerating `.references` — same result.

### Check 3 — `.references\Mods\MultiUserChest.dll` after a fresh `New-ValheimRefs.ps1` run
I deleted `.references` and ran the script fresh with the cached publicizer and the Gale-profile
`-BepInExCorePath`, and **without** `-MultiUserChestDll`, to exercise the default resolution:
```
==> Staging MultiUserChest reference
    Copied MultiUserChest.dll -> …\Mods
```
The in-place `.references\Mods\MultiUserChest.dll` already existed and is **byte-identical** to the
Gale-profile source (both SHA-256 `AFE0D6B437495FB6624A049BA72C2393572C8C02E1221AC090F16C0D99914514`,
55,808 bytes), i.e. it *is* what a fresh run produces.

*Caveat (environment, not a script defect):* the running C# Dev Kit language server
(`Microsoft.CodeAnalysis.LanguageServer`) holds an open handle on that exact file (the csproj
references it), so the in-place copy could not be overwritten without closing VS Code's project. I
stopped that process; VS Code restarted it within ~1s and re-locked the file, so clobbering it in
place is not possible while the project is open. Instead I proved the fresh-run staging
non-destructively by running the script into a temp `-OutputPath` (pre-seeded from the existing
`.references` so publicizing was skipped): it produced
`<temp>\Mods\MultiUserChest.dll`, **55,808 bytes**, SHA-256 `AFE0D6B4…` — identical to the source.
✔

## Files changed / generated

**Committed (7 modified, 6 added):**
- Added: `CraftFromDrawersAndChests/ModInfo.cs`, `manifest.json`, `CHANGELOG.md`, `icon.png`,
  `DoPrebuild.props`, `REFERENCES.md`.
- Modified: `README.md` (rewritten from the 2-line stub), `Directory.Build.props`,
  `CraftFromDrawersAndChests/packages.config`, `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj`,
  `tools/Build.ps1`, `tools/New-ValheimRefs.ps1`, `tools/README.md`.
- `LICENSE.md` already existed (GPL-3.0) and is unchanged; the README links to it.

**Generated, not committed (gitignored):** `.references/**` (including `Mods/MultiUserChest.dll`),
`CraftFromDrawersAndChests/bin/{Debug,Release}/**`, `packages/**`, `tools/.cache/**`.

**`icon.png`:** a **generated 256×256 placeholder** (valid PNG, 3,301 bytes, verified via
`System.Drawing.Image` → 256×256). It is *not* final artwork; replace before publishing.

**Test-suite note:** the Tests suite is untouched by this task and stays 41/41.

## Self-review findings

- The prior attempt's on-disk work matched every ruling; I changed **no source** — I only verified,
  then committed. The only thing "missing" was the verification and the report.
- `README.md` satisfies the global constraints: it states that **every player *and* the server** must
  install the mod, and that **CraftFromContainers** and **AzuCraftyBoxes** must be removed before
  installing; there is no Nexus-flavoured content.
- `Logic/**` still references none of `UnityEngine`/`Valheim`/`BepInEx`/`Jotunn` in code — the only
  matches are XML-doc prose *stating* that constraint.
- `git status --porcelain` before committing was exactly the 7 modified + 6 untracked the task
  described; no build output or `.references` leaked into the index. `.gitignore` covers
  `bin/ obj/ packages/ Release/ .references/ tools/.cache/ .superpowers/ .vs/ *.user`.

## Concerns

1. **Release packaging is not wired (highest-value finding).** `Build.ps1` defaults to
   `-Configuration Release` and, after MSBuild, expects
   `Release\NorCal_Nerds-CraftFromDrawersAndChests\plugins\CraftFromDrawersAndChests.dll`. The product
   csproj defines **no** packaging target that stages the DLL into `plugins\` or copies the repo-root
   docs into the package. The reference repo has exactly this target — `XPortalNetworks.csproj`'s
   `<Target Name="Copy" AfterTargets="Build" Condition="Release">` (plus `CopyToDev`) — but Ruling 8's
   explicit "Required:" list does not include it. Actual result of running it:
   ```
   ==> Building (Release)
       Built …\bin\Release\CraftFromDrawersAndChests.dll (14,336 bytes)
   !! Packaging did not produce '…\Release\NorCal_Nerds-CraftFromDrawersAndChests\plugins\CraftFromDrawersAndChests.dll'.
   exit code: 1
   ```
   So `Build.ps1` with no arguments (Release) fails; only the Debug path is green. This is consistent
   with the brief (Step 4 expected *this task's* build to fail — Ruling 3 corrected the reason, not the
   stopping point) and with Ruling 3 scoping success to Debug, so I did **not** add the target. If the
   controller intends Task 6 to deliver a working Release/zip, the `Copy` (+ `CopyToDev`) targets must
   be ported into `CraftFromDrawersAndChests.csproj` (`$(ModRoot)/$(ProjectName)` docs,
   `$(ReleaseRoot)/$(PackageFolder)/plugins`, `$(DevPluginsFolder)/$(PackageFolder)`), and then
   `Build.ps1 -Configuration Release` should be the fourth check.
2. **`MSB3274` warning (benign).** `MultiUserChest.dll` is built against **.NET Framework 4.7.2** while
   the project targets **net462**, so MSBuild prints
   `warning MSB3274: The primary reference "MultiUserChest" could not be resolved because it was built
   against the ".NETFramework,Version=v4.7.2" framework…`. The build still succeeds and the reference is
   usable (Valheim's Mono runtime is 4.7.2-class). I deliberately did **not** add `MSB3274` to
   `NoWarn`/`MSBuildWarningsAsMessages`: Ruling 8 named only the Unity codes, and suppressing a real
   framework-mismatch warning could be read as weakening the csproj. Flagging it for the controller.
3. **`DoPrebuild.props` is a no-op.** Nothing imports it (neither here nor in the reference repo). It
   satisfies the letter of Ruling 8 but has no effect; noted so it is not mistaken for a live switch.
4. **`-MultiUserChestDll` default relies on `-BepInExCorePath`.** The default recursive search is rooted
   at `<parent of BepInExCorePath>\plugins`. If BepInEx is installed in the game folder *without* a
   `plugins\MSchmoecker-MultiUserChest` folder, the flag must be passed explicitly (the script warns and
   the product build then fails on the missing reference — the documented failure mode).
5. **`icon.png` is a placeholder** (see above) — must be swapped for real art before a Thunderstore
   release.

---

# Task 6 — Fix round: Release packaging (append)

**Date:** 2026-10-11. **Branch:** `feature/craft-from-drawers-and-chests`.
**Commit:** `af13928` — `fix: stage Release package (plugins DLL + docs) so Build.ps1 -Configuration Release succeeds`.

## What changed

The controller ruled the Release-packaging scope call wrong: a mod that cannot produce a release zip
has not finished the job, and "release packaging" is in Task 6's title. I ported the missing staging.

**`CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj`** (+45 lines, the only tracked change),
appended after the `Microsoft.CSharp.targets` / net462-reference-assemblies imports:

- A guarded `<PropertyGroup>` giving repo-relative defaults so a bare MSBuild/VS build is well-defined:
  `ReleaseRoot` → `$(MSBuildProjectDirectory)\..\Release`, `ModRoot` → `$(MSBuildProjectDirectory)\..`,
  `PackageFolder` → `$(AssemblyName)` (**not** the reference repo's `<AssemblyName>-Vapok`),
  `DevPluginsFolder` → the `/tmp/craftfromdrawersandchests-devplugins` sentinel. Every default is
  `Condition="'$(X)' == ''"`, so the values `tools/Build.ps1` passes with `/p:` always win — nothing is
  hardcoded.
- **`<Target Name="CopyToDev" AfterTargets="Build" ...>`** — ported verbatim in shape; condition
  requires `Exists('$(DevPluginsFolder)')`, and `Build.ps1` passes a `/tmp/…` sentinel that does not
  exist on this machine, so the target stays **inert** (confirmed: no dev-deploy messages in either
  build log).
- **`<Target Name="Copy" AfterTargets="Build" Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">`**
  — the reference target, ported. It copies `$(TargetPath)` →
  `$(ReleaseRoot)\$(PackageFolder)\plugins`, `Translations\**\*.*` →
  `…\plugins\Translations\…`, and the package docs from `$(ModRoot)\$(ProjectName)\` →
  `$(ReleaseRoot)\$(PackageFolder)\`. The five doc items (`LICENSE.md`, `manifest.json`,
  `CHANGELOG.md`, `README.md`, `icon.png`) each carry `Condition="Exists(...)"`, matching
  `Build.ps1`'s `New-DocStage`, which likewise skips any that are missing — so the two agree instead
  of the csproj hard-failing on an absent doc.

**Deliberately not adopted:** the reference's `Docs/` T4 pipeline, its Nexus README, and its
`PackageFolder` default of `<AssemblyName>-Vapok`. Identity values are untouched
(`CraftFromDrawersAndChests` / `NorCal_Nerds`). The mod's `Translations\` folder does not exist yet, so
the translation copies are empty no-ops today but are forward-compatible.

## Commands run and their output

### Check 1 — `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Release`

```
CraftFromDrawersAndChests build - Release
    Version 0.1.0
    Package folder NorCal_Nerds-CraftFromDrawersAndChests
==> Validating
    manifest.json = 0.1.0
    JSON OK (0 files)
    References: …\.references (Valheim 1.0.16)
==> Restoring NuGet packages
    Packages restored
==> Building (Release)
… warning MSB3274 (MultiUserChest net472 vs net462 — pre-existing, benign) …
  CraftFromDrawersAndChests -> …\bin\Release\CraftFromDrawersAndChests.dll
    Built …\bin\Release\CraftFromDrawersAndChests.dll (14,336 bytes)
    Package: …\Release\NorCal_Nerds-CraftFromDrawersAndChests
==> Packaging zip
    Zip: …\Release\CraftFromDrawersAndChests-release.zip (23,887 bytes)
    Zip: …\Release\CraftFromDrawersAndChests-0.1.0.zip (23,887 bytes)

BUILD OK - CraftFromDrawersAndChests 0.1.0 (Release)
```

`Release\` tree (sizes in bytes):

```
    23,887  Release\CraftFromDrawersAndChests-0.1.0.zip
    23,887  Release\CraftFromDrawersAndChests-release.zip
       343  Release\NorCal_Nerds-CraftFromDrawersAndChests\CHANGELOG.md
     3,301  Release\NorCal_Nerds-CraftFromDrawersAndChests\icon.png
    35,823  Release\NorCal_Nerds-CraftFromDrawersAndChests\LICENSE.md
       421  Release\NorCal_Nerds-CraftFromDrawersAndChests\manifest.json
     3,796  Release\NorCal_Nerds-CraftFromDrawersAndChests\README.md
    14,336  Release\NorCal_Nerds-CraftFromDrawersAndChests\plugins\CraftFromDrawersAndChests.dll
```

Package shape is exactly `Release\<Team>-<Package>\plugins\<AssemblyName>.dll` + the staged root docs,
with both zips present and identical in size. ✔

### Check 2 — `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`

```
==> Building (Debug)
  CraftFromDrawersAndChests -> …\bin\Debug\CraftFromDrawersAndChests.dll
    Built …\bin\Debug\CraftFromDrawersAndChests.dll (15,360 bytes)

BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```

**BUILD OK**, DLL still **15,360 bytes** — no regression, and `CopyToDev` stayed inert. ✔

### Check 3 — `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`

```
NUnit3TestExecutor discovered 41 of 41 NUnit test cases
Test summary: total: 41, failed: 0, succeeded: 41, skipped: 0, duration: 1.7s
Build succeeded in 4.0s
```

**41/41 green.** ✔

### Check 4 — `Release/` is gitignored

```
$ git status --porcelain
 M CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj
$ git check-ignore -v Release\CraftFromDrawersAndChests-release.zip
.gitignore:5:Release/   "Release\\CraftFromDrawersAndChests-release.zip"
```

Only the csproj is modified; `Release/…` (and both zips) are ignored by `.gitignore:5`. Nothing from
`Release/` is committed. ✔

## Concerns (fix round)

1. **`MSB3274` still prints** (MultiUserChest is net472, project is net462). Pre-existing, benign,
   deliberately left unsuppressed (unchanged from the earlier round).
2. **`icon.png` is still the 256×256 placeholder** — unchanged; must be swapped for real art before a
   Thunderstore release.
3. **Translations staging is a no-op today** (no `Translations\` folder in the product). It mirrors the
   reference target and will pick up files automatically once that folder exists.
4. **`DoPrebuild.props` remains a no-op** (nothing imports it) — unchanged from the earlier round.
