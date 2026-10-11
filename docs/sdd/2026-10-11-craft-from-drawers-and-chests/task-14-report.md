# Task 14 report — Ctrl-click pull, messages and flash

Branch `feature/craft-from-drawers-and-chests`, commit `fc0d0d8` ("feat: ctrl-click ingredient pull with feedback").

## What I implemented

- **`PullEngine.PullToPlayer(Piece.Requirement[] requirements, int qualityLevel, int multiplier, Player player, FeatureFlags flags)` → `bool`** (`Pull/PullEngine.cs`). The ctrl-click action: it asks storage for the **whole** recipe (not the shortfall) through `PullEngine.PlanFor(...)`, refuses when the plan is infeasible, refuses when the player's inventory cannot accept everything the plan would deliver, and only then executes the plan with `PullEngine.Execute(...)`. Returns `true` only when the pull completed; `false` consumes nothing and drops nothing.
- **`PullEngine.DestinationAllows(Player, Piece.Requirement[], PullPlanResult, DestinationCapacity.Delivery?)`** (`Pull/PullEngine.cs`, public). The destination half of the availability gate, now living in one place and called by both the crafting path and the pull.
- **`Patches/PullKeyPatches.cs`** (new). A prefix on `InventoryGui.OnCraftPressed` turns a recipe click into a pull while the pull key is held, and a postfix on `InventoryGui.SetupRequirement` flashes the requirement readout. Both obey the prevent gate.
- **`Logic/PullKeyRules.cs`** (new, pure) + **`Tests/Logic/PullKeyRulesTests.cs`** (new, 16 tests).

## Helpers reused

- `PullEngine.PlanFor(...)` — the single planning entry point Task 13 reserved for this task (Carried Decision 1). `PullToPlayer` uses it; no second planning entry point was added.
- `Logic/DestinationCapacity.CanAccept` — the free-space question, reached through the shared `PullEngine.DestinationAllows` (Carried Decision 2). `PullToPlayer` passes `reserved: null` (nothing is crafted, so no output is charged).
- `Logic/PullRunner.Run` — the take sequencing, reached through `PullEngine.Execute`. `PullRunner`'s `ChestTake.Deferred` (`-1`) handling is untouched: `-1` is never summed or retried.
- `PullEngine.Collect` / `OriginOf` / `CountHeld` / `PrefabName` / `CanServeSynchronously`, `ToggleRules.IsKeyConfigured`, `PluginConfig.Flags` / `FlashColor` / `UnFlashColor`.

**Refactor to avoid a parallel rule:** Task 13's `CraftPatches.DestinationAllows` and its private `FindRequirement`/`ItemDataOf`/`DestinationStacks`/`DestinationEmptySlots` duplicated the capacity rule. I moved that logic into `PullEngine.DestinationAllows` (game-facing, so it stays out of `Logic/`) and reduced `CraftPatches.DestinationAllows` to a one-line delegation. This is why `CraftPatches.cs` appears in the diff, beyond the brief's declared file list; the alternative was a second copy of the free-space rule, which Carried Decision 2 forbids.

## How the failure path refuses without dropping

`PullToPlayer` orders the work so nothing can move until delivery is proven:

1. `PlanFor` → if `!plan.Feasible`, return `false`. `Plan` is all-or-nothing, so an infeasible plan has **no lines** and nothing is touched.
2. `plan.Lines.Count == 0` → return `true` (the recipe needed nothing from storage).
3. `DestinationAllows(...)` → if `false`, return `false` **before any `Execute`**. The whole delivery is modelled with `DestinationCapacity.CanAccept` (matching free stack space + empty slots, chunked as `Inventory.AddItem` places it); a line whose item cannot be described fails closed.
4. Only after (3) passes does `Execute` run, and `Execute`'s per-line walk hands items straight into the player inventory via `IStorageSource.Take(...)`.

So a refusal at any step means zero items left storage, and the successful path only ever removes an item that the destination model has already proven it can hold — items cannot land on the ground (Carried Decision 7 / Review Focus 2).

## How the message de-duplicates

