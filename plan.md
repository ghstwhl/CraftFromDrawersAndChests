# CraftFromDrawersAndChests — "nearby but blocked" hint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development or
> superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a crafting requirement is not covered because the only storage holding the item is
*switched off* or *locked*, say so — instead of showing the same `0/1` as "you have none".

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
   hinted — "not a source" is not "locked".
2. **Signal on the requirement row:** **colour + suffix** — the amount text keeps its
   `available/required` numbers, is tinted amber, and gains a marker (`" *"` by default).
3. **Second signal:** **yes** — one HUD line naming the item and the reason, shown when the crafting
   panel shows a recipe with a blocked shortfall, once per panel open.

## Global Constraints

- **Pulls must not change.** A blocked storage stays unusable; the blocked list is a read-only
  diagnostic and must never reach `Plan`/`Execute`. (Same rule as
  `StorageRegistry.CollectIncludingSwitchedOff`, which this plan reuses in spirit.)
- `Logic/**` must not reference UnityEngine/Valheim/BepInEx/Jötunn.
- The numbers in the readout must not change: `Resource string` still formats
  `available`/`required`, so a CraftFromContainers-style `{0}/{1}` reads identically.
- Legacy csproj: a **new `.cs` file in the product project must be added to `<Compile>`**, or the
  build silently omits it. Prefer adding types to existing files; one new `Logic/` file is planned
  because the Tests project links `Logic/**` by glob while the product project needs one csproj line.
- TDD: failing test first (`/tdd`), then the change.
- No publishing. Version for this build: **1.0.11** (1.0.10 stays the tested 1.0.21-baseline build).

## Design

**Pure rule — `Logic/AvailabilityHint.cs`** (new):

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

`ShouldHint` is `available < required && blockedStock > 0` — a *partially* blocked shortfall still
explains why the count looks wrong, which is the whole point.

**Why is this source blocked? — `Logic/Eligibility.cs`:**

```csharp
public enum StorageBlockReason { None, SwitchedOff, NoAccess }
public static bool TryBlockReason(in StorageCandidate c, Vec3 origin, float range,
    bool chestsEnabled, bool drawersEnabled, ChestTypeRules rules, out StorageBlockReason reason);
```

True **only** when the storage is in range, of an enabled kind, well-formed as storage, and blocked
by exactly one of the two lock reasons (switch checked first). Out-of-range / loose / ignored chest
types / null inventory return false. A drawer can only be `SwitchedOff` (drawers carry no access
check in this mod).

**Collection — `Storage/StorageRegistry.cs`:**

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

**Readout — `Patches/PullKeyPatches.cs::SetupRequirementPostfix`:**

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
- [x] Re-run `dotnet test` → green (298).

### Task 2: The block reason

- [x] Add `StorageBlockReason` + `Eligibility.TryBlockReason` to `Logic/Eligibility.cs`.
- [x] Extend `Tests/Logic/EligibilityTests.cs`: off drawer → `SwitchedOff`; off chest → `SwitchedOff`
      (switch wins over access); accessible-but-denied chest → `NoAccess`; out-of-range, drawers
      disabled, chest outside a piece, ignored chest type → `false`; an eligible chest → `false`.
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

- [x] `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` →
      `References: … (Valheim 1.0.21)`, `BUILD OK - CraftFromDrawersAndChests 1.0.11 (Debug)`.
- [x] `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → all green (308).
- [x] Version bump: `ModInfo.Version` and `manifest.json` → `1.0.11`.
- [x] `-Configuration Release` → `Release\CraftFromDrawersAndChests-1.0.11.zip` (70,857 bytes).
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
  *informative*, not an error — the hint never claims the item is reachable.
- **Cost.** One extra collection per unanswered requirement per frame, cached per frame like the
  others, and skipped entirely when the requirement is covered.
- **Not** planned: hinting out-of-range storage, hinting config exclusions, or a per-storage icon.
