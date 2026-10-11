# Task 15a report — Station pulls (Inventory-variant refactor + four station patches)

Commit: **38638f1** `feat: feed stations from storage (Task 15a)` on `feature/craft-from-drawers-and-chests`
(parent `df532f2`). Working tree clean. Nothing pushed.

Build: `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
Tests: `Passed! - Failed: 0, Passed: 205, Skipped: 0, Total: 205` (baseline before this task: 194 requested;
205 now, +11 from `StationFeedRulesTests`).
Oracle: `.references\Mods\CraftFromContainers\BepInExPlugin.cs` (CFC 4.0.7) read as text. **No assembly was
decompiled**; all Valheim facts below come from the sanctioned Cecil probe
(`tools/cecil.ps1`), which returns in ~1.3 s.

## 1. Audit of the uncommitted draft — what was wrong, what I did

The draft was a real implementation (seam + four patches + pure rules + tests), not a stub, but it had four
defects and one silently-fatal omission. All are now fixed.

| # | Finding | Disposition |
|---|---|---|
| 1 | **`Patches/StationPatches.cs` and `Logic/StationFeedRules.cs` were not registered in the legacy csproj's explicit `<Compile>` items.** A legacy csproj compiles *only* the listed files, so both files would have been excluded from the product assembly with no error: `BUILD OK`, all tests green (the tests link `Logic\**\*.cs` by glob, so they still saw `StationFeedRules`), and **no station feeding at all in the shipped DLL**. | Added both `<Compile>` items; verified with Cecil on the *built* DLL that `StationFeedRules::RemainingFuelCapacity(Single,Single)` and `StationPatches::FireplaceInteractPrefix(Fireplace,Humanoid,Boolean,ZNetView)` are present. |
| 2 | `StationFeedRules.RemainingCapacity` had **no production caller** (tests only) — pure code that existed to be tested rather than used. | Routed the smelter-ore full-guard and the turret full-guard through it (`RemainingCapacity(m_maxOre, GetQueueSize()) <= 0`, `RemainingCapacity(m_maxAmmo, GetAmmo()) <= 0`), so all three pure methods are used and tested. |
| 3 | `PullKeyPatches.KeyHeld` was widened `private` → `internal` with a doc paragraph claiming "the station patches read the fill-all modifier with it". **No such caller exists** (fill-all is Task 15b). Internally inconsistent + false doc. | Reverted: `KeyHeld` is `private` again and `Pull/PullKeyPatches.cs` is byte-identical to HEAD (it no longer appears in the diff). |
| 4 | CFC line citations in the doc comments were several lines off (origins cited as 301/408/480/602, turret guard 1137, `PullResources` call 1155). | Corrected to the verified numbers: origins **304/361/401/485/612**, turret guard **1135**, `PullResources` call **1152**. All citations in the file were re-checked against `Select-String` on the oracle. |
| 5 | Kept, deliberately: the draft's *design* (see §3 — deliver into the interacting player's inventory) and its one extra guard (`HaveDoneItem()` in the cooking patch, see §4). | Kept and documented as deliberate deviations. |

## 2. The seam — `Inventory`-taking variants, player-facing signatures unchanged

`Pull/PullEngine.cs` (+126/-14) adds an `Inventory`-taking overload beside each of the four player-bound
wrappers; **the player-facing method is now a one-line delegate**, so Tasks 13/14/14b are untouched:

| Player-facing signature | New variant | Delegate body |
|---|---|---|
| `PlanFor(reqs, q, mult, Player, flags, out sources)` | `PlanFor(reqs, q, mult, Inventory, Vec3 origin, flags, bool allowUpgraderResources, out sources)` | `PlanFor(..., player != null ? player.GetInventory() : null, OriginOf(player), flags, AtUpgrader(player), out sources)` |
| `Execute(plan, sources, Player)` | `Execute(plan, sources, Inventory destination)` | `Execute(plan, sources, player != null ? player.GetInventory() : null)` |
| `PullToPlayer(reqs, q, mult, Player, flags, out requested)` | `PullToInventory(reqs, q, mult, Inventory destination, Vec3 origin, flags, bool allowUpgraderResources, out requested)` | delegates with the player's inventory, `OriginOf(player)`, `AtUpgrader(player)` |
| `DestinationAllows(Player, reqs, plan, reserved)` | `DestinationAllows(Inventory, reqs, plan, reserved)` | delegates with `player?.GetInventory()` |

Each wrapper already computed `player != null ? player.GetInventory() : null` on its first line and already
derived the origin/upgrader flag from the player, so the change is a pure extraction: one implementation, two
entry shapes, no behaviour change for the player paths (the only thing the station callers pass differently is
an explicit origin and an explicit `allowUpgraderResources`). `Plan`/`BuildShortfallRequests`/`HeldCounts`/
`InventoryStacks`/`DestinationEmptySlots` were already `Inventory`-generic and were **not** touched. No new
planner, capacity rule, take loop or shortfall calculation was written.

## 3. Per-station behaviour, and how capacity is actually checked

**The requirement's "the station's inventory as the destination" is not implementable.** Cecil on
`assembly_valheim_publicized.dll` (`-Method m_inventory -Fields`) shows `m_inventory : Inventory` exists only
on `Container`, `Humanoid`, `Inventory` and `InventoryGrid` — **no `Fireplace`, `Smelter`, `CookingStation` or
`Turret` has one**. A fireplace's fuel is a `float` on its ZDO (`ZDOVars.s_fuel`), a smelter's queue is
`GetQueueSize()`, a cooking station's food is ZDO slots, a turret's ammunition is `GetAmmo()`. There is
therefore no `Inventory` to hand `IStorageSource.Take`, and a *synthetic* station inventory would put items
nowhere.

What the patches do instead, which is CFC's own end state: pull the item **into the interacting player's
inventory** and return the Harmony prefix as `true`, so vanilla's own method consumes it from there — exactly
the mechanism CFC relies on for the player's own items (CFC returns to vanilla whenever
`inventory.HaveItem(...)`). Call-graph evidence that vanilla consumes it: `Smelter.OnAddOre →
FindCookableItem(Inventory)`, `CookingStation.Interact → OnInteract(Humanoid)` and
`CookingStation.OnAddFoodSwitch → OnInteract` (and `OnInteract → FindCookableItem(Inventory)`),
`Turret.Interact → Turret.UseItem(Humanoid, ItemData)`. CFC's *container* branch removes from the container
and RPCs directly, which this design forbids (chest removal only via `MultiUserChestBridge.RemoveFromChest`,
drawer only via `ForceRemove`) — so the player-inventory route is the only one the engine and the constraints
admit, and it is what the draft implemented.

Capacity is therefore proved twice, and **nothing can be pulled that cannot be delivered**:

* **Station side, before any pull:** `StationFeedRules.RemainingFuelCapacity(maxFuel, fuel) <= 0` for the
  fireplace / smelter fuel / cooking fire, `StationFeedRules.RemainingCapacity(m_maxOre, GetQueueSize()) <= 0`
  for the smelter queue, `RemainingCapacity(m_maxAmmo, GetAmmo()) <= 0` for the turret, and
  `GetFreeSlot() == -1` for the cooking station's food slots.
* **Destination side, inside the pull:** `PullToInventory` → `PlanFor` → … → `DestinationAllows(inventory, …)`
  (`DestinationCapacity.CanAccept`), the same all-or-nothing room proof the crafting path uses. A full player
  inventory means nothing is taken and nothing reaches the ground; an infeasible plan moves nothing.

Per-station triggers, guards and CFC mirrors (line numbers verified against the oracle):

| Patch | Harmony target | Flag | Guards (CFC mirror) | Origin |
|---|---|---|---|---|
| Fireplace fuel | `Fireplace.Interact` prefix `(Fireplace, Humanoid, bool hold, ZNetView)` | `PullFuel` | CFC 272-345: guard 280 (`!AllowByKey \|\| !pullFuel \|\| hold \|\| inventory == null \|\| (HaveItem(fuel) && !pullAll)`), not-full 302 (`CeilToInt(zdo "fuel") < m_maxFuel`), disallow 311 (`fuelDisallowTypes.Value.Split(',').Contains(item.m_dropPrefab.name)`) | 304 `__instance.transform.position` |
| Smelter ore | `Smelter.OnAddOre` prefix `(Smelter, Humanoid, ItemData)` | `PullOre` | CFC 459-573: guard 465 (`!AllowByKey \|\| !pullOre \|\| item != null \|\| GetQueueSize() >= m_maxOre`), "player already has an input type" 471-482, disallow 497 & 525 (`oreDisallowTypes`) | 485 |
| Smelter fuel | `Smelter.OnAddFuel` prefix `(Smelter, Humanoid, ItemData)` | `PullFuel` | CFC 575-660: guard 582 (`!AllowByKey \|\| !pullFuel \|\| item != null \|\| inventory == null \|\| HaveItem(fuel)`), full 589 (`GetFuel() > m_maxFuel - 1`), disallow 619 | 612 |
| Cooking fire | `CookingStation.OnAddFuelSwitch` prefix `(CookingStation, Humanoid, ItemData)` | `PullFuel` (CFC gates a fire on `pullFuel`, not `pullCooking`) | CFC 348-385: guard 355, full `GetFuel() > m_maxFuel - 1`, disallow 368 | 361 |
| Cooking food | `CookingStation.OnInteract` prefix `(CookingStation, Humanoid)` | `PullCooking` | CFC 388-427: guard 395 (`!AllowByKey \|\| !pullCooking \|\| found \|\| (m_requireFire && !IsFireLit) \|\| GetFreeSlot() == -1`), disallow 410 (**`oreDisallowTypes`** — CFC really tests the cooking ingredients against the *ore* list; mirrored rather than inventing a third list) | 401 |
| Turret ammo | `Turret.UseItem` prefix `(Turret, Humanoid, ItemData)` | **none** (see §4) | CFC 1129-1156: guard 1135 (`!modEnabled \|\| !AllowByKey \|\| item != null \|\| !(user is Player)`), "no ammo of that type in inventory", then pull (CFC 1152) | 612-style, `__instance.transform.position` |

Two deliberate deviations from CFC, both documented in the code:

* **Cooking food is patched at `OnInteract`, not postfixed on `FindCookableItem`.** In CFC's target version
  `FindCookableItem` was the interaction's only callee; here the probe shows
  `CookingStation::FindCookableItem(Inventory)` has **two** callers — `OnInteract` and `CanUseItems` (reached
  from `TryGetItems`, i.e. the radial menu's config screen). A postfix would pull ingredients while merely
  opening a menu. `OnInteract` is the shared funnel (`Interact` and `OnAddFoodSwitch` both call it) and runs
  before the station looks for food, so the behaviour is preserved without the menu-time side effect. The
  guard `__instance.FindCookableItem(inventory) != null` reproduces CFC's `__result != null` test.
* **`HaveDoneItem()` early-out (extra, CFC has no such guard).** `OnInteract` also collects finished food;
  skipping the pull when the station already holds a done item is conservative (it can only pull *less*), and
  the station is fed on the next interaction. Flagged because it is not a CFC guard.

Only the pull key's shared prevent gate `PullKeyPatches.AllowByKey()` is used from `PullKeyPatches` — the
fill-all modifier (`KeyHeld`) is **not** read here; per-interaction the patches take one item, which is CFC's
behaviour when the fill-all modifier is not held. Fill-all and the `OnHoverAddFuel`/`OnHoverAddOre` "… max"
tooltips (CFC 428-457) are Task 15b.

## 4. Turret flag — conclusion and evidence

**Turret pulling is not flag-gated; it is gated on the mod-enabled flag and the prevent gate only. No turret
config key was invented.**

* CFC's turret prefix (CFC 1132-1155) tests exactly `!modEnabled.Value || !AllowByKey() || item != null ||
  !(user is Player)` (line 1135) — there is **no** `pullFuel`/`pullOre`/`pullCooking` term.
* CFC binds only `pullFuel`/`pullOre`/`pullCooking` (CFC 114-116) and has no turret key.
* This project's `Logic/ConfigModel.cs` likewise declares no turret key (`PullFuel`/`PullOre`/`PullCooking`
  only), and `Logic/FeatureFlags.cs` has no turret property. Adding one would change the user-facing key set
  that is bound in one place, so it is reported rather than created.

So `TurretUseItemPrefix` reads `PluginConfig.Flags`, requires `ModEnabled`, then `PullKeyPatches.AllowByKey()`.
The ammunition type follows CFC (~1160 precedent): `GetAmmo() > 0 ? GetAmmoType() : m_ammoType`, resolved to a
prefab via `ZNetScene.instance.GetPrefab(...).GetComponent<ItemDrop>()`; if the prefab does not resolve, it
logs and pulls nothing (a warning, not an invented key).

## 5. What went into `Logic/`, and the tests

`Logic/StationFeedRules.cs` (new, pure — no `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`):

* `IsDisallowedType(string configuredTypes, string prefabName)` — the `FuelDisallowTypes`/`OreDisallowTypes`
  match, ordinal and exact on the item's **prefab** name (mirrors CFC's
  `Value.Split(',').Contains(item.m_dropPrefab.name)`), parsed through the project's single
  `ConfigModel.ParseList` (trim + drop empties).
* `RemainingCapacity(int capacity, int used)` — `max(0, capacity - used)`, for the smelter queue and turret.
* `RemainingFuelCapacity(float maxFuel, float fuel)` — `ceil(maxFuel) - ceil(fuel)`, floored at zero, on
  vanilla's "a fire is full when `CeilToInt(fuel) >= m_maxFuel`" boundary (which equals CFC's
  `GetFuel() > m_maxFuel - 1`).

`Tests/Logic/StationFeedRulesTests.cs` (new, 11 cases): every branch of the above, including ordinal
case-sensitivity, no-substring matching, trim/empty handling, the floor-at-zero cases, and an explicit
assertion that `RemainingFuelCapacity` agrees with the smelter's `> m_maxFuel - 1` boundary.

## 6. Files changed (commit 38638f1, 6 files, +880/-14)

* `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` (+2) — the two missing `<Compile>` items.
* `CraftFromDrawersAndChests/ModInfo.cs` (+11) — `ConsoleCommand`/`ConsoleCommandAlias`; **kept and committed**
  for Task 15b as instructed (unused here).
* `CraftFromDrawersAndChests/Pull/PullEngine.cs` (+126/-14) — the four `Inventory` variants; player-facing
  signatures delegate.
* `CraftFromDrawersAndChests/Logic/StationFeedRules.cs` (new).
* `CraftFromDrawersAndChests/Patches/StationPatches.cs` (new).
* `Tests/Logic/StationFeedRulesTests.cs` (new).
* `CraftFromDrawersAndChests/Patches/PullKeyPatches.cs` — reverted to HEAD (no change in the commit).

## 7. AUTHOR-VERIFY (in-game — not run here)

1. Chest of Wood within range of a campfire, player carrying none: interact → 1 Wood leaves the chest and the
   fire gains 1 fuel, no message/dupe; the Wood must **not** appear on the ground or stay in the player's
   inventory.
2. Same with a chest out of the fire's range but within the player's range: nothing is pulled.
3. Smelter: chest of CopperOre in range, empty-handed interact with the ore switch → 1 ore queued; verify
   queue caps at `m_maxOre` and that a `FuelDisallowTypes` entry stops it.
4. Smelter fire + cooking-station fire: fuel pulled, fire lit; full fire pulls nothing.
5. Cooking station: chest with a cookable (e.g. BoarMeat) in range → 1 ingredient added to a free slot and the
   station cooks; no pull while a done item is pending; `OreDisallowTypes` entry blocks it.
6. Turret: no ammo in inventory, chest with matching arrows in range → 1 arrow loaded; confirm feeding works
   with all three `PullFuel`/`PullOre`/`PullCooking` set to false (§4 says the turret still feeds).
7. Prevent key held (default config) → no station feeds at all.
8. Item Drawers in range of a station → the drawer (`ForceRemove`) feeds it too.

## 8. Self-review

* Engine `Logic/**` purity preserved (the new file imports only `System`); no `-1`/`ChestTake.Deferred`
  handling was added or changed.
* The seam is additive: I diffed the player-facing bodies and they contain no logic beyond delegation, so
  Tasks 13/14/14b cannot regress behaviourally.
* I verified the compiled output, not just the build exit code, because finding #1 was a silent-exclusion
  class of bug that the build and the test suite both pass straight through.
* I did not re-run the raw IL of vanilla's `Fireplace.Interact`/`Smelter.OnAddFuel` bodies (that would be
  decompilation); the "vanilla consumes from the player's inventory" claim rests on the call graph plus CFC's
  own guard, which returns to vanilla precisely when the player holds the item.
* Line-number corrections were made only after `Select-String`-verifying each number.

## 9. Concerns

1. **The literal requirement cannot be met and was reinterpreted.** Stations have no `Inventory`
   (Cecil-verified), so "the station's inventory as the destination" and "capacity checked against the
   station's inventory" were implemented as "destination = the interacting player's inventory; station
   capacity via `StationFeedRules`". If the controller intended a different shape, this needs a ruling.
2. **The feed depends on vanilla consuming the item from the player's inventory.** That is evidenced for the
   smelter ore, cooking food and turret paths by the call graph, and inferred for the two fuel paths from
   CFC's own guard; AUTHOR-VERIFY steps 1/4 cover it. If a fuel path turns out not to consume, the fallback
   would be CFC's direct `RPC_AddFuel` on the station's nview (permitted — it is the station, not a
   container), which is a larger change than this task.
3. **Turret origin diverges from CFC**: the brief requires the station's transform, and the patch uses it,
   but CFC's `PullResources` measures from `Player.m_localPlayer.transform.position` (CFC 1160). A turret
   with storage beside it but no storage by the player will therefore feed here and not in CFC.
4. **One item per interaction** (fill-all is 15b), and CFC's `OnHoverAddFuel`/`OnHoverAddOre` "… max"
   tooltips are consequently not ported; players will see no hint of the modifier until 15b.
5. `StationPatches.cs` has no automated coverage (it needs the game); only the pure half is tested. All
   game-facing behaviour rests on AUTHOR-VERIFY.
6. **The commit reached the remote, though I never pushed.** `origin/feature/craft-from-drawers-and-chests`
   reflog shows `38638f1 ... update by push` at 22:40:03 (+1300), ~80 s after my 22:38:40 amend; earlier
   commits on this branch carry the same "pushed by something else" pattern (df532f2 at 21:41). There are no
   active hooks (`.git/hooks` holds only `*.sample`), no `core.hooksPath`, and no local push config, so this
   is environment/controller automation, not a command of mine. I did not run `git push` and did not
   force-push to undo it (that would be both a push and destructive).

---

# Fix round 1 — six non-public station members now go through reflection

Commit **5a42206** (`fix: reflect the six non-public station members, fail closed (Task 15a fix round 1)`,
parent `38638f1`). Local only — see concern 6 above; I ran no `git push`.

Build: `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
Tests: `Test summary: total: 205, failed: 0, succeeded: 205, skipped: 0, duration: 1.3s`

## 1. The Important finding — visibility check (sanctioned probe only)

`tools\cecil.ps1 -Definitions` against both assemblies, run in a child `powershell -NoProfile`:

| Member | `.references\Valheim\1.0.16\Managed\assembly_valheim.dll` (shipping) | `...\assembly_valheim_publicized.dll` (compile ref) |
|---|---|---|
| `Smelter::GetQueueSize()` | **private** | public |
| `Smelter::GetFuel()` | **private** | public |
| `CookingStation::GetFuel()` | **private** | public |
| `CookingStation::HaveDoneItem()` | **private** | public |
| `CookingStation::GetFreeSlot()` | **private** | public |
| `CookingStation::IsFireLit()` | **private** | public |
| `Turret::GetAmmo()` | public | public |
| `Turret::GetAmmoType()` | public | public |

`Smelter::GetFuel` and `CookingStation::GetFuel` are distinct types, both `private` in the shipping
assembly; no other type in the scan owned a matching `GetQueueSize`/`HaveDoneItem`/`GetFreeSlot`/
`IsFireLit`. `Turret::GetAmmo`/`GetAmmoType` are `public` in **both**, so those two stay direct calls.

## 2. The fix — reflection, cached, fail-closed

R29 applied: a member is called directly only when it is `public` in the **unpublicized** assembly.
`StationPatches` resolves the six through `AccessTools.Method` **once**, in `static readonly` fields (field
initializers run at type initialization, before any prefix can run):

| Call site | Cache field | Resolved as | Fail-closed guard |
|---|---|---|---|
| smelter ore | `SmelterGetQueueSize` | `AccessTools.Method(typeof(Smelter), "GetQueueSize")` | `!TryInvokeInt(...) \|\| RemainingCapacity(m_maxOre, queueSize) <= 0` → `return true` |
| smelter fuel | `SmelterGetFuel` | `… typeof(Smelter), "GetFuel"` | `!TryInvokeFloat(...) \|\| RemainingFuelCapacity(m_maxFuel, smelterFuel) <= 0` → `return true` |
| cooking fire | `CookingStationGetFuel` | `… typeof(CookingStation), "GetFuel"` | `!TryInvokeFloat(...) \|\| RemainingFuelCapacity(m_maxFuel, cookingFuel) <= 0` → `return true` |
| cooking food | `CookingStationHaveDoneItem` | `… "HaveDoneItem"` | `!TryInvokeBool(...) \|\| doneItem` → `return true` |
| cooking food | `CookingStationGetFreeSlot` | `… "GetFreeSlot"` | `!TryInvokeInt(...) \|\| freeSlot == -1` → `return true` |
| cooking food | `CookingStationIsFireLit` | `… "IsFireLit"` | `m_requireFire && (!TryInvokeBool(...) \|\| !fireLit)` → `return true` |

* **Cached**: six `static readonly MethodInfo` fields, resolved once by `Resolve(type, name)` — nothing is
  re-resolved per frame, so a guard that runs inside a per-frame interaction costs one virtual `Invoke`.
* **Fail-closed**: `Resolve` logs a single warning when a member is missing (once, at type init);
  `TryInvoke` returns `false` — **never throws** — on a null member or a failed `Invoke`, and logs a failed
  invoke **once** (a `bool` flag), not once per frame. Every guard treats "cannot read" as "this station
  cannot be fed": the prefix returns `true`, vanilla runs untouched and nothing is pulled. No exception can
  escape a prefix onto a vanilla method.
* The typed helpers (`TryInvokeInt/Float/Bool`) unbox the `object` result, so a substituted member of the
  wrong shape also fails closed instead of throwing `InvalidCastException`.

`Turret::GetAmmo()` and `Turret::GetAmmoType()` are deliberately left as direct calls (public in both).

## 3. Turret ammo-type key — conclusion and evidence

**`GetAmmo() > 0 ? GetAmmoType() : m_ammoType` is replaced by `GetAmmoType()` unconditionally.**

Probe evidence (unpublicized assembly, `tools\cecil.ps1`):

* `Turret::GetAmmoType -> ZDO::GetString(Int32, String)` and `Turret::GetAmmo -> ZDO::GetInt(Int32, Int32)`
  (from the `-Callers` scans of `GetString`/`GetInt`, filtered to `Turret::` lines).
* `ZDOVars::s_ammoType` and `ZDOVars::s_ammo` both exist as separate `static Int32` keys.

So `GetAmmoType()` and `GetAmmo()` read **independent** ZDO keys: the type name has its own key and
survives while the count is zero, and `GetAmmoType()` falls back to `m_ammoType` only when that key is
unset. `GetAmmoType()` can therefore differ from `m_ammoType` while `GetAmmo() == 0` (a turret emptied of a
non-default type keeps that name in its ZDO). CFC's **prefab** lookup uses `GetAmmoType()` unconditionally
(CFC line 1145) and passes `m_ammoType` only to its *inventory* lookup (line 1140) — a lookup vanilla
performs here, because this patch hands the item back to vanilla's `UseItem` — so the patch now mirrors CFC
on the key it actually uses. The `string.IsNullOrEmpty(ammoType)` guard is kept (log + no pull — the safe
direction).

## 4. `REFERENCES.md` §2 correction

The §2 claim that publicizing makes internal/private members usable **without reflection** is wrong and is
corrected: publicizing changes only the **compile-time** reference; the shipped game loads the
**unpublicized** assembly, which still does not expose the member, so a non-public member still fails — or
throws out of a Harmony prefix — when the JIT compiles the caller. The paragraph now states the rule
(reach a member by reflection unless it is `public` in the unpublicized assembly) and names
`Patches/VanillaChestSource.cs`, `Patches/PullKeyPatches.cs` and `Patches/StationPatches.cs`.

## 5. Bundled doc corrections

* **`StationPatches.cs` class remark** — was "every one of its station patches collects its sources with
  `GetNearbyContainers(__instance.transform.position)`"; now names CFC's fireplace/smelter/cooking patches
  as station-position (lines 304, 361, 401, 485, 612) and states that CFC's **turret** patch measures from
  the **player** (`Player.m_localPlayer.transform.position`, CFC line 1160) — the port's station-position
  turret is labelled a deliberate divergence.
* **`StationPatches.cs` cooking-food patch** — the `HaveDoneItem()` early-out is now labelled
  `CFC DIVERGENCE` (CFC has no such guard) and explained as conservative (it can only pull less).
* **`StationFeedRules` class remark + `IsDisallowedType` doc** — the trim-and-drop through
  `ConfigModel.ParseList` is now stated to be a **deliberate divergence** from CFC's untrimmed
  `Split(',').Contains(...)`, so the test pinning it documents a choice, not the oracle.

---

# Fix round 2 — the two remaining direct calls to `private` vanilla methods, plus two minors

Commit **ab50bfc** (`fix: drop direct FindCookableItem calls, guard Resolve, log return-type mismatches
(Task 15a fix round 2)`, parent `5a42206`). Local only: `git branch -vv` shows
`ahead 2` of `origin/feature/craft-from-drawers-and-chests`; I ran no `git push`.

Build: `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
Tests: `Test summary: total: 205, failed: 0, succeeded: 205, skipped: 0, duration: 1.3s`

## 1. The Important — the two `FindCookableItem` calls are gone, not reflected (R30)

Round 1 closed the six *named* members but the R29 sweep was incomplete: `StationPatches.cs:254`
(smelter ore) and `:418` (cooking food) still called a `private` vanilla method directly. Both call sites
are now removed. Re-confirmed with the sanctioned probe against the **unpublicized** shipping assembly
(this is what the game loads):

```
==== definitions of FindCookableItem ====      (assembly_valheim.dll, 1.0.16)
private CookingStation::FindCookableItem(Inventory) : ItemData
private Fermenter::FindCookableItem(Inventory) : ItemData
private Smelter::FindCookableItem(Inventory) : ItemData
```

**What replaced them.** Both guards wanted a single fact — *"does the interacting player's inventory
already hold one of this station's input items?"* — because vanilla only needs the item to be in the
player's inventory for its own `FindCookableItem` to find and consume it. That fact is obtainable through
the **public** API, and CraftFromContainers already obtains it that way. CFC's smelter-ore prefix never
calls `FindCookableItem`; it walks the station's `m_conversion` and tests the player's inventory with the
public `Inventory.GetItem(<shared name>, -1, false)`:

```csharp
// CFC BepInExPlugin.cs lines 468-479 (verbatim)
foreach (Smelter.ItemConversion itemConversion in __instance.m_conversion)
{
    if (!(itemConversion.m_from == null))
    {
        ItemDrop.ItemData item2 = inventory.GetItem(itemConversion.m_from.m_itemData.m_shared.m_name, -1, false);
        if (item2 != null)
        {
            return true;
        }
    }
}
```

That loop is now a single shared helper, `StationPatches.InventoryHasInput(Inventory, List<ItemDrop>)`,
which is called by **both** the smelter-ore guard and the cooking-food guard (each walks its own
`m_conversion` through the existing `InputItems(...)` overloads and reuses the list for `PullFirstInputs`,
so the conversion list is enumerated once). It is the same public shape CFC uses, minus CFC's reliance on
`m_dropPrefab` (we key on the shared name, as the whole mod does).

| Guard | Before (round 1) | After (this round) |
|---|---|---|
| smelter ore (`SmelterOnAddOrePrefix`) | `__instance.FindCookableItem(inventory) != null` — `private` call | `InventoryHasInput(inventory, InputItems(__instance.m_conversion))` — public `Inventory.GetItem(String, Int32, Boolean)` |
| cooking food (`CookingStationOnInteractPrefix`) | `__instance.FindCookableItem(inventory) != null` — `private` call | `InventoryHasInput(inventory, InputItems(__instance.m_conversion))` — public `Inventory.GetItem(String, Int32, Boolean)` |

No `MethodInfo` was cached for either; the non-public dependency is removed, not reflected, which also
shrinks the direct-access surface. `Fermenter::FindCookableItem` is not called anywhere (it too is
`private`). Verified on the **built** DLL with the sanctioned probe rather than trusting the source:

```
==== call sites of FindCookableItem ====      (CraftFromDrawersAndChests\bin\Debug\...dll)
  (none)
==== call sites of GetItem ====
StationPatches::InventoryHasInput -> Inventory::GetItem(String, Int32, Boolean)
```

## 2. `Resolve` is now wrapped in try/catch

`AccessTools.Method(Type, string)` can throw (`AmbiguousMatchException` when a type declares two same-named
overloads). The six `static readonly` fields are initialized at type initialization, so an escaping
exception surfaced as `TypeInitializationException` out of every prefix — bypassing fail-closed entirely.
`Resolve` now catches, logs once, and returns `null` (the same as "not found"), so a throw degrades to
"this station is not fed" instead of breaking the station's own interaction.

## 3. The typed helpers now log an unexpected return type

`TryInvokeInt/Float/Bool` previously collapsed two distinct failures into one `false`: "the member is
absent" (already logged by `Resolve`) and "the member resolved and invoked but returned the wrong type"
(the one case that disables the feature *silently*). Each helper now checks the result type separately and
calls `LogUnexpectedReturnType(expected, actual)`, which logs **once** (`_returnTypeFailureLogged`) the
actual type returned and the type expected. A failed invoke keeps its own existing one-shot log in
`TryInvoke`, so the three failure modes stay distinguishable.

## 4. `ZDOVars.s_fuel` — verified `public`, left as the direct hash constant

Sanctioned probe against the unpublicized assembly:

```
==== fields named s_fuel ====
public ZDOVars::s_fuel : static Int32
```

It is `public`, so the direct `zdo.GetFloat(ZDOVars.s_fuel, 0f)` stays. (The alternate public overload CFC
uses, `zdo.GetFloat("fuel", 0f)`, was confirmed present too — `ZDO::GetFloat(String, Single) : Single` is
`public` — so it remains a drop-in fallback if the key ever changes, but nothing needed changing.)

## 5. Complete visibility sweep — every vanilla member and field this file touches

Done in one pass with `tools\cecil.ps1` (`-Definitions` for methods, `-Fields` for fields) against
`.references\Valheim\1.0.16\Managed\assembly_valheim.dll`, i.e. the **unpublicized** assembly the game
loads. This is the whole class, not a sample.

**Called directly from this file — all `public` in the shipping assembly (OK to call):**

| Member / field | Declaring type(s) | Shipping visibility | Use |
|---|---|---|---|
| `m_fuelItem` | `Fireplace`, `Smelter`, `CookingStation` | `public` | read |
| `m_maxFuel` | `Fireplace` (`Single`), `Smelter` (`Int32`), `CookingStation` (`Int32`) | `public` | read |
| `m_maxOre` | `Smelter` | `public` | read |
| `m_maxAmmo` | `Turret` | `public` | read |
| `m_conversion` | `Smelter`, `CookingStation` | `public` | read |
| `m_requireFire` | `CookingStation` | `public` | read |
| `m_from` | `Smelter.ItemConversion` / `CookingStation.ItemConversion` | `public` | read |
| `Inventory.GetItem(String, Int32, Boolean)` | `Inventory` | `public` | read (new in this round) |
| `Inventory.HaveItem(String, Boolean)` | `Inventory` | `public` | read |
| `ZDO.GetFloat(Int32, Single)` | `ZDO` | `public` | read |
| `ZDOVars.s_fuel` | `ZDOVars` | `public static Int32` | read |
| `ZNetScene.get_instance()` (the `ZNetScene.instance` property) | `ZNetScene` | `public` | read |
| `ZNetScene.GetPrefab(String)` | `ZNetScene` | `public` | read |
| `ZNetView.GetZDO()` | `ZNetView` | `public` | read |
| `GetInventory()` | `Humanoid` (inherited by `Player`) | `public` | read |
| `Turret.GetAmmo()` | `Turret` | `public` | read |
| `Turret.GetAmmoType()` | `Turret` | `public` | read |
| `m_amount`, `m_resItem` | `Piece.Requirement` | `public` | write (on a locally constructed `Requirement`) |
| `m_itemData` | `ItemDrop` | `public` | read |
| `m_shared` | `ItemData` | `public` | read |
| `m_name` | `SharedData` | `public` | read |

`transform.position`, `gameObject`, `GetComponent<T>()` and `Object.name` are `UnityEngine` members, not
Valheim, and are all public.

**Read via reflection because they are `private` in the shipping assembly (unchanged from round 1):**

| Member | Shipping visibility |
|---|---|
| `Smelter.GetQueueSize()` | `private` |
| `Smelter.GetFuel()` | `private` |
| `CookingStation.GetFuel()` | `private` |
| `CookingStation.HaveDoneItem()` | `private` |
| `CookingStation.GetFreeSlot()` | `private` |
| `CookingStation.IsFireLit()` | `private` |
| `Smelter.FindCookableItem(Inventory)` | `private` — **no longer referenced at all** |
| `CookingStation.FindCookableItem(Inventory)` | `private` — **no longer referenced at all** |

**Harmony patch targets** (a private target is fine — Harmony installs a detour through its own
`AccessTools` lookup; our assembly emits no `call` to it, unlike a direct invocation):

| Patch | Shipping visibility |
|---|---|
| `Fireplace.Interact(Humanoid, Boolean, Boolean)` | `public` |
| `Turret.UseItem(Humanoid, ItemData)` | `public` |
| `Smelter.OnAddOre(Switch, Humanoid, ItemData)` | `private` (patched) |
| `Smelter.OnAddFuel(Switch, Humanoid, ItemData)` | `private` (patched) |
| `CookingStation.OnAddFuelSwitch(Switch, Humanoid, ItemData)` | `private` (patched) |
| `CookingStation.OnInteract(Humanoid)` | `private` (patched) |

**Harmony-injected parameter** (also a private field, resolved by Harmony's own field injection, not by a
direct read from this assembly): `Fireplace.m_nview` → `private`, injected as `___m_nview`.

**Verified but no longer read by this file:** `Turret.m_ammoType` and `SharedData.m_ammoType` (both
`public`) — since round 1 the turret uses `GetAmmoType()` unconditionally, so the field is only named in
the doc comment.

Rule cross-check on the known counterexample: `Player.m_placementStatus` probes as `private` (which is
why `PullKeyPatches.cs:64` reads it through `AccessTools.Field`), confirming that the `public` results
above are a real property of these members and not an artifact of the probe.

## 6. Files changed (commit ab50bfc, 1 file, +118/-10)

* `CraftFromDrawersAndChests/Patches/StationPatches.cs` — the two `FindCookableItem` guards replaced by
  the public `Inventory.GetItem` helper `InventoryHasInput`; `Resolve` wrapped in try/catch; the three
  typed helpers now log an unexpected return type once; doc comments updated (the class remark explaining
  why `FindCookableItem` is removed rather than reflected, the smelter and cooking guards, and the
  cooking patch's `<para>` that previously said the guard "calls the station's own `FindCookableItem`").

No new files, so nothing to register in the csproj; no scratch scripts were left in the tree.

## 7. Self-review

* The `Logic/**` purity, the "never write a container's inventory or a drawer's ZDO `Amount`", the prefab-name
  keying and the `-1`/`ChestTake.Deferred` rules are all untouched by this round (the only edits are in
  `Patches/StationPatches.cs`), and the build and the 205 tests confirm no behavioural regression on the
  pure half.
* The replacement is *strictly* weaker in reach than the removed call: `InventoryHasInput` only ever
  inspects the player's inventory, exactly as `FindCookableItem(inventory)` did, so the pull decision is
  unchanged while the non-public dependency disappears.
* Nothing was reflected for `FindCookableItem` — R30 satisfied — and `Fermenter::FindCookableItem` is not
  referenced anywhere.
* The one thing tests cannot cover is `StationPatches.cs` itself (it needs the game); the IL-level checks
  above (zero `FindCookableItem` call sites, the `Inventory.GetItem(String, Int32, Boolean)` call site
  present) are the strongest evidence available off-machine, backed by the existing AUTHOR-VERIFY steps.

## 6. Scope

No `Logic/**` dependency change (only doc comments); no write to a `Container` inventory or a drawer ZDO
`"Amount"`; the `-1` sentinel untouched; no new files, so no csproj `<Compile>` changes; no scratch scripts
left in the tree (`git status` before the commit listed only `StationPatches.cs`, `StationFeedRules.cs` and
`REFERENCES.md`); no push. `MSB3274` printed as expected and untouched.
