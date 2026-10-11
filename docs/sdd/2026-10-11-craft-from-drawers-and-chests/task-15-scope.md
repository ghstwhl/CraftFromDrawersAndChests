# Task 15 — verified scope (controller, 2026-10-11)

Three dispatches of this task produced no work at all. Before retrying again, the scope was verified
against the code rather than inferred from the plan, and the plan's framing was wrong: this is **not**
"port CFC's station feeding". The engine already exists and is already parameterised the way this task
needs.

## Already available from earlier tasks — do not rebuild any of it

| Piece | Evidence |
|---|---|
| `StorageRegistry.Collect(Vec3 origin, FeatureFlags flags)` | `Storage/StorageRegistry.cs:35` — **already takes an origin**, so "range from the station" is a caller change |
| `PullEngine.Collect(Vec3 origin, FeatureFlags flags)` | `Pull/PullEngine.cs:55` — same, with the per-frame source cache |
| `IStorageSource.Take(string itemName, int amount, Inventory destination)` | `Storage/IStorageSource.cs` — the destination is **already an `Inventory`**, so a station's inventory needs no interface change |
| `PullRunner.Run(lines, take, warn)` | pure take sequencing, destination-agnostic (T13) |
| `PullEngine.Plan(requests, sources, Vec3 origin, flags, allowUpgraderResources)` | `Pull/PullEngine.cs:238` — **explicit origin, no player** |
| `PullEngine.BuildShortfallRequests(requirements, qualityLevel, multiplier, Inventory inventory)` | `Pull/PullEngine.cs:108` — takes an **`Inventory`**, not a player |
| `HeldCounts(Inventory,…)`, `CountHeld(Inventory, string)`, `InventoryStacks(Inventory)`, `DestinationEmptySlots(Inventory)` | all already `Inventory`-generic |
| `Logic/DestinationCapacity.cs`, `Logic/DrawerTake.cs` | pure, operate on inventory stacks |

## The actual work

1. **The seam — four player-bound wrappers.** Each already computes
   `player != null ? player.GetInventory() : null` on its first line, so adding `Inventory`-taking
   variants is mechanical, not a redesign:
   - `PlanFor(…, Player player, …, out sources)` — `Pull/PullEngine.cs:220` (`OriginOf(player)`)
   - `Execute(plan, sources, Player player)` — `Pull/PullEngine.cs:266`
   - `PullToPlayer(…, Player player, …)` — `Pull/PullEngine.cs:324`
   - `DestinationAllows(…, Player player)` — `Pull/PullEngine.cs:373`
2. **Four game-facing patches** (Fireplace, Smelter, CookingStation, Turret) that call those variants with
   **the station's transform as the origin** and **the station's inventory as the destination**.
   For a station, the "held" count is what the station already contains, so the shortfall is what it
   still needs — the same shape `BuildShortfallRequests` already implements.
3. **`fillAllModKey`** (fill-to-max) and **`TerminalPatch`** (console command reporting in-range source
   count and drawer-API state).

CFC remains the oracle for the *game-facing* details only — trigger conditions, guards,
`FuelDisallowTypes`/`OreDisallowTypes` matching, `fillAllModKey` semantics, messages, and the console
command's shape. Not for the engine, which already exists.
