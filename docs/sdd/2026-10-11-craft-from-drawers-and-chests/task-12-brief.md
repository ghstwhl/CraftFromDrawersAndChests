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

