# CraftFromDrawersAndChests â€” "nearby but blocked" hint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development or
> superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a crafting requirement is not covered because the only storage holding the item is
*switched off* or *locked*, say so â€” instead of showing the same `0/1` as "you have none".

**Origin:** the `SealBlubber` / Unbaked Poteitr investigation (2026-11-07). The player had switched
that one drawer off with the per-storage key; the readout said "nothing holds it", the hover line on
the drawer said `(pull: off)`, and only a `Debug` log could connect the two. The debug log is fixed
(1.0.10); this plan puts the same answer in the UI.

**Previous plan:** the finished Valheim 1.0.20 compatibility plan is archived at
`docs/superpowers/plans/2026-10-11-craft-from-drawers-and-chests.md`; its evidence record is
`docs/sdd/2026-10-11-craft-from-drawers-and-chests/game-1.0.20-compat-analysis.md`. This file replaces
`plan.md`, which `.github/copilot-instructions.md` requires at the repo root.

## Decisions taken by the owner (2026-11-07)

1. **Which blocks count:** the per-storage **off switch** and **no access** (another player's private
   chest, or a ward denying the chest). Out-of-range, loose containers and disabled kinds are *not*
   hinted â€” "not a source" is not "locked".
2. **Signal on the requirement row:** **colour + suffix** â€” the amount text keeps its
   `available/required` numbers, is tinted amber, and gains a marker (`" *"` by default).
3. **Second signal:** **yes** â€” one HUD line naming the item and the reason, shown when the crafting
   panel shows a recipe with a blocked shortfall, once per panel open.

## Global Constraints

- **Pulls must not change.** A blocked storage stays unusable; the blocked list is a read-only
  diagnostic and must never reach `Plan`/`Execute`. (Same rule as
  `StorageRegistry.CollectIncludingSwitchedOff`, which this plan reuses in spirit.)
- `Logic/**` must not reference UnityEngine/Valheim/BepInEx/JÃ¶tunn.
- The numbers in the readout must not change: `Resource string` still formats
  `available`/`required`, so a CraftFromContainers-style `{0}/{1}` reads identically.
- Legacy csproj: a **new `.cs` file in the product project must be added to `<Compile>`**, or the
  build silently omits it. Prefer adding types to existing files; one new `Logic/` file is planned
  because the Tests project links `Logic/**` by glob while the product project needs one csproj line.
- TDD: failing test first (`/tdd`), then the change.
- No publishing. Version for this build: **1.0.11** (1.0.10 stays the tested 1.0.21-baseline build).

## Design

**Pure rule â€” `Logic/AvailabilityHint.cs`** (new):

```csharp
[Flags] public enum StorageBlockReasons { None = 0, SwitchedOff = 1, NoAccess = 2 }

public static class AvailabilityHint
{
    public static bool ShouldHint(int required, int available, int blockedStock);
    public static string Suffix(string configuredSuffix);           // "" stays ""
    public static string ReasonPhrase(StorageBlockReasons reasons); // "switched off" | "locked" | both
    public static string Message(string displayName, StorageBlockReasons reasons);
}
```

`ShouldHint` is `available < required && blockedStock > 0` â€” a *partially* blocked shortfall still
explains why the count looks wrong, which is the whole point.

**Why is this source blocked? â€” `Logic/Eligibility.cs`:**

```csharp
public enum StorageBlockReason { None, SwitchedOff, NoAccess }
public static bool TryBlockReason(in StorageCandidate c, Vec3 origin, float range,
    bool chestsEnabled, bool drawersEnabled, ChestTypeRules rules, out StorageBlockReason reason);
```

True **only** when the storage is in range, of an enabled kind, well-formed as storage, and blocked
by exactly one of the two lock reasons (switch checked first). Out-of-range / loose / ignored chest
types / null inventory return false. A drawer can only be `SwitchedOff` (drawers carry no access
check in this mod).

**Collection â€” `Storage/StorageRegistry.cs`:**

```csharp
public readonly struct BlockedStorage
{
    public readonly IStorageSource Source;
    public readonly StorageBlockReason Reason;
}
public static IReadOnlyList<BlockedStorage> CollectBlocked(Vec3 origin, FeatureFlags flags);
```

