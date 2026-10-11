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

