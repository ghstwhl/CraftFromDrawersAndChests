# Task 15a: Station pulls (Inventory-variant refactor + four station patches)

Split out of Task 15. `fillAllModKey` and the console command are **Task 15b** — not here.

## Oracle — read this, do not decompile anything

`\.references\Mods\CraftFromContainers\BepInExPlugin.cs` — CraftFromContainers **4.0.7**, 75 KB of plain
C# text. Read it with your normal file tools; it is cheap. **Do not run `ilspycmd`, and do not decompile
any assembly.** Two recent workers lost an hour to unbounded decompile/probe loops, and repeated
dispatches of this task produced no work at all, most likely because earlier briefs sent them looking
for a CFC source that was not where it claimed to be. If you need a Valheim field or type fact, use the
sanctioned probe (a targeted reflection scan, verified at 1.3 s):

```
powershell -NoProfile -ExecutionPolicy Bypass -File .superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\tools\cecil.ps1 -Assembly .references\Valheim\1.0.16\assembly_valheim_publicized.dll -Method <Name> [-Fields] [-Definitions] [-Callers]
```

## The engine already exists — do not rebuild it

`.superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\task-15-scope.md` records this with
file:line evidence. In short:

- `StorageRegistry.Collect(Vec3 origin, FeatureFlags)` (`Storage/StorageRegistry.cs:35`) and
  `PullEngine.Collect` (`Pull/PullEngine.cs:55`) **already take an origin**.
- `IStorageSource.Take(string, int, Inventory destination)` — the destination is **already an `Inventory`**.
- `PullEngine.Plan(requests, sources, Vec3 origin, flags, allowUpgraderResources)` (`PullEngine.cs:238`) —
  explicit origin, no player.
- `BuildShortfallRequests(…, Inventory inventory)` (`PullEngine.cs:108`), `HeldCounts`, `CountHeld`,
  `InventoryStacks`, `DestinationEmptySlots` — already `Inventory`-generic.
- `Logic/DestinationCapacity.cs`, `Logic/PullRunner.cs`, `Logic/DrawerTake.cs` — pure, destination-agnostic.

**Do not write a new planner, a capacity rule, a take-sequencing loop, or a second shortfall calculation.**

## What to build

1. **The seam.** Four player-bound wrappers each already compute `player != null ? player.GetInventory() : null`
   on their first line: `PlanFor` (`PullEngine.cs:220`), `Execute` (`:266`), `PullToPlayer` (`:324`),
   `DestinationAllows` (`:373`). Add `Inventory`-taking variants and make the player-facing signatures
   **delegate** to them — Tasks 13/14/14b depend on those signatures, so they must keep working exactly as
   they do now.

2. **`Patches/StationPatches.cs`** — Fireplace fuel, Smelter ore, CookingStation food, Turret ammunition.
   Each calls the new variants with **the station's transform as the origin** and **the station's inventory
   as the destination**. For a station, the "held" count is what the station already contains, so the
   shortfall is what it still needs. Gate each on its own flag (`PullFuel`, `PullOre`, `PullCooking`) read
   from `PluginConfig.Flags` — never a constructed `FeatureFlags`.

3. **Audit the uncommitted draft** rather than trusting it: `CraftFromDrawersAndChests/Logic/StationFeedRules.cs`
   is untracked, and `PullEngine.cs`/`PullKeyPatches.cs` have uncommitted edits. Keep what is correct,
   fix what is not, and say what you changed. `ModInfo.cs`'s new `ConsoleCommand`/`ConsoleCommandAlias`
   constants are for Task 15b — keep them and commit them.

4. Register `Patches/StationPatches.cs` in the legacy csproj's explicit `<Compile>` items (check whether
   `Logic/StationFeedRules.cs` is already registered).

## Open question, resolve from CFC — do not guess

`ConfigModel` binds `PullFuel`, `PullOre` and `PullCooking` but **no turret key** (I checked the whole
constant list). Verify in CFC 4.0.7 whether turret pulling is flag-gated at all and on what, and mirror it.
**Do not invent a config key** — the key set is a user-facing contract bound in one place; if one is
genuinely needed, report it as a finding for the controller instead.

## Constraints

- `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`.
- `Logic/**` must not reference `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`. Put anything pure into `Logic/`
  with tests — that is where this project's defects have been caught.
- **Never write a `Container`'s inventory or a drawer's ZDO `"Amount"`.** Chest removal only via
  `MultiUserChestBridge.RemoveFromChest`; drawer only via `ForceRemove`. (Adding to a *station's* inventory
  is a different thing — do it the way vanilla/CFC does and say what you did.)
- Storage is keyed by **prefab name**. `-1` (`ChestTake.Deferred`) is never a count, never summed, never
  retried. **Items must never end up on the ground, and nothing may be pulled that cannot be delivered** —
  the capacity check must be against the station's inventory.
- Leave no scratch or inspection scripts in the tree. No Nexus artefacts; no other mod's identity,
  feature description, path or URL anywhere in this repository.
- `MSB3274` is expected — do not fix or suppress it. **Do not push to any remote.**

## Verify and report

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 194 or more)
3. **Skip in-game verification** — write those steps as `AUTHOR-VERIFY` in your report.
4. Commit, then write `.superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\task-15a-report.md`:
   what you found on audit and what you completed or corrected; the `Inventory`-variant refactor and how the
   player-facing signatures still work; per-station triggers and guards with the CFC lines you mirrored;
   how capacity is checked against a station inventory; **your turret-flag conclusion and its evidence**;
   what went into `Logic/` with tests; build and test result lines; files changed; `AUTHOR-VERIFY`;
   self-review; concerns.

Then reply with ONLY (under 15 lines): **Status** · commit · one-line build/test summary · concerns ·
the report path. Do not narrate your work in the reply.