`Pull/PullEngine.cs` mirrors it with its own per-frame cache slot (`CollectBlocked`), separate from
`Collect` and `CollectIncludingSwitchedOff` for the same reason those two are separate.

**Readout â€” `Patches/PullKeyPatches.cs::SetupRequirementPostfix`:**

1. `available = held + fromStorage` (unchanged).
2. If `available < amount`: walk `PullEngine.CollectBlocked(origin, flags)`, summing the count of
   `prefab` in each and OR-ing its reason flags.
3. If `AvailabilityHint.ShouldHint(...)`: `text.color = PluginConfig.BlockedColor` and
   `text.text = PullKeyRules.ResourceAmountText(...) + AvailabilityHint.Suffix(flags.BlockedHintSuffix)`.
4. Otherwise the existing behaviour, untouched: flash while storage covers the gap, vanilla colour
   otherwise.
5. HUD line (when `flags.BlockedMessage`): `AvailabilityHint.Message(localized item name, reasons)`
   at `MessageHud.MessageType.TopLeft`, deduped per `prefab|reasons`, with the dedupe set cleared
   when the crafting panel changes visibility, so the line is not silent for the rest of the session.

**Configuration (3 new entries, all `IsAdminOnly` like the rest):**

| Section | Key | Default | Meaning |
|---|---|---|---|
| `1 - Server` | `Blocked hint suffix` | `" *"` | Appended to the readout when the shortfall is only in blocked storage; empty disables the marker (the colour still applies). |
| `1 - Server` | `Blocked color` | `FFA500FF` (amber) | Tint of that readout. Presentation-only accessor on `PluginConfig`, like the flash colours. |
| `1 - Server` | `Blocked message` | `true` | Show the one-off HUD line. |

`BlockedHintSuffix` and `BlockedMessage` join `FeatureFlags`; `BlockedColor` joins the `PluginConfig`
colour accessors, keeping `FeatureFlags` free of Unity types.

## Tasks

### Task 1: The pure rule (TDD)

- [x] Write `Tests/Logic/AvailabilityHintTests.cs` first, covering: no hint when covered; no hint
      when nothing is blocked; hint for a fully blocked shortfall; hint when only part of the
      shortfall is blocked; suffix appended verbatim and an empty suffix adding nothing; reason
      phrase for each flag and for both; message naming the item and the phrase.
- [x] Run `dotnet test` and confirm it fails to compile (`AvailabilityHint` does not exist).
- [x] Create `Logic/AvailabilityHint.cs` implementing the rule, and add it to
      `CraftFromDrawersAndChests.csproj` `<Compile>`.
- [x] Re-run `dotnet test` â†’ green (298).

### Task 2: The block reason

- [x] Add `StorageBlockReason` + `Eligibility.TryBlockReason` to `Logic/Eligibility.cs`.
- [x] Extend `Tests/Logic/EligibilityTests.cs`: off drawer â†’ `SwitchedOff`; off chest â†’ `SwitchedOff`
      (switch wins over access); accessible-but-denied chest â†’ `NoAccess`; out-of-range, drawers
      disabled, chest outside a piece, ignored chest type â†’ `false`; an eligible chest â†’ `false`.
- [x] Green (308).

### Task 3: Collection + cache

- [x] `StorageRegistry.CollectBlocked` (+ `BlockedStorage` in the same file), fed by the existing
      single pass so there is no second walk of the base.
- [x] `PullEngine.CollectBlocked` with its own cache slot.
- [x] Doc comments state plainly: diagnostic only, never a pull source.

### Task 4: Readout + HUD line

- [x] Config: `ConfigModel` keys/defaults + `FeatureFlags` properties + `PluginConfig` bindings
      (`BlockedColor` accessor).
- [x] `SetupRequirementPostfix`: blocked branch before the flash branch; suffix and colour; HUD line
      with the localized item name, deduped; clear the dedupe on the panel visibility change in
      `InventoryGuiUpdatePrefix`.
- [x] No change to `PullEngine.PlanFor`/`Execute` call paths.

### Task 5: Verify and package

- [x] `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` â†’
      `References: â€¦ (Valheim 1.0.21)`, `BUILD OK - CraftFromDrawersAndChests 1.0.11 (Debug)`.
