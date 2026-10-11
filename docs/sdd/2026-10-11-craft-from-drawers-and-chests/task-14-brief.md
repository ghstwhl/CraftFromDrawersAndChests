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

