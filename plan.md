# CraftFromDrawersAndChests — Valheim 1.0.20 compatibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move this repo's build baseline onto the Valheim 1.0.20 assemblies, ship it as an un-released 1.0.4 build, and hand the owner a checklist that proves the mod still works in game — after establishing, from the assemblies themselves, that the update broke nothing.

**Architecture:** No product code changes. The 2026-10-16 game update changed 6 members of `assembly_valheim.dll` and none of the 24 members this mod patches or reflects; so the deliverable is (a) a durable evidence record, (b) regenerated reference assemblies plus the one-line version flip the repo already centralises in `Directory.Build.props`, (c) a version bump to 1.0.4 with no release artifacts, and (d) an in-game checklist. Every claim is checked against a primary artifact (assembly hash, Cecil dump, build output, test run) — never against a web summary.

**Tech Stack:** Valheim 1.0.20 (net462 game assemblies), BepInEx 5.4.23.5 (pack 5.4.2351), HarmonyX, Jötunn 2.30.2, MultiUserChest 0.6.2, NUnit, Mono.Cecil, CabbageCrow/AssemblyPublicizer, MSBuild via `tools/Build.ps1`.

**Spec:** `docs/sdd/2026-10-11-craft-from-drawers-and-chests/game-1.0.20-compat-analysis.md` (created by Task 1; this plan argues from it).

**Plan location note:** `.github/copilot-instructions.md` requires the plan at `plan.md` (its gate reads that path), so it lives at the repo root. The writing-plans skill's default is `docs/superpowers/plans/YYYY-MM-DD-<feature>.md`, which is where the previous plan sits. Local policy wins; do not duplicate this file.

## Global Constraints

- Valheim game version: **1.0.20** (folder name and `ValheimGameVersion` value — the owner read it off the game).
- Mod version for this build: **1.0.4** — `ModInfo.Version` and root `manifest.json` `version_number` must match or `Build.ps1` fails by design.
- **Do not publish.** No GitHub release, no Thunderstore upload — the owner tests first and releases separately. (This originally also forbade producing the zip; the owner asked for the zip on 2026-10-16, so `-Configuration Release` packaging now happens — see Task 5. Publishing itself is still held.)
- **No product code changes.** `CraftFromDrawersAndChests/**/*.cs` other than `ModInfo.cs`'s version string must be untouched. If any task seems to need one, stop and report instead.
- `Logic/**` must not reference `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`.
- BepInEx reference folder stays **5.4.2350** (the csproj `HintPath` and `tools/Build.ps1` default both use it; both packs ship the same 5.4.23.5 core).
- `.references/` and `Release/` are gitignored; never commit them.
- Commits are local. **Never push** — the owner pushes in batches.
- `git add` explicit paths only, never `-A`.
- `.references/Valheim/1.0.16/` is left in place. Deleting it needs an explicit owner instruction.
- Web tools are dead in this environment (`fetch_webpage` fails, `github_repo` errors). Do not plan any step that needs them; patch notes are unavailable, and the version label is the owner's word.

## Established evidence (do not re-derive; re-verify only if a check fails)

- Steam `appmanifest_892970`: buildid **25909357**; `Managed\assembly_valheim.dll`, `assembly_utils.dll`, `Assembly-CSharp.dll` all rewritten **2026-10-16 06:56:42**.
- Pre-update log (`...Modheim-interim\BepInEx\LogOutput.log`, 2026-10-13) said the game was **1.0.17** and this mod loaded and patched **22 methods with 0 errors** — so the repo's `1.0.16` label was already one behind.
- `assembly_valheim.dll` API delta old→new: **6 removed / 21 added**. `assembly_utils.dll`: **0/0** (bytes differ, API identical).
  - Removed: `ItemDrop.ResetVariantIfInvalid()` (renamed `EnsureValid`), `CharacterDrop.s_pseudoCounter` (retyped `Dictionary<string,Tuple<float,int>>` → `Dictionary<int,int>`), 2 `<PrivateImplementationDetails>` blobs.
  - Added: internal `CheatUtils` (`IsCheated`/`SetCheated`/`RemoveCheated`/`HasCheatedProperties` over the pre-existing `ZDOVars.s_cheated`, `== 2` means cheated), `Player.GetItemUpgradeChance(int)` + `Player.m_itemUpgradeSeeds`, `Version/{Item,Player,World,PlayerData}` enum members, `CharacterDrop/Drop::m_levelMultiplierNotAmount`.