- [x] `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` â†’ all green (308).
- [x] Version bump: `ModInfo.Version` and `manifest.json` â†’ `1.0.11`.
- [x] `-Configuration Release` â†’ `Release\CraftFromDrawersAndChests-1.0.11.zip` (70,857 bytes).
- [x] Docs: `README.md` (behaviour + configuration rows), `CHANGELOG.md` `## [1.0.11]`.

## Verification in game (owner)

1. Switch a drawer holding a recipe ingredient off; open the recipe: its row is amber with `0/1 *`,
   and the HUD says `<item> is nearby but the storage holding it is switched off.`
2. Repeat with a private chest owned by another player (or behind a ward): the line says `locked`.
3. Storage that is in range and usable still flashes as before; a genuinely absent item still looks
   exactly as it does today.
4. Pulls are unchanged: a blocked storage supplies nothing.

## Risks / non-goals

- **False positives.** Marking a row for a storage the player cannot use even if it were unlocked is
  *informative*, not an error â€” the hint never claims the item is reachable.
- **Cost.** One extra collection per unanswered requirement per frame, cached per frame like the
  others, and skipped entirely when the requirement is covered.
- **Not** planned: hinting out-of-range storage, hinting config exclusions, or a per-storage icon.

---

# Follow-up: a chest another player fills reads as empty (measurement in progress)

**Report:** another player's chest changes are counted as empty "until you open the affected chests
again"; the reporter runs `goldenrevolver.quick_stack_store` with MultiUserChest.

**Established from the decompiled assemblies** (facts, not inference):
- `Container` keeps contents in `m_inventory`, loaded from the ZDO at `Awake`; `CheckForChanges` runs on
  a 1 s timer but calls the private `Load()`, which returns immediately unless the **local** ZDO's
  `DataRevision` changed.
- `Container.OnContainerChanged` saves the chest back **only when this client owns the ZDO**.
- The guaranteed push of a chest's contents happens at `RPC_RequestOpen`/`RPC_RequestStack`
  (`ForceSendZDO` then `SetOwner`).
- MultiUserChest applies a change on the **owning** client; Quick Stack Store (MUC-aware) calls
  `m_nview.ClaimOwnership()` before storing.
- `ZDOMan.RequestZDO` targets the **server** (`InvokeRoutedRPC` → `GetServerPeerID`), and
  `RPC_ZDOData` applies an incoming copy only when its revision is **newer**.

**Observed in game (owner's server):**
1. A creates a chest and puts 1 SealBlubber in it; B counts 0 at the Food Preparation Table.
2. B opens the chest, then B counts 1.
3. With 1.0.13 installed, A then opening that chest made B's count drop **1 → 0**.

**What is NOT established — and the reason this plan stops here:** which peer's copy is wrong in each
step, and therefore whether any request-based fix can work at all. Shipping behaviour on that guess was
a mistake: 1.0.12 and 1.0.13 were two speculative fixes, and the second turned a display bug into a
downgrade.

### Current state of the tree (1.0.15, diagnostic, not released)

- **All chest-sync behaviour is reverted.** No `RequestZDO`, no reflected `CheckForChanges`, no
  ownership claim, no writes. Behaviour with `Debug` off is identical to the published **1.0.11**.
- **Observation only, and only with `Debug` on:**
  - every change to a chest's local contents logs
    `Chest '<name>' contents changed: items A -> B, revision R, zdoItems N bytes, owner O, weAreOwner X`;
  - each in-range chest is described at most every 10 s while the collection runs:
    `Chest '<name>': items N, revision R, zdoItems N bytes, owner O, weAreOwner X`;
  - cadence is `Logic/StorageRefreshRules` (pure, 4 tests); bookkeeping pruned; `Clear()` resets it.
- `dotnet test` 312/312; `Build.ps1 -Debug` and `-Release` OK.

### Next step — measure, then decide (one test run)

Run the three steps above on the server with `Debug` on, on **both** clients, and keep both
`LogOutput.log` files. For each step the logs answer one question:

| Question | What the log shows |
|---|---|
| Does B's copy ever receive A's item? | B: `items 0` with `zdoItems 0 bytes` throughout ⇒ B was never sent it; `zdoItems > 0` with `items 0` ⇒ B has the data and its copy is stale |
| Was B's 1 real? | B step 2: `items 1` and `zdoItems > 0` ⇒ yes, and the ZDO on B agrees |
| Who empties B at step 3? | A `contents changed`/name line at that moment + the owner id tells us whether the change came from a ZDO we received (and from which owner) or from a local path |
| Is the server's copy the empty one? | `zdoItems 0 bytes` on the owner side would show the item only in `m_inventory`, i.e. an owner that never saved |

Only after that: write the failing test / the fix that the evidence supports — including the possibility
that the answer is "read the owner's `Container` state through MultiUserChest" or "this cannot be fixed
without a MultiUserChest change", rather than another request-shaped guess.

---
# Done: Plateautem feed, storage + Adventure Backpacks (1.0.17, 2026-11-22)

- [x] Diagnose from Plateautem 0.4.3's own code: `TakeFuelItemFromUser` counts the selected fuel with
      `inventory.CountItems(sharedName, -1, true)` on `player.GetInventory()` and messages
      `$piece_plateautem_noFuel`. It never consults Adventure Backpacks, whose `CountItems` postfix adds
      an equipped backpack only inside its own `CraftingContext`.
- [x] Confirm the player's A/B test matches that mechanism exactly (backpack-only wood fails,
      main-inventory wood feeds).
