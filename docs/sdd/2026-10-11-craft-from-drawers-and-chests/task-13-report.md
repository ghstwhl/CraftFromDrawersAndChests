# Task 13 — Crafting and building pull through storage — Report

**Status:** DONE_WITH_CONCERNS (in-game step not runnable here; one brief instruction had to be corrected to preserve the all-or-nothing rule)
**Branch:** `feature/craft-from-drawers-and-chests`
**Commit:** `cc2d0e1` — *feat: craft and build from chests and drawers* (parent `1786216`)
**Files committed (5, +703):**
- `CraftFromDrawersAndChests/Pull/PullEngine.cs` (new) — plan + execute
- `CraftFromDrawersAndChests/Patches/CraftPatches.cs` (new) — the three patch sites
- `CraftFromDrawersAndChests/Logic/PullMath.cs` (new) — the one pure decision
- `Tests/Logic/PullMathTests.cs` (new) — 5 tests
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` (modified: +3 `<Compile>`)

## 1. Ground truth re-derived, not assumed

Every patch target and API shape was re-derived from `.references/Valheim/1.0.16/assembly_valheim_publicized.dll` with Mono.Cecil, run in a child `powershell` process (nothing locked).

- `Player.HaveRequirementItems(Recipe piece, bool discover, int qualityLevel, int amount)` — the crafting material check. `Player.HaveRequirements(Recipe,…)` gates **station level + crafting station + recipe DLC before calling it**, so a postfix here cannot bypass the station. Confirmed by decompiling both.
- `Player.HaveRequirements(Piece piece, Player.RequirementMode mode)` — building. `RequirementMode`: `CanBuild = 0`, `IsKnown = 1`, `CanAlmostBuild = 2`. Vanilla checks the **crafting station and the piece's DLC inside this method, before its material loop** (station gate: `Known` for IsKnown/CanAlmostBuild; `HaveBuildStationInRange(...) || ZoneSystem.GetGlobalKey(GlobalKeys.NoWorkbench)` for CanBuild). Callers: `Hud.UpdatePieceBuildStatus` (CanBuild), `BuildUiPieceButton.UpdateRequirements` (CanBuild), `PieceTable.UpdateAvailable` (CanAlmostBuild), `Player.UpdatePlacement` (CanBuild).
- `Player.ConsumeResources(Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier)` is the **single consume chokepoint** for both paths: called only from `Player.UpdatePlacement` (building) and `InventoryGui.DoCrafting` (crafting).
- Vanilla's amount arithmetic is `requirement.GetAmount(qualityLevel) * amount` and the test is `CountItems(sharedName, -1, true) >= that`; `PlanFor`/`BuildRequests` use the identical formula.
- `CraftingStation.m_upgrader` is a `bool`; `Player.GetCurrentCraftingStation()` exists; `Player.m_knownStations` is a public `Dictionary<string,int>`; `DLCMan.instance` is a static property; `GlobalKeys` 27 is `NoWorkbench`.

## 2. How `PlanFor` obtains the prefab name, and why it is the right identifier

`PullEngine.PrefabName(requirement)` returns **`requirement.m_resItem.gameObject.name`** — the requirement item's prefab/GameObject name, e.g. `"Wood"` — and both `PullRequest.Prefab` and `PullRequest.ItemName` are set to it. This is the corrected identifier the dispatch demanded: the brief's `m_resItem.m_itemData.m_shared.m_name` is the *localised* name (`"$item_wood"`).

It is right because **every other layer of this mod already keys storage by prefab name**, and the planner joins on it:
- `IStorageSource.Count(itemName)` documents `itemName` as a prefab name; `VanillaChestSource.Count` matches `item.m_dropPrefab.name` and `DrawerSource.Count` matches the ZDO `"Prefab"` (Ruling 1).
- `StorageStock.ItemName` carries a prefab name, and `PullPlan.Plan` matches a request to a stock by `ItemName` (Ruling 12).
- A dropped/created item's `m_dropPrefab.name` is the prefab GameObject's name, so `m_resItem.gameObject.name` is the same string the sources will match.

Passing `m_shared.m_name` would make every `Count` return 0, `Plan` would report every requirement missing, and nothing would ever pull — the exact no-op the dispatch warned about. A requirement whose item cannot be resolved is skipped, never guessed at.

## 3. `Execute` and `Take`'s three return shapes; "secure everything before consuming"

`Execute(plan, sources, player)` walks `plan.Lines` in order and calls `IStorageSource.Take(itemName, amount, player.GetInventory())` per line, honouring all three shapes:

- **`>= 0`, equal to the line** — secured; continue.
- **`>= 0` but fewer than the line** (a drawer hands over one stack per delivery; a chest may hold less than was counted) — **fail**, stop the walk. Never silently accept a short line.
- **`ChestTake.Deferred` (`-1`)** — treated as **not a count at all**: `taken == ChestTake.Deferred` is checked *before* the `<` comparison so `-1` can never be summed or compared as an amount. It means the removal was routed to another player and the outcome is not knowable here, so it **fails**. Plans are built only from synchronously-servable sources (below), so a deferred answer means a chest changed hands mid-pull; failing is the all-or-nothing response.

`Execute` also returns `false` for a null/infeasible plan (nothing secured by definition) and when the destination inventory is unavailable.

**Secure everything before consuming** is enforced in `CraftPatches.ConsumeResourcesPrefix`: it *plans the whole shortfall first*, and only then moves anything. If `plan.Feasible` is false it returns `false` (vanilla consumes **nothing**). If a line cannot be fully secured it returns `false` (vanilla consumes **nothing**; whatever was already pulled stays in the player's inventory, which is recoverable). Only when every line is secured does it return `true`, letting vanilla consume the now-present materials. This is the design's top anti-goal and `Review Focus 1`.

**Correction 2 applied:** `PullEngine.BuildStocks` skips any `VanillaChestSource` whose `CanTakeSynchronously` is false, **before** planning, so a chest another player has open is never planned against — the executor declines such a chest before asking, rather than after a routed request has already left. `CraftPatches.AnySourceHolds` uses the same `PullEngine.CanServeSynchronously` predicate so the `CanAlmostBuild` answer agrees with what a pull could actually do.

**Correction 4 applied:** the upgrader flag comes from the player's current crafting station (`PullEngine.AtUpgrader(player)` → `GetCurrentCraftingStation().m_upgrader`) and is passed to `PullPlan.Plan`. `false` makes the planner *skip* upgrader requirements (Idols) rather than fail on them, matching vanilla, which does not consume them at a non-upgrader station.

**Correction 5 applied:** every read of configuration is `PluginConfig.Flags` (the shared snapshot). No `FeatureFlags` is constructed anywhere in the new code.

**Correction 6 applied:** the origin is the player's transform position for both crafting and building.

## 4. The one brief instruction I had to correct: shortfall planning

The brief says `PullRequest.Amount = GetAmount(qualityLevel) * multiplier` (the full requirement) and that the consume prefix "pulls into the player inventory first and lets vanilla consume from there". **Planning the full requirement makes the split case silently wrong**, so `CraftPatches` plans only the **shortfall** and passes the player's own holdings into `PullEngine.BuildRequests(..., held)`; `PullMath.Shortfall(required, held) = max(0, required - held)` (the pure, tested piece).

Worked example — player carries 3 Wood, needs 10, storage holds 8:
- Full-requirement planning → infeasible (8 < 10) → the prefix would either consume nothing (a free item) or let vanilla eat the player's 3 (a silent partial fail). Both violate an anti-goal.
- Shortfall planning (this implementation) → request 7 Wood, storage supplies 7, player ends at 10, vanilla consumes 10. Storage loses 7, the player loses their own 3. Correct.

The player-already-has-everything case is the same mechanism: every shortfall is 0, `BuildRequests` emits no requests, the plan is feasible with zero lines, and the prefix returns `true` so vanilla consumes from the inventory. Without the shortfall, that everyday case (crafting from your own pocket, empty nearby storage) would break.

`PullEngine.PlanFor(requirements, qualityLevel, multiplier, origin, flags)` is implemented with exactly the brief's signature and semantics (full requirement, for the "pull the whole recipe" case the ctrl-click feature will want); the crafting/building paths instead use `BuildRequests(..., held)` followed by `Plan(...)` directly.

## 5. Build and tests

- **Build:** `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
  Final lines:
  ```
    CraftFromDrawersAndChests -> C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll
      Built C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll (52,224 bytes)

  BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
  ```
  `MSB3274` (MultiUserChest 4.7.2 vs this project's 4.6.2) printed once, expected, left unfixed.
- **Failing-first evidence:** before implementing, `PullMathTests` failed to compile with `CS0103: The name 'PullMath' does not exist in the current context` (5×).
- **Tests:** `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
  ```
  Passed!  - Failed: 0, Passed: 107, Skipped: 0, Total: 107, Duration: 116 ms - CraftFromDrawersAndChests.Tests.dll (net462)
  ```
  107 = 102 baseline + 5 new `PullMathTests`.

## 6. Constraints and rulings honoured

- `net462`; BepInEx 5.4.2350; Jötunn 2.30.2; HarmonyX via BepInEx's `0Harmony`. `MSB3274` untouched.
- `Logic/**` stays free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn` (`PullMath` is the only addition and uses none). `Pull/` and `Patches/` are game-facing.
- **Never writes** `Container.m_inventory` or a drawer's ZDO `"Amount"`: all removal goes through `IStorageSource.Take` → `MultiUserChestBridge` (chest) or `ForceRemove` (drawer).
- Pulls are all-or-nothing; storage stays prefab-keyed.
- All three new files are explicit legacy-style `<Compile>` items.
- No Nexus-flavoured artefact, no other mod's path/URL introduced. No ctrl-click key handling and no station-feeding added.
- **Ruling 13 honoured:** the prevent/fill-all modifier keys are *not* touched here — they belong to T14/T15. Only `flags.ModEnabled` gates this task's behaviour.
- Nothing pushed (`origin/feature/…` still at `1786216`).

## AUTHOR-VERIFY (brief Step 4 — in-game, not runnable from here)

1. **Split across chest and drawer, player carries none.** Put a recipe's materials split between a chest and a drawer, stand within `Maximum range`, carry none. Open the crafting tab: the recipe must show **available before you craft**. Craft it: it succeeds and the materials leave **both** the chest and the drawer. (Also check the same for a build piece with the hammer.)
2. **Larger than everything in range (Review Focus 1).** Repeat with a requirement larger than everything within range. The recipe must **not** be marked available, and crafting/building it must consume **nothing** (no materials leave any storage, nothing lands on the ground).
3. **Player-contributes-some.** Carry part of the requirement, leave the rest in range. Confirm the craft succeeds and that storage gives up only the shortfall (your own carried items are consumed first).
4. **Craft entirely from your inventory, storage empty/out of range.** Confirm it still crafts (the shortfall is zero; storage must not be required).
5. **Upgrader resources (Idols).** At a normal workbench/forge, confirm Idols are never pulled/consumed. At the upgrader, confirm they are.
6. **Drawers absent.** With Item Drawers uninstalled, confirm chests still work and nothing throws.
7. **Another player owns a chest (Review Focus 3, shared with T11).** With a second player holding a chest open, confirm the pull declines that chest (it is not planned against) and that items still leave storage exactly once.

## Self-review findings

- Confirmed via decompilation that patching `HaveRequirementItems` cannot bypass the station gate, but that patching `HaveRequirements(Piece, mode)` *could* — so the postfix **re-checks the station (known/in-range, incl. `GlobalKeys.NoWorkbench`) and DLC gates** before letting storage answer. Skipping that would have let storage substitute for a crafting station. `IsKnown` is deliberately not augmented (storage must not make an unknown material known).
- `BuildRequests` drops a requirement whose item cannot be resolved rather than guessing an identifier.
- Some code paths genuinely cannot be exercised without the game, and are **not** implied to be covered: the `Piece.Requirement` → request mapping, the Harmony patch sites themselves, `Execute`'s use of a live `Inventory`, and the per-frame source cache. Only the detached arithmetic (`PullMath.Shortfall`) is unit-tested; the planner's own all-or-nothing logic is already covered by T4's tests.
- Added a per-frame cache of `StorageRegistry.Collect` (CFC kept one too): `Hud.UpdatePieceBuildStatus` re-checks every buildable piece each frame and the crafting panel re-checks the selected recipe each frame, so without it a single frame would rescan every tracked container and query Item Drawers once per piece/recipe. The cache is keyed on frame number + exact origin + the exact flags instance, and it is also what guarantees a plan's `SourceId`s (source indices) resolve against the same list in `Execute`.

## Concerns

1. **Returning `false` from the consume prefix skips consumption, and `ConsumeResources` is `void`** — the game creates the crafted item regardless of that return. In the *race* where the materials were secured at the availability check but cannot be pulled at consume time, skipping consumption would yield a free item. The brief explicitly directs "must not let vanilla consume" and accepts that pulled items staying in the inventory is recoverable; I followed it, and the availability check gates the normal path, so this window is between the two checks in the same method call. Worth confirming in game (AUTHOR-VERIFY 2) that the infeasible case consumes nothing **and** does not award the item.
2. **`PlanFor` (the brief's exact signature, full-requirement semantics) is not called by Task 13** — the crafting/building paths need shortfall planning (§4). It is provided for the ctrl-click pull (T14) and later callers. Flagged so the reviewer sees it is intentional, not dead code by oversight.
3. **World-level / quality matching.** Storage counts are keyed on prefab name only (Ruling 1), so a stored item whose `m_worldLevel` differs from the current world level is counted as available, while vanilla's `CountItems(..., matchWorldLevel: true)`/`RemoveItem(...)` may not consume it. This is inherent to the established storage keying (T9/T10/T11), not introduced here, but it is a residual risk for the consume step; the in-game split/upgrade checks above are the place to catch it.
4. **`CanAlmostBuild` augmentation** (for the build menu's availability list) is a judgement call beyond the brief's literal "do I have the materials"; it mirrors CraftFromContainers and is guarded by the same station/DLC re-check. If the reviewer disagrees, it is a self-contained removal.