- **No access-modifier or signature change anywhere in the assembly.** All 24 mod touchpoints are identical old vs new.
- The mod's 24 touchpoints: `Container.Awake|GetHoverText|OnDestroyed|CheckAccess`, `CookingStation.OnAddFuelSwitch|OnInteract`, `Fireplace.GetHoverText|Interact`, `InventoryGui.OnCraftPressed|SetupRequirement|Update|UpdateCraftingPanel`, `Player.ConsumeResources|HaveRequirementItems|HaveRequirements(×2)|Update|UpdatePlacement|m_placementStatus`, `Smelter.OnAddFuel|OnAddOre|OnHoverAddFuel|OnHoverAddOre`, `Terminal.InputText`, `Turret.UseItem`.
- All 22 patches are `[HarmonyPrefix]`/`[HarmonyPostfix]`; **zero transpilers**.
- `tools/Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 1.0.3 (Debug)`; `dotnet test` → **262 passed / 0 failed** (pre-change baseline, on the old refs).
- The NUnit suite is `Logic/**`-only and cannot observe game APIs. It is a regression guard, not evidence of game compatibility; the assembly diff is the evidence.

## Review Focus

Failure modes this plan's automated checks cannot catch, most likely first — each gets its check in Task 4's owner checklist:

1. **An existing pre-update world loads through the new `Version/World::CheatCleanSlate` migration** — containers the mod tracks must still load, still be pullable, and must not lose or duplicate items.
2. **The mod leaves no cheat flag behind** — no container or placed piece the mod touched may become flagged (`ZDOVars.s_cheated == 2`), which the new clean-slate pass would then act on.
3. **Craft/placement costs are unchanged** under the new `Player.GetItemUpgradeChance`/`m_itemUpgradeSeeds` upgrade system — a piece must still never cost nothing.
4. **Client-side station feeding on a dedicated server** — the only multi-peer path, and the one that historically needed a second round.
5. **MultiUserChest interop on a non-host client** — deferred inventory writes are the most order-sensitive thing this mod does.

---

### Task 1: Persist the compatibility analysis as the durable record

**Files:**
- Create: `docs/sdd/2026-10-11-craft-from-drawers-and-chests/game-1.0.20-compat-analysis.md`
- Create: `docs/sdd/2026-10-11-craft-from-drawers-and-chests/tools/dump-api.ps1`
- Source to copy from: `%TEMP%\cfdac-1020\dump-api.ps1` (written this session; if absent, re-create it from the description in Step 3)

**Interfaces:**
- Produces: the evidence document every later task cites as its spec, and a reusable probe other tasks invoke.
- Produces: `dump-api.ps1 -Assembly <dll> -OutFile <txt>` — writes one sorted line per type/method/field/property as `<kind>|<Type::Member>|<param-or-field types>|<return type>|<attributes>`; exits non-zero on any Cecil type-resolution failure.

- [ ] **Step 1: Copy the hardened dumper into the SDD tools folder**

Copy `%TEMP%\cfdac-1020\dump-api.ps1` to `docs/sdd/2026-10-11-craft-from-drawers-and-chests/tools/dump-api.ps1` unchanged. It must keep the hardening that `tools/cecil.ps1` documents: `$ErrorActionPreference='Stop'`, every Cecil type resolved before any loop, results accumulated in a `List[string]`, module `Dispose()`d in a `finally`, and output written with `Set-Content -Encoding UTF8`.

- [ ] **Step 2: Write the analysis document**

Record, with the evidence, in `game-1.0.20-compat-analysis.md`:
1. **Trigger** — buildid 25909357, the three rewritten filenames and `2026-10-16 06:56:42`, and the SHA256 pairs proving `assembly_valheim.dll`/`assembly_utils.dll` differ from `.references/Valheim/1.0.16/Managed/` while `assembly_guiutils.dll`/`assembly_postprocessing.dll` are identical.
2. **Version label** — state plainly that 1.0.20 is the owner's reading and that the only local record is the pre-update 1.0.17 log line; the version is not in the DLLs, `app.info`, or `globalgamemanagers`, and web tools were unavailable.
3. **Delta** — the 6 removed / 21 added members, verbatim, and the 0/0 for `assembly_utils.dll`; note `CheatUtils` wraps a pre-existing ZDO var and that the mod references none of the changed members.
4. **Touchpoint table** — all 24 members, each `old == new` (signature and access), with the access modifiers as dumped.
5. **Verdict** — no code change required; the reasoning being "no access or signature change anywhere" plus "no transpilers".
6. **How to re-run** — the two commands: `dump-api.ps1` over the installed assembly and over `.references/Valheim/<ver>/Managed/`, then `Compare-Object $old $new -CaseSensitive` (the switch matters; PowerShell string comparison is case-insensitive by default).
7. **Anti-cheat scope** — `IsCheated` callers (spawn/drop paths, `Container.AddDefaultItems`, `InventoryGui.DoCrafting`, `Smelter.Spawn`, `Piece.DropResources`, `Fermenter.DelayedTap`, `CookingStation.DropAllItems`, `ZDOMan.ConvertInventories`) and `SetCheated` callers (`Player.PlacePiece`, `Character.ApplyDamage`, `Destructible.Destroy`, `Ragdoll.Setup`, cheat `Terminal` spawn, `ZoneSystem.SpawnLocation`), and the conclusion that container-to-container item movement is not implicated.