- [x] `Logic/PlateautemFuelRules.PrefabToSupply(selectedIndex, fuelPrefabNames)` — stone in the totem's
      `-1` stone mode, otherwise the selected configured fuel; no fallback for an index past the list.
      Tests written red first (4), then green.
- [x] `Patches/PlateautemPatches` — soft dependency resolved by name; pulls one unit of the selected
      fuel from storage around the totem (gated on `Pull fuel` + the prevent key + not in place mode),
      then opens Adventure Backpacks' published `CraftingContext` scope so its count *and* its removal
      see the backpack, closing it in a finalizer.
- [x] Reflection targets verified against the installed DLLs (`Plateautem.Interact`,
      `fuelItems`, `currentFuelItemIndex`, `FuelItem.prefabName`, `CraftingContext.Enter/Exit`).
- [x] 322/322 tests; 1.0.17 forced-debug build packaged; normal 1.0.17 package regenerated after it.
- [x] In-game confirmation from the owner (2026-11-22: 1.0.19 — single press feeds one, holding E fills
      the totem from the drawer, and backpack-only fuel now pays).

# Done: backpack ammunition, spent in place (1.0.21, verified 2026-11-22)

- [x] Vanilla resolves ammo in `Attack.HaveAmmo`/`FindAmmo`/`UseAmmo` via `Inventory.GetAmmoItem`, which
      walks `m_inventory`; AB never widens it, so a bow with arrows in the backpack reported "out of".
- [x] Tried the move (1.0.20) then replaced it with three hooks that spend in place — lookup postfix,
      removal redirect, equip suppression — because `Inventory.RemoveItem` *refuses* a foreign item, so
      answering the lookup alone would fire arrows for free.
- [x] Verified in play on 1.0.21: 15 answers / 3 spends, stack unchanged at 92 during draws and cancels,
      bow and crossbow both usable with every inventory slot full, no patch failures.
- [ ] Release is **blocked**: the chest-locking issue still needs another player's logs before 1.0.22 can
      be considered the release baseline.

# Done: Valheim 1.0.30 compatibility (1.0.23, 2026-11-26)

- [x] Confirmed the release: Steam news post "Patch 1.0.30" (2026-11-25 22:49 local) and the installed
      `assembly_valheim.dll` rewritten 2026-11-26 05:13:14 (appmanifest buildid 26870846).
- [x] Rebuilt the references for 1.0.30 with `tools/New-ValheimRefs.ps1` and pointed
      `Directory.Build.props` at `1.0.30`; the 1.0.21 baseline was copied to `%TEMP%` first.
- [x] API diff, 1.0.21 vs 1.0.30 (24,493 -> 24,553 signatures after dropping compiler noise):
      **zero changes in any type this mod touches** — `Container`, `CookingStation`, `Fireplace`,
      `Inventory`, `InventoryGui`, `Player`, `Humanoid`, `ShieldGenerator`, `Smelter`,
      `Terminal`, `Turret` (including the nested `Turret/AmmoType` fields), `Attack`, `Piece`,
      `Recipe`, `ItemDrop`. The 338-line delta is UI/rendering/perf work: `SmokeRenderer`,
      `ConnectPanel` frame-time fields, `BuildUi` focus rects, accessibility brightness, the
      Forge of Potential and the renumbered compiler-generated iterator state machines.
