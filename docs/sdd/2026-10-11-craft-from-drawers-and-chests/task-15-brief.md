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