`PullKeyRules.MissingMessage(IReadOnlyList<string>)` runs the shortfall names through `PullKeyRules.DistinctNames(...)`, a first-seen-order `HashSet` collapse that also drops null/empty entries. `PullPlan` may report the same item once per unfilled request (Task 4's ruling; the planner stays a pure per-request reporter), so the presentation layer collapses repeats here — each missing item is named once. `PullKeyRulesTests` pins this: `MissingMessage_NamesEachMissingItemOnce` asserts `"Wood"` occurs exactly once for `["Wood","Wood","Resin"]`. The success path shows `flags.PulledMessage` unchanged.

## `PreventModKey` / `SwitchPrevent` semantics implemented, and the basis

Implemented as a faithful port of CraftFromContainers 4.0.7, in pure `PullKeyRules.AllowByKey(bool switchPrevent, bool preventKeyHeld)`:

```csharp
return preventKeyHeld ? switchPrevent : !switchPrevent;
```

- **`SwitchPrevent == false` (shipped default):** holding `PreventModKey` (`"left alt"`) **suppresses** this mod's pull, so the click behaves as vanilla; not holding it allows the pull.
- **`SwitchPrevent == true`:** the roles invert — the mod's pull only happens **while** the prevent key is held.

`ShouldPull = pullKeyHeld && AllowByKey(...)` is what the click prefix consults; when it is false the prefix returns `true` and vanilla crafts. The gate affects only this mod's pull state (the click prefix and the readout flash); it writes nothing.

**Basis — not an invention.** I retrieved the reference source (`aedenthorn/ValheimMods`, `CraftFromContainers/BepInExPlugin.cs`, public domain) over `raw.githubusercontent.com` and read it directly:

```csharp
public static bool AllowByKey()
{
    if (CheckKeyHeld(preventModKey.Value))
        return switchPrevent.Value;
    return !switchPrevent.Value;
}
```

Its config description is *"if true, holding down the PreventModKey modifier key will allow this mod's behaviour; if false, holding down the key will prevent it."* This also matches the wording already committed in this repo by Task 8 (`PluginConfig.cs`: *"If true, holding the prevent key enables this mod's pulling instead of suppressing it"*, and the `ConfigModel` comment *"switchPrevent stays off, so out of the box holding the prevent key suppresses pulling rather than enabling it"*).

**Where the brief's own note disagreed:** the brief's parenthetical described `SwitchPrevent` as "a key held to suppress the per-storage toggle". That reading is not what the reference mod does and would make the setting affect toggle state (explicitly out of scope per the brief's "keep it affecting only this mod's pull state"). I implemented the reference semantics above and flag the discrepancy here rather than silently choosing.

## Did the flash colour entries already exist?

**Yes.** `PluginConfig` already exposed them as bound entries — `ConfigEntry<Color> flashColor` / `unFlashColor`, bound to `ConfigModel.FlashColor` ("Flash color", default `Color.yellow`) and `ConfigModel.UnFlashColor` ("Unflash color", default `Color.white`), surfaced by the `PluginConfig.FlashColor` / `UnFlashColor` accessors (Task 8). I used those accessors and invented no value. (Carried Decision 5 satisfied — no finding needed.)

## What moved into `Logic/`, and its tests

**`Logic/PullKeyRules.cs`** — the pure decisions: `AllowByKey`, `ShouldPull`, `DistinctNames`, `MissingMessage`, and the `NoRoomMessage` constant. No Unity/Valheim/BepInEx/Jotunn types.

**`Tests/Logic/PullKeyRulesTests.cs`** — 16 tests: all four `AllowByKey` key/switch combinations, the switch being the exact inverse of itself, `ShouldPull` requiring the pull key and honouring the gate, `DistinctNames` (null, duplicates, order, null/empty entries) and `MissingMessage` (de-duplication, empty-list fallback).

The shared capacity check did **not** move to `Logic/`: it must read `Inventory`/`ItemDrop`, so it belongs in the game-facing `Pull/` layer; the pure part it drives (`DestinationCapacity.CanAccept`) was already in `Logic/` and already tested.

## Build and test result lines

```
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
Passed!  - Failed: 0, Passed: 152, Skipped: 0, Total: 152, Duration: 141 ms - CraftFromDrawersAndChests.Tests.dll (net462)
```

136 baseline passing + 16 new; 0 failing. `MSB3274` printed as expected and was not touched.

## Files changed

- `CraftFromDrawersAndChests/Pull/PullEngine.cs` (modified — `PullToPlayer`, `DestinationAllows`, private helpers)
- `CraftFromDrawersAndChests/Patches/PullKeyPatches.cs` (new)
- `CraftFromDrawersAndChests/Patches/CraftPatches.cs` (modified — delegates to `PullEngine.DestinationAllows`)
- `CraftFromDrawersAndChests/Logic/PullKeyRules.cs` (new)
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` (modified — two `<Compile>` items)
- `Tests/Logic/PullKeyRulesTests.cs` (new)

## AUTHOR-VERIFY

The brief's in-game Step 4, to be run by the author (not automated here):

- [ ] Ctrl-click a recipe whose materials are in a chest and a drawer. Expected: the recipe is **not** built, the ingredients land in your inventory, and the pulled message shows.
- [ ] Confirm the **readout flash**: with the crafting panel open on a recipe the inventory cannot cover but storage can, the requirement's amount text flashes between `Flash color` and `Unflash color`. (This is the flash affordance from CraftFromContainers; see the concern below about its interpretation.)
- [ ] Fill your inventory completely and repeat the ctrl-click. Expected: nothing is pulled, **nothing is on the ground**, and a message says why (missing items by name, or "not enough room").
- [ ] Hold `Prevent mod key` and ctrl-click. Expected: the pull is suppressed and the click crafts as vanilla.
- [ ] Set `Switch prevent = true`, reload, and ctrl-click with and without the prevent key. Expected: the pull only happens while the prevent key is held.

## Self-review against the Carried Decisions

1. `PlanFor` used; no second planning entry point. ✅
2. `DestinationCapacity` reused (shared via `DestinationAllows`); `PullRunner` reused via `Execute`; no parallel rule. ✅
3. `MissingItemNames` de-duplicated in the message. ✅
4. Reads `PluginConfig.Flags`; no `FeatureFlags` constructed. ✅
5. Flash colours taken from `PluginConfig.FlashColor`/`UnFlashColor` (already bound). ✅
6. `PreventModKey`/`SwitchPrevent` implemented and basis stated. ✅
7. Items never on the ground — capacity proven before any take; fails closed. ✅
8. `ChestTake.Deferred` untouched (`PullRunner` handles it; never summed/retried). ✅
9. Range origin is `PullEngine.OriginOf(player)`. ✅

Constraints: `Logic/**` dependency-free; no writes to `Container.m_inventory` or a drawer ZDO `"Amount"`; storage keyed by prefab name (`CountHeld`/`PrefabName`/`source.Count(prefab)`); new files registered in the csproj; probes were run from `%TEMP%` and deleted; no Nexus artefacts; no push.

## Concerns

1. **Flash interpretation (most significant).** The brief/plan phrase this as "flashes the contributing sources/storages" ("the contributing chest and drawer flash"). I implemented CraftFromContainers' actual flash — the **resource-readout** amount text alternating between `FlashColor`/`UnFlashColor` in `InventoryGui.SetupRequirement` — because (a) the design's Global Constraint says CFC 4.0.7 *is* the behavioural spec and this project changes "only three things" (storage layer, mutation paths, config/sync) — not the flash; (b) this repo's own `PluginConfig` descriptions call it "the resource readout flashes"; and (c) a world-object highlight would require extending `IStorageSource` to expose a `GameObject`, which is outside the brief's declared file scope (only `PullEngine.cs` + `PullKeyPatches.cs`). If the author genuinely wants the storage **objects** to highlight, that is a different feature and needs the interface change — flagging it rather than inventing it as parity.
2. **Building pull-key not implemented.** CFC also prefixes `Player.UpdatePlacement` so the held pull key fetches a *piece's* materials without placing. The brief scopes this task to "clicking a recipe" and "prefixes `InventoryGui`", and the plan's Step 4 checks a recipe only, so I implemented crafting only. If CFC parity for building is wanted, it is a follow-up on `Patches/PullKeyPatches.cs`.
3. **Panel-refresh coverage row not implemented.** The plan's self-review table assigns "Crafting-panel refresh while the key is held" to Task 14 (CFC's `InventoryGui.Update` prefix that re-runs `UpdateCraftingPanel` when `AllowByKey()` flips). The brief does not list it, and in this build it would be inert: `CraftPatches`' requirement postfixes gate on `ModEnabled`, not on `AllowByKey`, so the panel's availability does not change with the prevent key. I omitted it rather than add a no-op patch. If parity is required here, `CraftPatches` would need the same `AllowByKey` gate.
4. **Double planning on the failure path.** The click prefix calls `PullToPlayer` (which plans internally); on failure it re-calls `PlanFor` to name the missing items. Sources are cached per frame so the cost is small, and it only happens on a refused pull. A richer outcome type from `PullToPlayer` would avoid it, but the brief pins the `bool` signature.
5. **`CraftPatches.cs` touched.** An unplanned-but-deliberate refactor (Concern of Decision 2), to keep a single free-space rule. It is behaviour-preserving: `CraftPatches.DestinationAllows` now delegates to the extracted `PullEngine.DestinationAllows` with the same `reserved` output ordering.
6. **`"res_amount"` child name is version-fragile.** The readout flash locates the amount `TMP_Text` via the game's `res_amount` child (as CFC does). A future game version renaming it degrades to "no flash" (null-guarded), not an exception.

## Fix round 1 (Task 14 R11/R12), commit `bf4a2e3`

### Audit of the uncommitted draft

I treated the four modified files as an unverified draft and checked every requirement against the CFC oracle
(`CraftFromContainers/BepInExPlugin.cs`, read from the local checkout) and the shipped game IL
(`assembly_valheim_publicized.dll`, Valheim 1.0.16, decompiled with Mono.Cecil). The draft was **substantially
correct and complete** — no Critical, no Important gap, nothing to revert. What I had to complete or correct: nothing
functional; I ran the full build and test suite on it and committed it as-is. Everything below was independently
verified rather than taken on the draft's word.

### R11 — how `resourceString` is consumed, and a malformed one

`PullKeyRules.ResourceAmountText(resourceString, available, required)` (pure, in `Logic/`) is the single consumer.
The `SetupRequirement` postfix now calls it and assigns the result to `text.text`, exactly where CFC writes
`text.text = string.Format(resourceString.Value, invAmount, amount)`. `available` is the player's inventory count,
augmented with eligible sources only when the player is short — the same "scan storage only when needed" order CFC
uses — so the readout reads "available/required".

- null, empty or whitespace-only setting → the plain required amount (`required.ToString`), matching CFC's
  `text.text = amount.ToString()` fallback (its config description "set to nothing to leave it as default").
- a malformed format — unbalanced braces, or an argument index the readout does not supply — throws
  `FormatException`, which is caught and falls back to the plain amount, so a bad config entry can never throw
  inside the game's UI loop. (CFC would throw here; we deliberately do not.) Tests pin `"{0}/{1"` and `"{2}"` → `"5"`.
- Deliberate micro-divergence: we format with `CultureInfo.InvariantCulture` where CFC uses the current culture.
  For the shipped default `"{0}/{1}"` the output is identical on every ordinary locale; the invariant choice just
  makes the fallback and the format deterministic.

### R12 — exactly how the prevent key affects availability

The key is a single decision, `PullKeyRules.AllowByKey(switchPrevent, preventKeyHeld) = preventKeyHeld ? switchPrevent
: !switchPrevent`, read through the one game-facing wrapper `PullKeyPatches.AllowByKey()`. With the shipped default
(`SwitchPrevent=false`) holding the prevent key makes the gate **refuse**; with `SwitchPrevent=true` it makes the gate
**allow**. The gate now governs every storage-substitution and pull site this project has, so both key states flip the
whole feature consistently. Call sites touched:

- `CraftPatches.HaveRequirementItemsPostfix` — when the gate refuses, returns before substituting storage, so the
  craft answer is vanilla's own (player inventory only).
- `CraftPatches.HaveRequirementsPiecePostfix` — the same for the building answer (`CanBuild` / `CanAlmostBuild`).
- `CraftPatches.ConsumeResourcesPrefix` — when the gate refuses, returns `true` without pulling, so vanilla consumes
  from the player's own inventory only.
- `PullKeyPatches.OnCraftPressedPrefix` and `SetupRequirementPostfix` — already gated at `fc0d0d8` (the pull and the
  readout); unchanged this round except for the readout text below.
- `PullKeyPatches.InventoryGuiUpdatePrefix` (new) — re-runs `InventoryGui.UpdateCraftingPanel(false)` the frame the
  gate flips, so the recipe *list* follows the key. This is needed, not cosmetic: `InventoryGui.Update` only calls
  `UpdateRecipe` (selected recipe) and `UpdateRepair`; the list is rebuilt solely by
  `UpdateCraftingPanel → GetAvailableRecipes → UpdateRecipeList` (verified in IL), which is why CFC has the same patch.

### The safety argument — neither direction can produce a free craft

The dangerous state would be "a gate counted storage, but the pull was suppressed". It cannot occur because the
availability decision and the consumption decision are made **inside the same synchronous vanilla call with the same
key state**:

- Crafting: vanilla `InventoryGui.DoCrafting` calls `Player.HaveRequirements(Recipe, …)` → `Player.HaveRequirementItems(…)`
  (our gated postfix) and, later in the same method, `Player.ConsumeResources(…)` (our gated prefix). Verified in IL.
- Building: vanilla `Player.UpdatePlacement` calls `Player.HaveRequirements(Piece, mode)` (our gated postfix), then
  `TryPlacePiece`, then `Player.ConsumeResources(…)`. Verified in IL.

Unity samples input once per frame and no input can arrive mid-call, so the two gate reads cannot disagree.

- **Gate allows:** availability may count storage → `__result = true`; the consume prefix's gate also allows → the
  shortfall is pulled into the inventory → vanilla consumes it. The craft is fully paid.
- **Gate refuses:** availability leaves vanilla's answer (inventory only). If the player lacks the materials in the
  inventory, `DoCrafting`/`UpdatePlacement` abort *before* creating the item or placing the piece; if the player has
  them, the consume prefix returns `true` and vanilla consumes from the inventory only — no pull, storage untouched.

The panel can be stale (built on an earlier frame), but a click re-runs the gated check at click time, so a stale
"craftable" cannot yield a craft: the click aborts, or crafts from the inventory. There is no path on which storage
is counted and the pull is then suppressed.

### Case normalisation

`PullKeyPatches.KeyHeld` now passes `key.ToLowerInvariant()` to `Input.GetKey`, after the `ToggleRules.IsKeyConfigured`
guard, mirroring CFC's `Input.GetKey(value.ToLower())`. A user who types `"Left Ctrl"` now gets a working pull key
instead of a silently dead one. Both the pull key and the prevent key go through this one helper.

### Build and test (run on this exact tree)

```
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
Passed!  - Failed: 0, Passed: 157, Skipped: 0, Total: 157, Duration: 141 ms - CraftFromDrawersAndChests.Tests.dll (net462)
```

152 from `fc0d0d8` + 5 new `ResourceAmountText` tests. `MSB3274` printed as expected and was not touched.

### CFC behaviour this project still lacks

- **Building-piece pull** (CFC's `UpdatePlacement_Patch`: holding the pull key and attacking pulls the selected
  piece's materials). Not implemented — out of scope this round and scheduled separately by the controller.
- **Station feeding** (CFC's `Fireplace`/`Smelter`/`CookingStation`/`Turret` prefixes and the `fillAllModKey`
  "fill to max" behaviour). This project's station feeding is a different, separately-planned task, so CFC's exact
  `fillAllModKey` semantics are not present.
- **`leaveOne` in the readout/availability** and **cross-requirement contention** in the flash — deferred by the
  brief and untouched (the readout can therefore still over-promise), as is the double planning on the refusal path.