- [x] `assembly_utils` delta checked separately: new `ColorUtils`/`FastIterableKeyValues`/`Cover`
      raycast buffers, `ZInput` dead-zone reshuffle (`GetButtonDown` unchanged), and
      `BrotliCompressor` removed — the last one is unused here; our only `assembly_utils` dependency
      is `ZInput.GetButtonDown`.
- [x] `Test-PatchTargets.ps1 -GameVersion 1.0.30`: **23/23 patch classes PASS**, exit 0.
- [x] Reflection-only targets confirmed present in the new assemblies (`Inventory.GetAmmoItem`,
      `Inventory.RemoveItem(ItemData,int)`, `Attack.EquipAmmoItem`, `Turret.GetAmmoType` and the
      ammo fields, `Container.CheckAccess`, `InventoryGui.UpdateCraftingPanel`,
      `Player.m_placementStatus`, `Player.HaveRequirementItems`).
- [x] 332/332 tests pass; Release 1.0.23 packaged (`CraftFromDrawersAndChests-1.0.23.zip`, 81,183 B).
- [x] Forced-debug 1.0.23 packaged separately for the outstanding chest-locking capture
      (`CraftFromDrawersAndChests-1.0.23-FORCED-DEBUG.zip`), banner verified, initialiser verified in
      IL, and the Release folder rebuilt clean afterwards.
- [x] Published on the owner's instruction (2026-11-27): commit `0ed84e8` on `main`, tag `v1.0.23`, and a GitHub release carrying `CraftFromDrawersAndChests-1.0.23.zip`.
- [ ] Still open, and shipped as a known issue: chest locking can leave a container open without handing over ownership when MultiUserChest is installed (see `No-Chest-Block/Docs/in-use-open-not-synced.md`).

# Done: Valheim 1.0.31 compatibility (1.0.24, 2026-11-27)

- [x] Confirmed the release: Steam news "Hotfix 1.0.31" (2026-11-27 03:06 local) and the installed
      `assembly_valheim.dll` rewritten 2026-11-27 05:22:45 (appmanifest buildid 26948984).
- [x] Rebuilt the references for 1.0.31 and pointed `Directory.Build.props` at `1.0.31`.
- [x] API diff, 1.0.30 vs 1.0.31 (24,553 -> 24,554 signatures after dropping compiler noise): **3 lines** in
      `assembly_valheim` - added `BuildUi.SearchFieldSelect(string)` and `BuildUi.SearchFieldDeselect(string)`,
      removed `BuildUiFavoriteCategoryCheckButton.Awake()`. **Nothing this mod touches changed.**
      `assembly_utils` is identical (2,055 vs 2,055 members, zero diff).
- [x] `Test-PatchTargets.ps1 -GameVersion 1.0.31`: **23/23 patch classes PASS**, exit 0.
- [x] Reflection-only targets confirmed present (`Inventory.GetAmmoItem`, `Attack.EquipAmmoItem`,
      `Turret.GetAmmoType` + `m_allowedAmmo`/`m_defaultAmmo`/`m_maxAmmo`/`m_ammoType`, `Container.CheckAccess`,
      `InventoryGui.UpdateCraftingPanel`, `Player.m_placementStatus`, `Player.HaveRequirementItems`).
- [x] 332/332 tests pass; Release 1.0.24 packaged.
- [ ] Open, unchanged: the MultiUserChest chest-locking issue shipped as a known issue in 1.0.23.

# In progress: absorb MultiUserChest (branch `MultiUserChestMerge`, 2026-11-27)

Why: chest locking can leave a container open without handing over ownership. The mechanism is visible in
MultiUserChest's source, but the *state* at the moment of failure is not - and its only diagnostic switch is
a compile-time `#if DEBUG` that its own changelog calls slow. Owning the code makes the failure observable
from our runtime `Debug` flag, which is the whole point of this branch.

Source: `..\No-Chest-Block-Original` (MID/MIT-licensed MultiUserChest 0.6.2, GUID
`com.maxsch.valheim.MultiUserChest`, namespace `MultiUserChest`).

## Rulings taken (owner, 2026-11-27)