- [ ] **Step 3: Verify the record's load-bearing numbers are present**

Run:
```powershell
$f='docs\sdd\2026-10-11-craft-from-drawers-and-chests\game-1.0.20-compat-analysis.md'
Select-String -Path $f -Pattern '25909357','6 removed','21 added','ResetVariantIfInvalid','CheatUtils','24','25909357' | Measure-Object | Select-Object -ExpandProperty Count
```
Expected: `7` or more matches — every anchor found. A `0` means the document is missing evidence, not that the evidence is absent.

- [ ] **Step 4: Verify the copied probe still runs**

Run:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'docs\sdd\2026-10-11-craft-from-drawers-and-chests\tools\dump-api.ps1' -Assembly '.references\Valheim\1.0.16\assembly_valheim_publicized.dll' -OutFile "$env:TEMP\cfdac-1020\check_publicized.txt"
```
Expected: one line, `assembly_valheim_publicized.dll -> check_publicized.txt : <N> member lines` with N > 20000, and no error records.

- [ ] **Step 5: Commit**

```powershell
git add docs/sdd/2026-10-11-craft-from-drawers-and-chests/game-1.0.20-compat-analysis.md docs/sdd/2026-10-11-craft-from-drawers-and-chests/tools/dump-api.ps1
git commit -m "docs: record the Valheim 1.0.20 API delta and the 24-touchpoint compatibility proof"
```

---

### Task 2: Point the build at Valheim 1.0.20

**Files:**
- Modify: `Directory.Build.props:3` (`<ValheimGameVersion>`) — the single source of truth
- Modify: `tools/Build.ps1:151` (the `return '1.0.16'` fallback in `Get-ValheimGameVersion`)
- Modify: `tools/New-ValheimRefs.ps1:104` (the `if (-not $GameVersion) { $GameVersion = '1.0.16' }` fallback)
- Create (generated, gitignored): `.references/Valheim/1.0.20/**`

**Interfaces:**
- Consumes: nothing from Task 1 at runtime; Task 1's document is the justification for the version value.
- Produces: `.references/Valheim/1.0.20/assembly_valheim_publicized.dll` and `.references/Valheim/1.0.20/Managed/*.dll`, which `CraftFromDrawersAndChests.csproj` binds through `$(ReferencesRoot)\Valheim\$(ValheimGameVersion)\...`. Both later tasks build against these.

- [ ] **Step 1: Flip the single source of truth**

In `Directory.Build.props`, change `<ValheimGameVersion>1.0.16</ValheimGameVersion>` to `<ValheimGameVersion>1.0.20</ValheimGameVersion>`. Change nothing else. Note the consequence: `ReferencesRoot` is only set when `...\Valheim\$(ValheimGameVersion)\assembly_valheim_publicized.dll` exists, so **do not build between this step and Step 3**.

- [ ] **Step 2: Bump the two stale hardcoded fallbacks**

`tools/Build.ps1:151` → `return '1.0.20'`; `tools/New-ValheimRefs.ps1:104` → `{ $GameVersion = '1.0.20' }`. These apply only when `Directory.Build.props` cannot be read, but leaving `1.0.16` there contradicts the "single source of truth" comment and silently misleads the next reader.

- [ ] **Step 3: Regenerate the reference assemblies**

Run from the repo root (absolute paths; the runner strips a leading `cd`, so do not rely on one):
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 -GameVersion 1.0.20 -PublicizerPath .\tools\.cache\publicizer\AssemblyPublicizer\AssemblyPublicizer.exe -BepInExCorePath "$env:APPDATA\com.kesomannen.gale\valheim\profiles\Modheim-interim\BepInEx\core" -MultiUserChestDll "$env:APPDATA\com.kesomannen.gale\valheim\profiles\Modheim-interim\BepInEx\plugins\MSchmoecker-MultiUserChest\MultiUserChest.dll"
```
Expected: `.references\Valheim\1.0.20\assembly_valheim_publicized.dll` plus `assembly_utils_publicized.dll`, `assembly_guiutils_publicized.dll`, `assembly_postprocessing_publicized.dll`, `Managed\`, and `.references\BepInEx\5.4.2350\BepInEx\core\`. All inputs are verified present; the script may re-copy the BepInEx core (same 5.4.23.5 core either way).

- [ ] **Step 4: Prove the new references are the installed game, not a stale copy**

Run:
```powershell
$g="${env:ProgramFiles(x86)}\Steam\steamapps\common\Valheim\valheim_Data\Managed"
foreach ($n in @('assembly_valheim.dll','assembly_utils.dll')) {
  $a=(Get-FileHash "$g\$n" -Algorithm SHA256).Hash
  $b=(Get-FileHash ".references\Valheim\1.0.20\Managed\$n" -Algorithm SHA256).Hash
  "{0}: {1}" -f $n,($a -eq $b)
}
```
Expected: both `True`. This is the check that would have caught the drift that made the repo's `1.0.16` label stale.

- [ ] **Step 5: Re-verify all 24 touchpoints against the NEW publicized assembly**

Reuse Task 1's probe: dump `.references\Valheim\1.0.20\assembly_valheim_publicized.dll` to `$env:TEMP\cfdac-1020\pub120.txt`, then run the same 24-target loop used on the unpublicized pair, matching `^[MF]\|<Type>::<Member>\|`. Expected: **24 of 24 found, 0 missing**. Compare *signatures only* — a publicized assembly reports every member as `public`, so access modifiers are expected to differ here and must not be compared.

- [ ] **Step 6: Build and test against the new references**

Run:
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug
dotnet test "Tests\CraftFromDrawersAndChests.Tests.csproj" --nologo
```
Expected: `References: ...\.references (Valheim 1.0.20)` and `BUILD OK - CraftFromDrawersAndChests 1.0.3 (Debug)` (still 1.0.3 — Task 3 bumps it), the usual single `MSB3274`; then `total: 262, failed: 0, succeeded: 262`.

- [ ] **Step 7: Commit**

```powershell
git add Directory.Build.props tools/Build.ps1 tools/New-ValheimRefs.ps1
git commit -m "build: compile against the Valheim 1.0.20 reference assemblies"
```

---

### Task 3: Build 1.0.4 (no release)

**Files:**
- Modify: `CraftFromDrawersAndChests/ModInfo.cs:16` (`public const string Version = "1.0.3";` → `"1.0.4"`)
- Modify: `manifest.json` (`version_number`: `1.0.3` → `1.0.4`)
- Modify: `CHANGELOG.md` (new top entry)
- Modify: `REFERENCES.md:4`, `REFERENCES.md:9`, `REFERENCES.md:68` (game version and the stale `0.1.0` mod version)

**Interfaces:**
- Consumes: `.references/Valheim/1.0.20/...` from Task 2 (the build fails with "Reference assemblies for Valheim 1.0.20 not found" otherwise).
- Produces: `CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll` version 1.0.4 — the artifact the owner deploys for Task 4. No zip, no `Release/` folder, no upload.

- [ ] **Step 1: Bump the version in its two required places**

`CraftFromDrawersAndChests/ModInfo.cs` → `public const string Version = "1.0.4";` (keep the exact `public const string <Name> = "<value>";` form — `Build.ps1` reads it by regex). `manifest.json` → `"version_number": "1.0.4"`. `Build.ps1` fails if these disagree, which is the point.

- [ ] **Step 2: Add the changelog entry**

Add above the `## [1.0.3]` entry, in the file's existing Keep-a-Changelog style:
```markdown
## [1.0.4] - 2026-10-16

Compatibility with Valheim 1.0.20. **No behaviour change** — the game update removed 6 and added 21 members
across `assembly_valheim.dll`, none of them one this mod patches or reflects, and it changed no access
modifier or signature the mod binds. See `docs/sdd/.../game-1.0.20-compat-analysis.md`.

### Changed

- **The build now compiles against Valheim 1.0.20** (`ValheimGameVersion`), down from 1.0.16.
```
Keep the entry factual: do **not** claim any in-game verification, because the owner has not run it yet.

- [ ] **Step 3: Correct the stale facts in `REFERENCES.md`**

Line 9 game version → `Valheim 1.0.20`; line 68 table cell → `— 1.0.20`; line 4 mod version → `1.0.4`. Grep first and fix every occurrence, not just the three: `Select-String -Path REFERENCES.md -Pattern '1\.0\.16','0\.1\.0'`.

- [ ] **Step 4: Build and test**

Run:
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug
dotnet test "Tests\CraftFromDrawersAndChests.Tests.csproj" --nologo
```
Expected: `Version 1.0.4`, `manifest.json = 1.0.4`, `References: ... (Valheim 1.0.20)`, `BUILD OK - CraftFromDrawersAndChests 1.0.4 (Debug)`; then 262/262 passing.

- [ ] **Step 5: Confirm no release artifact was produced**

Run:
```powershell
Get-ChildItem Release -Recurse -File -ErrorAction SilentlyContinue | Where-Object Name -match '1\.0\.4' | Select-Object -ExpandProperty FullName
```
Expected: **no output**. If anything matches, a Release build ran — delete only that file and re-check; do not run `-Configuration Release`.

- [ ] **Step 6: Commit**

```powershell
git add CraftFromDrawersAndChests/ModInfo.cs manifest.json CHANGELOG.md REFERENCES.md
git commit -m "release: 1.0.4 - built against Valheim 1.0.20 (not published)"
```

---

### Task 4: Hand the owner the in-game checklist for the new game build

**Files:**
- Modify: `docs/sdd/2026-10-11-craft-from-drawers-and-chests/author-verify.md` (append to the Validation log, then the new check section)

**Interfaces:**
- Consumes: the 1.0.4 DLL from Task 3, deployed by the owner to `BepInEx\plugins\` on **both** server and client using the same build.
- Produces: the owner-run sign-off that lifts the "no release" hold. This task's verification is the owner's run; the agent's check is only that the checklist is complete and unambiguous.

- [ ] **Step 1: Add the validation-log entry**

Under `## Validation log`, add an entry recording that the 1.0.4 build is **built but not yet run in game**, so "reviewed clean" is never mistaken for "works":
```markdown
- **1.0.4 (Valheim 1.0.20), built 2026-10-16, NOT YET RUN IN GAME:** no code change; the update renamed
  `ItemDrop.ResetVariantIfInvalid` → `EnsureValid` and added a cheat-flag system, touching none of the 24
  members this mod binds. Awaiting the checks below before release.
```

- [ ] **Step 2: Add the five checks, one per Review Focus line**

Append a `## Valheim 1.0.20 update` section whose items each name the action, the expected result, and the failure it pins:

1. Load a **pre-update** world with existing chests and drawers. Expected: it loads, the mod's log line reports the patched methods as before, and every container's contents are intact (count one known chest before and after).
2. Ctrl-click pull from a chest, then run the `cfdac` console command. Expected: the readout matches the pulled amounts and nothing is duplicated.
3. Craft a multi-ingredient recipe and place a piece using storage materials. Expected: the whole cost is consumed each time, the second press still delivers (the 1.0.2 fix), and no craft/placement ever costs nothing.
4. On a dedicated server, have the **client** feed a fireplace and a smelter. Expected: fuel/ore arrive, the hover text is correct, and no spurious "you don't have any …" message appears.
5. Store into and pull from a chest with MultiUserChest present, as a **non-host** client. Expected: contents survive a rejoin and no item is lost or duplicated.

- [ ] **Step 3: State the release gate explicitly**

End the section with: *"Release 1.0.4 only after all five pass. Any failure goes to `/investigate` — do not patch from a guess."*

- [ ] **Step 4: Verify the section is complete**

Run:
```powershell
$f='docs\sdd\2026-10-11-craft-from-drawers-and-chests\author-verify.md'
(Select-String -Path $f -Pattern 'Valheim 1\.0\.20','NOT YET RUN IN GAME','1\.0\.4' | Measure-Object).Count
```
Expected: `3` or more. Also confirm all five checks are present: `(Select-String -Path $f -Pattern '^[0-9]\.' | Measure-Object).Count`.

- [ ] **Step 5: Commit**

```powershell
git add docs/sdd/2026-10-11-craft-from-drawers-and-chests/author-verify.md
git commit -m "docs: in-game checklist for the 1.0.4 build on Valheim 1.0.20"
```

---

## Self-Review

**1. Spec coverage.** The spec is Task 1's `game-1.0.20-compat-analysis.md`, which is produced by the plan itself; its content is specified item by item in Task 1 Step 2. Every requirement the owner stated maps to a task: *analyze the changes* → Task 1; *run tests* → already done on the old refs and re-run in Tasks 2 and 3 Steps 6/4; *ensure compatible* → Task 2 Steps 4–6 (hash identity, 24/24 touchpoints, build + 262 tests); *build 1.0.4, no release until tested* → Task 3 plus the explicit no-release checks in Steps 5/6 and the gate in Task 4 Step 3.

**2. Step scan.** Every step is one action with one checkable result. No step says "handle edge cases" or "add appropriate validation". The only deliberately open values are the changelog prose and the checklist wording, which are copy decisions the plan fixes in outline and the implementer writes once.

**3. Type consistency.** `ValheimGameVersion` (1.0.20) is used identically in `Directory.Build.props`, the two fallbacks, the refs path, and every expected build-output string. The mod version 1.0.4 appears as the same literal in `ModInfo.cs`, `manifest.json`, `CHANGELOG.md`, `REFERENCES.md`, and the expected `BUILD OK` line. `dump-api.ps1` has one parameter contract, stated once in Task 1's Interfaces block and reused verbatim in Task 2 Step 5.

**4. Review Focus.** All five failure modes have an owner check in Task 4 Step 2, in the same order, one-to-one.

**5. Proportion.** Four tasks, no product code, no code bodies. The plan is longer than the change it describes only because the evidence and the no-release boundary both need to survive a hand-off.

---

## Task 5: Package the 1.0.4 release zip (owner-requested 2026-10-16)

Supersedes Task 3 Step 5's "no release artifact" check, which was correct when it ran. **Publishing stays held.**

**Files:** produces (all gitignored) `Release\CraftFromDrawersAndChests-1.0.4.zip`,
`Release\CraftFromDrawersAndChests-release.zip`, and `Release\NorCal_Nerds-CraftFromDrawersAndChests\**`.

- [x] **Step 1: Build the package.** `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Release`.
      Expected and observed: `Built …bin\Release\CraftFromDrawersAndChests.dll (84,480 bytes)`,
      `Package: Release\NorCal_Nerds-CraftFromDrawersAndChests`, both zips at 64,425 bytes, and
      `BUILD OK - CraftFromDrawersAndChests 1.0.4 (Release)`. `Build.ps1` contains no upload step and the repo
      has no CI workflow, so this cannot publish anything by itself.
- [x] **Step 2: Verify the artifact, not the tree.** The versioned zip and `-release.zip` are byte-identical
      (same SHA256); the zip contains `plugins\CraftFromDrawersAndChests.dll`, `manifest.json`, `README.md`,
      `CHANGELOG.md`, `LICENSE.md`, `icon.png`; the packaged `manifest.json` reads `1.0.4`; the packaged DLL
      hashes equal `bin\Release\CraftFromDrawersAndChests.dll`; and the DLL's `[BepInPlugin]` blob reads
      `NorCal_Nerds.CraftFromDrawersAndChests.CraftFromDrawersAndChests.1.0.4` with no stale `1.0.3`. Scan that
      blob as **ASCII** — attribute strings are UTF-8, and a UTF-16 scan reports a false negative.
- [x] **Step 3 (owner): test.** Deploy the packaged `plugins\CraftFromDrawersAndChests.dll` to server and client
      by hand — the build deliberately does not deploy (`/p:DevPluginsFolder` points at a non-existent path) —
      and run the five checks in "Valheim 1.0.20 update". **Done for the load path only:** the owner installed
      the packaged DLL into the `Modheim` profile and launched on Valheim 1.0.20 — the game itself reports that
      version, all 22 patches applied, tracking was live, **0 errors**. The five functional checks are still open.
- [x] **Step 4 (owner): publish.** **Published 2026-10-16** as `v1.0.4`:
      <https://github.com/ghstwhl/CraftFromDrawersAndChests/releases/tag/v1.0.4> — `draft = false`,
      `prerelease = false`, one asset `CraftFromDrawersAndChests-1.0.4.zip`, 64,425 bytes, equal to the local
      build; tag and `main` both at `4ce6fa6`. Full record: `release-1.0.4-notes.md`. Thunderstore was **not**
      uploaded (its state could not be checked — the web tools are unavailable here).

## Execution record (2026-10-16)

Tasks 1–4 complete and committed locally; **nothing pushed**.

| Commit | Task |
|---|---|
| `fc06adc` | 1 — the analysis record and `tools/dump-api.ps1` |
| `d62b1b3` | 2 — `ValheimGameVersion` 1.0.20 plus regenerated refs, hash-verified against the install |
| `ce8d041` | 3 — 1.0.4 in `ModInfo.cs`/`manifest.json`, CHANGELOG, `REFERENCES.md` |
| `e8e8fda` | 4 — the in-game checklist |

Final verified state: `BUILD OK - CraftFromDrawersAndChests 1.0.4` in Debug and Release; `dotnet test` 262/262;
24/24 touchpoints present in the 1.0.20 publicized assembly; the packaged DLL reports `1.0.4`. Left alone
uncommitted, as the owner's own work: `AGENTS.md`, the 12-line `author-verify.md` correction, and this file.

---

# Plan: 1.0.5 — shield generators are not fed from storage (2026-10-23)

> **For agentic workers:** Same loop as above. Track steps with `- [ ]`.

**Goal:** make `piece_shieldgenerator` behave like every other fuelled station: pull its fuel from
storage in range of the **generator**, and let the fill-all modifier feed the whole remaining capacity.

**Reported by the owner**, from a local session on Valheim 1.0.20 (`Modheim` profile, log
`2026-10-23 21:39`): feeding `BoneFragments` to a `piece_shieldgenerator` pulls nothing from nearby
containers (or the backpack); the fill-all hint is absent; the modifier "only fills one item".

**Established evidence (from the shipping assembly, `docs/sdd/.../tools/cecil.ps1` + `dump-api.ps1`)**

- `assembly_valheim.dll` (1.0.20, unpublicized) declares `ShieldGenerator`, a `MonoBehaviour` that is the
  fourth fuelled station. Its fuel API is `m_fuelItems : List<ItemDrop>` (**public**, a *list* of fuel
  prefabs — a smelter has one `m_fuelItem`), `m_maxFuel : Int32` (public), `GetFuel() : Single`
  (**private**), `RPC_AddFuel(Int64)` (private; adds exactly **1** fuel per call and is registered in
  `Start`), `m_add : String` (public — the hover's action word), `m_nview : ZNetView` (**private**).
- The interaction is **not** `Interact` (that fires the shield: `m_enableAttack` + `RPC_Attack`). Fuel
  goes through `OnAddFuel(Switch, Humanoid, ItemData)` — **private**, and its only two call sites in the
  whole assembly are `ShieldGenerator.Start` and `Smelter.Awake`, i.e. both wire it into a `Switch`'s
  `m_onUse`. `Switch.Interact` invokes `m_onUse(this, user, null)`; `Switch.UseItem` invokes
  `m_onUse(this, user, item)`. The hover is `OnHoverAddFuel()` (private, also wired in `Start`, into
  `Switch.m_onHover`; `Switch.GetHoverText` returns it).
- Vanilla's `OnAddFuel` removes **one** item and adds **one** fuel — which is exactly why the owner saw
  "shift-E adds one item": nothing in this mod patches `ShieldGenerator`, so the modifier has no effect.
- Cause: `StationPatches` patches `Fireplace`, `Smelter` and `CookingStation` only. `Smelter` and
  `ShieldGenerator` are the **only** types in the assembly that own an `OnAddFuel`, so the generator is
  the single station the mod left out. Container *collection* is origin-based and already correct, so
  nothing else needs changing.

**Decision — reuse `PullFuel` and `FuelDisallowTypes`.** A generator's fuel is fuel; CraftFromContainers
4.0.7 has no shield-generator patch to copy, so inventing a fourth setting would be new surface with no
oracle. Documented in the class remarks.

**Not in scope — the Adventure Backpacks backpack.** A backpack is deliberately **not** an
`IStorageSource` (`docs/sdd/.../backpack-requirement.md`, the owner's own requirement: never pull items
out of a player's own backpack). Adventure Backpacks extends `Inventory.HaveItem`/`RemoveItem` only
inside its `CraftingContext`, which a `Switch` interaction never opens, so backpack fuel is not
consumable on this path by either mod. Reported to the owner rather than changed unilaterally.

**Testing note:** all the arithmetic this feature needs (`RemainingFuelCapacity`, `FillAmount`,
`IsDisallowedType`, `FillAllHint`) is already pure and already pinned in
`Tests/Logic/StationFeedRulesTests.cs`; the new code is purely the game-facing seam. Per the Valheim
modder doctrine §7, a game-facing seam is verified by artifact checks (§9) plus the owner's in-game pass —
not by a unit test, which the `Logic/**`-only test project cannot write (it has no game assemblies).

### Task 1: feed `ShieldGenerator` from storage

**Files:** Modify `CraftFromDrawersAndChests/Patches/StationPatches.cs`

- [x] **Step 1:** resolve `ShieldGenerator.GetFuel` through `Resolve` (private) and add
      `ShieldFuelRemaining`, mirroring `SmelterFuelRemaining`.
- [x] **Step 2:** add a `[HarmonyPrefix]` on `ShieldGenerator.OnAddFuel` that mirrors the smelter's fuel
      patch: bail on a held `item`, on `PullFuel` off, on the prevent key, on a full generator, and when
      the player already carries one of `m_fuelItems` (unless fill-all is held); otherwise pull the first
      fuel storage can supply (range origin = the generator) and feed it through `RPC_AddFuel` one unit
      per invocation, confirming the generator's remaining capacity fell before removing another unit.
- [x] **Step 3:** add a `[HarmonyPostfix]` on `ShieldGenerator.OnHoverAddFuel` appending the fill-all
      hint, with `m_add` as the action word.
- [x] **Step 4:** update the class remarks and the `PullSetting.Fuel` doc to name the generator.

### Task 2: document and version the build

- [x] **Step 1:** `PluginConfig` — `Pull fuel` help text names shield generators.
- [x] **Step 2:** `README.md` — the station-feeding bullet names shield generators.
- [x] **Step 3:** bump `ModInfo.Version` and `manifest.json` `version_number` to `1.0.5` together, and add
      the `1.0.5` CHANGELOG entry (the build fails if the two versions disagree).

### Task 3: verify without the game

- [x] **Step 1:** `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` →
      `BUILD OK - CraftFromDrawersAndChests 1.0.5 (Debug)` (MSB3274 is expected).
- [x] **Step 2:** `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 262 passed / 0 failed.
- [x] **Step 3:** confirm both patch targets exist with the expected signatures in the shipping assembly
      (`cecil.ps1 -Type ShieldGenerator -Method OnAddFuel -Definitions`, `-Method OnHoverAddFuel`), and
      that the built DLL names `ShieldGenerator` (ASCII scan — attribute strings are UTF-8).

### Task 4 (owner): in-game checklist

- [ ] Stand at a `piece_shieldgenerator` with `BoneFragments` only in a nearby chest: one `E` on the
      fuel switch adds **one** fuel and takes **one** from the chest; the chest stock falls by exactly
      the fuel added.
- [ ] Hold the fill-all key (`left shift` by default) and press `E`: the generator fills to `m_maxFuel`
      and the hover shows the `[shift+E] Add max` line.
- [ ] A nearly full generator is topped up to exactly full, never over; a full generator takes nothing.
- [ ] Range is measured from the **generator**, not the player: a chest beside the generator feeds it
      while the player stands back; a chest beside the *player* but out of the generator's range does not.
- [ ] With `Pull fuel` off, the generator behaves exactly as vanilla did.
- [ ] The startup log reads `Harmony patched 24 game method(s)` including
      `ShieldGenerator.OnAddFuel` and `ShieldGenerator.OnHoverAddFuel`.

## Execution record (2026-10-23)

Tasks 1–3 complete; nothing committed or pushed yet.

- Debug build: `BUILD OK - CraftFromDrawersAndChests 1.0.5 (Debug)`, 94,208-byte DLL.
- `dotnet test`: **262 passed / 0 failed**.
- Shipping signatures re-read with the sanctioned probe:
  `private ShieldGenerator::OnAddFuel(Switch, Humanoid, ItemData) : Boolean` and
  `private ShieldGenerator::OnHoverAddFuel() : String`; `GetFuel()` is private, `m_fuelItems`/`m_maxFuel`/
  `m_add` are public, `m_nview` is private (hence `___m_nview` injection).
- Built DLL (Debug and packaged Release, identical SHA256 `E01592D5…`): ASCII-scannable `ShieldGenerator`
  metadata and `1.0.5` attribute string; `RPC_AddFuel` present as a UTF-16 literal (the RPC name is a
  `#US`-heap string, so an ASCII scan of it is a false negative — scan both encodings).
- Release package: `Release\CraftFromDrawersAndChests-1.0.5.zip`, 65,563 bytes, packaged manifest reads
  `1.0.5`. **Not published** — the owner tests first.
- `RPC_AddFuel` → `SetFuel(GetFuel()+1)` → `RPC_SetFuel` → `ZDO.Set(s_fuel, …)` is the same chain the
  smelter uses, so the feed's per-unit "did the station take it" check reads a settled value.
- **Confirmed in game** by the owner on 2026-10-23, Valheim 1.0.20 (`Modheim`, session at 21:53):
  `Loading [CraftFromDrawersAndChests 1.0.5]`, `Harmony patched 24 game method(s)` including
  `ShieldGenerator.OnAddFuel` and `ShieldGenerator.OnHoverAddFuel`, **0 errors**. The owner confirmed the
  fix works.
- **Released 2026-10-23** at the owner's instruction: tag `v1.0.5` (annotated, `8fb09c8`) on `141315e`,
  pushed; GitHub release published as <https://github.com/ghstwhl/CraftFromDrawersAndChests/releases/tag/v1.0.5>
  — API-verified `draft=false`, `prerelease=false`, asset `CraftFromDrawersAndChests-1.0.5.zip`,
  65,563 bytes, digest equal to the local zip. Record: `release-1.0.5-notes.md`; body:
  `release-1.0.5-body.md`. **Thunderstore not uploaded** (state unknown here).

