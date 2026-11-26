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