1. **Conflict policy: hard incompatibility.** `[BepInIncompatibility("com.maxsch.valheim.MultiUserChest")]`
   replaces the hard `[BepInDependency]`. Verified in BepInEx 5.4 source (`Chainloader.cs`, the
   `pluginsByGUID.Values.ToList()` loop): a plugin whose incompatibility is present is **removed from the
   load list** and logged as `Could not load [...] because it is incompatible with: ...` - it does not load
   and cannot double-patch. The player must uninstall MultiUserChest.
2. **Scope: absorb everything, verbatim**, including `ItemDrawerCompat`, `OdinShip`, the EAQS wrapper and
   the disabled QuickStack transpiler.
3. **Layout: `Chest/` with namespace `CraftFromDrawersAndChests.Chest`** (`…Chest.Patches` for the patch
   folder). Network names stay verbatim: the ten `MUC_*` RPC routes and the `MUC_Ignore` ZDO flag are wire
   and save data, not cosmetics.
4. Version: **2.0.0**, because upgrading now requires deleting another plugin. Every peer and the server
   must move together (the version is already `VersionStrictness.Patch`).

## Phases

- [x] Verified `BepInIncompatibility` semantics against the BepInEx source (quote above), not from memory.
- [x] Vendor `Chest/**` with the namespace rewrite; keep file names and structure for diffability.
- [x] `Chest/ChestPatches.cs` replaces MUC's `Plugin : BaseUnityPlugin` (its `ModGuid`/`ModName`/
      `ModVersion`/`PatchAll`/`ApplyModPatches`, minus the BepIn attributes) and is driven from our `Awake`.
- [x] `Log.Init(Logger)` must run before any absorbed code logs, or MUC's `Log` silently falls back to
      `UnityEngine.Debug`.
- [x] Remove `<Reference Include="MultiUserChest">`, its MSB3274 workaround target, and
      `.references\Mods\MultiUserChest.dll` from the compile path; list all 30 `Chest\**` files in the
      legacy csproj (it does not glob).
- [x] `MultiUserChestBridge`/`VanillaChestSource` switch to `CraftFromDrawersAndChests.Chest`.
- [x] Compile on net462 (`LangVersion 10` covers MUC's one C# 7.1 `default` literal; no 4.7.2-only API found).
- [x] Diagnostics: route MUC's `PrintDebug`/timer output through our runtime `Debug` flag instead of
      `#if DEBUG`, and log the open-hand-over decision - `IsContainerInUse`, `HasOwner`, `IsOwner`,
      `m_inUse`, the chosen branch - once per open, not per frame.
- [x] Verify the artifact: `Test-PatchTargets.ps1` (note MUC adds **4 transpilers**, which the script cannot
      judge), `strings.ps1` for the `MUC_*` routes and `MUC_Ignore`, and prove no `MultiUserChest` assembly
      reference survives.
- [ ] Then, and only then, diagnose the known issue: the hand-over branch calls
      `ForceSendZDO` + `ZDO.SetOwner` but never moves the in-use state, and the in-use branch answers
      `true` without any hand-over at all (`ContainerPatch.cs:44-59` upstream).
- [x] Licensing and docs: MIT notice and attribution in the repo and in the mod's credits, plus a
      divergence register naming every file we deviate from.

## Where the absorption landed (2026-11-27)

- 41 upstream files under `CraftFromDrawersAndChests\Chest\` (43 types), namespace `…Chest` / `…Chest.Patches`,
  with `Chest\README.md` holding the file map and the divergence register.
- `Plugin.cs`: the hard MultiUserChest dependency became `[BepInIncompatibility]`; upstream's three
  quick-move incompatibilities carried over; `ChestPatches.InitialiseLogging(Log, () => Flags.IsDebug)`
  runs before `PatchAll`, `ChestPatches.ApplyCompatPatches(Harmony)` after it.
- csproj: the MultiUserChest `<Reference>` and its net472 `AddMultiUserChestReference` workaround are gone
  (the MSB3274 warning with them); all 41 files are listed explicitly because the project does not glob.
- Build verified: `BUILD OK - 2.0.0`, 340/340 tests, no `MultiUserChest` assembly reference in the built
  DLL, and `MUC_RequestItemMove`/`MUC_Ignore`/the incompatibility GUID all present in the bytes.
- `Test-PatchTargets.ps1` now reads 46 patch classes, 43 PASS. The 3 non-PASS are conditional by design
  and are **not** regressions: `ItemDrawerCompat.UpdateItemOfDrawer` and
  `ItemDrawerCompat.CloneItemBeforeAssignmentTranspiler` target `"DrawerContainer, itemdrawers"` and are
  applied only when that mod is loaded, and `QuickStackPatch.QuickStackPatchTranspiler` targets
  `"QuickStack.QuickStackPlugin, QuickStack"` and upstream never applies it. The script has no
  "soft dependency" concept, so it reports them as failures; worth teaching it a SKIP state.
- Diagnostics now available in a normal Release build: the whole upstream trace set (package dumps, block
  traces, round-trip timings) plus `[chest] open request: requester=… player=… localUid=… zdoOwner=…
  instanceIsOwner=… ignored=… hasAccess=… inUse=… wagonInUse=… requesterIsLocal=… => <branch>`.
- Still open: the fix itself. The in-use branch still grants the open without hand-over.

## Jötunn 2.30.3 bump (2.0.1, 2026-11-28)

- Verified before changing anything: the 2.30.2 → 2.30.3 assembly diff is **3 lines, all private**
  (`PrefabManager.RegisterTerrainOp` gained an `ObjectDB` argument; `SynchronizationManager` gained a private
  `Menu_IsActive` patch for the config-window mouse-capture fix). Assembly identity and all 26 references
  unchanged; the XML docs are byte-identical in size.
- Every Jotunn symbol the mod binds is unchanged: `Main.ModGuid`, `NetworkCompatibility` +
  `CompatibilityLevel.EveryoneMustHaveMod` + `VersionStrictness.Patch`, `SynchronizationMode` +
  `AdminOnlyStrictness.Always`, `PrefabManager.Cache.GetPrefab<T>(string)` (used by the absorbed chest
  layer), `ConfigurationManagerAttributes.IsAdminOnly`.
- Bumped `packages.config`, the csproj `HintPath`, `manifest.json` (dependency **and** version), `README`,
  `REFERENCES.md`, `CHANGELOG` → **2.0.1**. Verified on the artifact: the packaged DLL references
  `Jotunn 2.30.3.0` and the packaged manifest pins `ValheimModding-Jotunn-2.30.3`.
- 340/340 tests; `Test-PatchTargets` unchanged at 43 PASS + the same 3 conditional classes; package
  `CraftFromDrawersAndChests-2.0.1.zip` (105,678 B, `sha256:E35952C9…`), no forced-debug banner.
- Not committed: the branch and the commit are the owner's.

## 2.1.0 — custody fix for the 2026-11-28 report (branch `MultiUserChestMerge`)

- Root cause (log-proven, see the 2.1.0 changelog): a chest owned by another player is excluded from the
  availability arithmetic (`ChestTake.CanServeSynchronously` = `!hasOwner || isOwner`, used at
  `CraftPatches`, `PullKeyPatches` and `PullEngine`), and the absorbed layer handed ownership to every
  requester that was not holding the container open. Opening a chest therefore changed who could count it.
- Ruling taken: **stop a second opener stealing ownership.** `ChestOpenRules.Decide` now decides on
  custody — `ZDO.GetOwner() != 0` — instead of the in-use flag: a claimed container keeps its owner
  (`GrantInUse`), and only an unclaimed one is handed over (`HandOver`). `IsInUse` survives only as a
  diagnostic field, and the `[chest]` line now prints `claimed=`, `zdoOwner=`, `inUseByOther=`.
- Accepted trade (documented in the changelog and README): a non-owner can open a claimed chest and move
  items (routed to the owner) but does not count it in their own crafting arithmetic until ownership is
  released.
- Verified: 339/339 tests (the decision table was rewritten to the new rule), `BUILD OK - 2.1.0`,
  `Test-PatchTargets` unchanged at 43 PASS + the same 3 conditional classes, package 2.1.0 (106,706 B) and a
  forced-debug 2.1.0 (106,830 B) for the two-player retest, with the release package rebuilt clean after it.
- Owed: an in-game two-player pass. Expected from the same scenario: B opening A's chest logs
  `claimed=True … => GrantInUse`, A keeps `storage 157`, and no more `ForceSendZDO`/revision churn on open.