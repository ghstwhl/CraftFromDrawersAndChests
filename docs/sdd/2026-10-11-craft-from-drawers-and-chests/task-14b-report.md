# Task 14b report — build-piece pull key

Branch `feature/craft-from-drawers-and-chests`, commit `eba689e` ("feat: pull a building piece's materials with the pull key (Task 14b)"), parent `bf4a2e3`.

`AUTHOR-VERIFY` at the end.

---

## 1. CFC's actual build-pull behaviour, and the lines mirrored

Oracle: the local checkout of CraftFromContainers 4.0.7 at
`C:\Users\merlin\Documents\Repos\storagework\ValheimMods\CraftFromContainers\BepInExPlugin.cs`
(the same file Task 8 and Task 14 used). `UpdatePlacement_Patch` is at **1096-1125** — the brief's
citation `1096-1128` overshoots the method by three lines (1126-1128 are blank / the start of the next
class). The rest of the brief's description of it was accurate.

CFC's method, verbatim in structure:

| CFC line | What it does |
|---|---|
| 1096-1097 | `[HarmonyPatch(typeof(Player), "UpdatePlacement")] public static class UpdatePlacement_Patch` |
| 1102 | one guard line: `!modEnabled \|\| !AllowByKey() \|\| !CheckKeyHeld(pullItemsKey) \|\| !__instance.InPlaceMode() \|\| !takeInput \|\| Hud.IsPieceSelectionVisible()` → `return true` |
| 1106 | trigger: `ZInput.GetButtonDown("Attack") \|\| ZInput.GetButtonDown("JoyPlace")` — a **button-down**, not a hold |
| 1108 | `Piece selectedPiece = ___m_buildPieces.GetSelectedPiece();` |
| 1109/1123 | `selectedPiece == null` → falls through to `return false` (the press is swallowed) |
| 1111-1112 | `selectedPiece.m_repairPiece` → `return true` (vanilla keeps the press) |
| 1113 | `___m_placementGhost != null` else falls through to `return false` |
| 1115-1116 | reflects the private `Player.m_placementStatus` and pulls **only when the value is `0`** |
| 1119 | `PullResources(__instance, selectedPiece.m_resources, 0, 1);` — quality 0, multiplier 1 |
| 1123 | `return false` — the attack is **consumed**; the piece is never placed |

`PullResources` (1157-1263) pulls the **whole** requirement per resource
(`requirement.GetAmount(qualityLevel) * multiple`, 1175), walking nearby containers and taking stack by
stack; it does **not** subtract what the player already holds, and it skips upgrader resources unless
the player is at an upgrader (1188-1196). Its feedback is lines 1261-1262: `pulledMessage` is shown
**inside the per-requirement loop but outside the `m_resItem` check** — i.e. once per requirement, and
**regardless of whether anything was pulled**. There is **no failure message anywhere** on CFC's build
path.

Why the `placementStatus == 0` guard can be reached at all with the materials in a chest: CFC postfixes
`Player.HaveRequirements(Piece, Player.RequirementMode)` (`HaveRequirements_Patch`, 762+) so
`CanBuild` counts containers. `Player.UpdatePlacement` calls that check to compute its status, so with
CFC installed the ghost of a piece whose materials are in a chest still reads `Valid` (0). **That is
exactly the role this repository's `Patches/CraftPatches.cs:114` postfix plays**, which is why this
task's pull can fire at all on this codebase.

Readout: CFC's only flash patch is `InventoryGui_SetupRequirement_Patch` (662) on the **crafting**
panel. `UpdatePlacementGhost_Patch` (950) is the optional station-connection VFX, not a readout. The
build HUD shows no per-requirement amounts, so **CFC applies no readout behaviour on the build path.**

### Where the brief (and my dispatch prompt) disagreed with CFC

1. **The line range** `1096-1128` is three lines long (above). Cosmetic.
2. **Requirement 5's "failure message naming the missing items" is not parity.** CFC has no failure
   feedback on the build path at all, and its success message is unconditional and repeated once per
   requirement. I implemented Task 14's refusal path instead (a reason on a refused pull, one success
   message), because the design's top anti-goal — "refuse and say why" — is a retained requirement.
   Flagged as a **deliberate divergence**, not silently.
3. **Requirement 5's "whatever readout behaviour CFC applies here" is "none."** Nothing implemented
   there, deliberately.
4. **Requirement 3's `AllowByKey` formula is correct as quoted** (CFC 138-142 is
   `if (CheckKeyHeld(preventModKey.Value)) return switchPrevent.Value; return !switchPrevent.Value;`),
   and matches what Task 14's fix round already settled in `PullKeyRules.AllowByKey`. No disagreement.
5. Nothing else. In particular the brief's requirement 4 mechanism — the build path already having a
   storage-aware gate at `CraftPatches.cs:114` — is correct and load-bearing, not a mis-description.

## 2. The exact trigger conditions implemented

`Patches/PullKeyPatches.cs` → `UpdatePlacementPrefix(Player __instance, bool takeInput, PieceTable ___m_buildPieces, GameObject ___m_placementGhost)` on `[HarmonyPatch(typeof(Player), "UpdatePlacement")] [HarmonyPrefix]`, in CFC's order:

1. `PluginConfig.Flags == null || __instance == null || !ReferenceEquals(__instance, Player.m_localPlayer)`
   → `return true`. (The local-player term is an **addition** to CFC — see Concerns 7.)
2. `BuildPullRules.ShouldIntercept(flags.ModEnabled, KeyHeld(flags.PullItemsKey), PreventKeyHeld(flags), flags.SwitchPrevent, __instance.InPlaceMode(), takeInput, Hud.IsPieceSelectionVisible())`
   false → `return true`. This is CFC's line 1102 term-for-term; the key half is
   `PullKeyRules.ShouldPull` = `pullKeyHeld && AllowByKey(...)`.
3. `!ZInput.GetButtonDown("Attack") && !ZInput.GetButtonDown("JoyPlace")` → `return true` (CFC 1106).
4. `Piece selectedPiece = ___m_buildPieces != null ? ___m_buildPieces.GetSelectedPiece() : null;`
   (CFC 1108).
5. `BuildPullRules.Decide(selectedPiece != null, selectedPiece != null && selectedPiece.m_repairPiece, ___m_placementGhost != null, BuildPullRules.IsPlacementValid(PlacementStatus(__instance)))`
   (CFC 1109-1116):
   - `!pieceSelected` → `RefuseWithoutPull` → `return false`;
   - `repairPiece` → `LetVanillaHandle` → `return true`;
   - `!placementGhostPresent` → `RefuseWithoutPull` → `return false`;
   - `!placementValid` → `RefuseWithoutPull` → `return false`;
   - otherwise `Pull`.
6. `Pull` → `PullEngine.PullToPlayer(selectedPiece.m_resources, 0, 1, __instance, flags)` (quality 0,
   multiplier 1, exactly CFC 1119), then `flags.PulledMessage` on success or
   `FailureReason(...)` on refusal, then `return false`.

`PlacementStatus(__instance)` reflects `Player.m_placementStatus` through a cached
`AccessTools.Field(typeof(Player), "m_placementStatus")` and converts the boxed enum with
`System.Convert.ToInt32`, exactly as CFC does at 1115; **unreadable reads as `-1`**, which
`IsPlacementValid` treats as not valid.

**Verified against the shipped game IL** (`assembly_valheim_publicized.dll`, Valheim 1.0.16), with the
sanctioned probe (R26: parameter types reported):

- `Player::UpdatePlacement(Boolean, Single) : Void`; its only caller is `Player::Update`.
- `Player::UpdatePlacement -> Player::TryPlacePiece(Piece)` — `TryPlacePiece`'s **only** caller, so a
  prefix that returns false and swallows the press makes placement unreachable for that press.
- `Player::UpdatePlacement -> Player::HaveRequirements(Piece, RequirementMode)` — the exact overload
  `CraftPatches` postfixes (the `Recipe` overload is a different call site: `InventoryGui::DoCrafting`).
- `Player::UpdatePlacement -> ZInput::GetButtonDown(String)` — the game's own place trigger uses the
  same API CFC and this patch use.
- `Player::UpdatePlacement -> Hud::IsPieceSelectionVisible()`; `Player::UpdatePlacement -> PieceTable::GetSelectedPiece()`.
- Fields confirmed on `Player`: `m_buildPieces : PieceTable`, `m_placementGhost : UnityEngine.GameObject`,
  `m_placementStatus : Player/PlacementStatus` (a nested enum with `Valid = 0`, `Invalid = 1`,
  `BlockedbyPlayer = 2`, `NoBuildZone = 3`, `MoreSpace = 5`, `WrongBiome = 8`, `DeepSnow = 14`, …).
- `ZInput` is defined in **`assembly_utils`**, not `assembly_valheim`;
  `ZInput::GetButtonDown(String) : Boolean` exists there, and the literals `"Attack"`, `"JoyPlace"`
  and `"JoyAttack"` are present in that assembly's user-string heap. (A UTF-8 metadata search misses
  them — IL string literals live in the UTF-16 `#US` heap.)

**Method note (R25).** Every call-graph and definition fact above came from the sanctioned
`.superpowers/.../tools/cecil.ps1`. That tool exposes only *method* definitions and call sites, so for
the *field* facts (which fields `Player` declares, their types, and the `PlacementStatus` members) I used
a bounded, metadata-only read — `$ErrorActionPreference = 'Stop'`, no instruction iteration at all, no
enum literal tested in a loop (the R25 hazard), run in a **child** process and deleted afterwards.
Nothing was written inside the repository tree.

## 3. How items stay off the ground and a placement stays paid-for

- **No press can place a piece.** The prefix returns `false` for the `Pull` outcome and for every
  `RefuseWithoutPull` outcome; only `LetVanillaHandle` (CFC's repair-piece case) and the pre-trigger
  guards return `true`. `Player::TryPlacePiece` has exactly one caller, `UpdatePlacement`, whose body
  this prefix skips — so the intercepted press cannot place.
- **The gate is read, never weakened.** `CraftPatches.cs`'s `HaveRequirementsPiecePostfix` (line 114)
  is untouched. The status this prefix reads is that gate's own answer, computed inside the same
  `UpdatePlacement` call, so "offered on storage's strength" and "placed" cannot disagree.
- **The pull is the one Task 14 built.** `PullEngine.PullToPlayer` → `PlanFor` (whole requirement) →
  `DestinationAllows` (`DestinationCapacity.CanAccept`: matching free stack space plus empty slots,
  chunked the way `Inventory.AddItem` places; a line whose item cannot be described fails closed) →
  `Execute` → `PullRunner`. Nothing moves until every line's destination is proven, so a refusal at
  any step leaves zero items out of storage and **zero items on the ground**; the only items removed
  are ones the destination model already proved it can hold.
- **`-1` (`ChestTake.Deferred`) is untouched.** `PullRunner.Run` still treats a routed removal as
  satisfied and never as a count; `PullToPlayer` still skips sources that cannot serve
  synchronously. Nothing here sums, retries or re-interprets it.
- Storage stays keyed by prefab name throughout (no change to that path).

## 4. The prevent-key wiring

`BuildPullRules.ShouldIntercept` calls `PullKeyRules.ShouldPull(pullKeyHeld, preventKeyHeld, switchPrevent)`,
which is `pullKeyHeld && PullKeyRules.AllowByKey(switchPrevent, preventKeyHeld)`. The patch feeds it
`KeyHeld(flags.PullItemsKey)`, `PreventKeyHeld(flags)` and `flags.SwitchPrevent` — the same
`PluginConfig.Flags` snapshot, the same accessors and the same decision the recipe click
(`OnCraftPressedPrefix`), the readout (`SetupRequirementPostfix`) and `CraftPatches`' three gates already
use. So the prevent key means the same thing on every pull site: with `Switch prevent` off (default)
holding it suppresses the mod and the press is vanilla; with it on the mod only acts while it is held.

## 5. What moved into `Logic/`, and its tests

**`Logic/BuildPullRules.cs`** (new, pure — no UnityEngine/Valheim/BepInEx/Jotunn):

- `enum BuildPullDecision { LetVanillaHandle, RefuseWithoutPull, Pull }` — the three outcomes, with the
  CFC lines each corresponds to.
- `ShouldIntercept(modEnabled, pullKeyHeld, preventKeyHeld, switchPrevent, inPlaceMode, takeInput, pieceSelectionVisible)`
  — CFC's guard line 1102, delegating the key half to `PullKeyRules.ShouldPull`.
- `IsPlacementValid(int placementStatus)` — CFC's `status == 0` (the nested enum's `Valid`), pinned so
  the magic zero is a documented, tested rule rather than an inline literal; a negative value
  (unreadable field) is **not** valid.
- `Decide(pieceSelected, repairPiece, placementGhostPresent, placementValid)` — CFC's 1108-1123 branch
  order, whose disagreements are the point: repair → vanilla, everything else → swallow, placeable →
  pull.

**`Tests/Logic/BuildPullRulesTests.cs`** (new, 18 tests): all seven `ShouldIntercept` terms individually
plus the all-satisfied baseline and both `SwitchPrevent` polarities; `IsPlacementValid` for `0` (valid),
six non-zero statuses, and the unreadable `-1`; `Decide` for each of its five outcomes plus two
branch-order pins (`repairPiece` beats a missing ghost; `pieceSelected` beats `repairPiece`).

The game-facing parts stayed game-facing: the reflection for `m_placementStatus`, the Harmony field
injection, `InPlaceMode`/`IsPieceSelectionVisible`/`ZInput`, and the pull itself all read live game
state and live in `Patches/` / `Pull/`.

## 6. Build and test result lines

```
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```
(`MSB3274` for `MultiUserChest` printed as always; not touched.)

```
Test summary: total: 176, failed: 0, succeeded: 176, skipped: 0, duration: 1.3s
```

19 of the 176 are the new `BuildPullRulesTests` fixture (the earlier figure of 18 was a miscount); the
other 157 are the pre-existing suite, unchanged. 0 failing, ≥ the brief's expected 157.

## 7. Files changed

| File | Change |
|---|---|
| `CraftFromDrawersAndChests/Logic/BuildPullRules.cs` | new — the pure guard/branch decisions (140 lines) |
| `CraftFromDrawersAndChests/Patches/PullKeyPatches.cs` | modified — `UpdatePlacementPrefix`, the `PlacementStatus` reader, `FailureReason` generalised from `Recipe` to `Piece.Requirement[]`, class doc updated from three patch sites to four |
| `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` | modified — one `<Compile Include="Logic\BuildPullRules.cs" />` |
| `Tests/Logic/BuildPullRulesTests.cs` | new — 18 tests |

`Patches/BuildPullPatches.cs` was not needed: the build half is the same feature, shares
`KeyHeld`/`PreventKeyHeld`/`ShowMessage`/`Debug`/`FailureReason`, and `PullKeyPatches.cs` stays
readable at ~380 lines.

## AUTHOR-VERIFY

In game, on a world with a chest and a drawer holding the piece's materials, player carrying none:

- [ ] Equip the hammer, select a piece, hold the pull key and press attack. Expected: the piece is
      **not** placed, its materials land in your inventory, and the pulled message shows once.
- [ ] With the pull key held, confirm you **cannot** place the piece by pressing attack (repeat). Release
      the key and confirm a normal attack **does** place it — the intercept must not outlive the key.
- [ ] Hold the pull key and press attack for a piece **no** storage in range can pay for. Expected: nothing
      happens (CFC parity: the press is swallowed with no message and no pull).
- [ ] Carry part of the piece's materials (so the ghost still reads placeable from storage) and press attack
      with the pull key. Expected: either the whole requirement is pulled and the message shows, or nothing
      moves and the message names the missing items — never a partial pull, never an item on the ground.
- [ ] Fill your inventory completely, then press attack with the pull key on a piece storage can pay for.
      Expected: nothing is pulled, **nothing is on the ground**, and the message reports the room failure.
- [ ] Hold the prevent key and press attack with the pull key (default `Switch prevent = false`). Expected:
      the press is vanilla and the piece places normally, paying from your own inventory.
- [ ] Set `Switch prevent = true`, reload, and repeat both key states. Expected: the pull only happens while
      the prevent key is held.
- [ ] Open the build piece picker (so it is visible) and press attack with the pull key. Expected: the picker
      behaves normally and nothing is pulled.
- [ ] Repair piece (`m_repairPiece`, e.g. a repair-mode build): press attack with the pull key held. Expected:
      vanilla repairs; nothing is pulled.

## Self-review

- **Oracle-first.** CFC's `UpdatePlacement_Patch` and `PullResources` were read directly (local checkout),
  not reconstructed from the brief; every guard, branch and argument order is cited by line above, and the
  three places the brief drifts from CFC are reported rather than smoothed over.
- **One pull path.** The build pull is `PullEngine.PullToPlayer`; no second planner, no second take path,
  no second capacity rule. `PullEngine`, `PullRunner`, `DestinationCapacity` and `CraftPatches` are
  unchanged.
- **No weakened gate.** `CraftPatches.HaveRequirementsPiecePostfix` is untouched, and the prefix runs
  before the body that would place, so a piece can never be placed on storage's word.
- **Nothing on the ground.** The destination is proven before any take (Task 14's ordering, reused).
- **`-1` respected.** Never a count, never summed, never retried — `PullRunner` owns it.
- **Purity.** All four decisions live in `Logic/`; `Logic/**` still references no UnityEngine/Valheim/
  BepInEx/Jotunn (the Tests project compiles it with no game reference at all and passes).
- **Accessibility discipline (runtime).** Every *direct* game member this patch touches is one CFC also
  touches directly with the same API — `Player.m_localPlayer`, `InPlaceMode()`, `Hud.IsPieceSelectionVisible()`,
  `ZInput.GetButtonDown(string)`, `PieceTable.GetSelectedPiece()`, `Piece.m_repairPiece`,
  `Piece.m_resources` — so none of it depends on the publicized reference at runtime. The two fields
  `m_buildPieces` and `m_placementGhost` are taken by Harmony `___` injection (which skips visibility),
  and `m_placementStatus` by reflection, exactly as `VanillaChestSource` reaches
  `Container.CheckAccess(long)`. No direct read of a private field was introduced.
- **Left the tree clean.** No scratch scripts in the repository; probe scripts were written under
  `%TEMP%` and deleted; no push; no Nexus artefacts; no other mod's identity added.

## Concerns

1. **Feedback is not parity (deliberate).** CFC shows no reason when the build pull finds nothing, and
   prints the success message unconditionally once per requirement. This mod shows a single message: the
   pulled message on success, the missing-items/room reason on a refused pull. If exact CFC feedback is
   wanted, it is a two-line change — but it would regress the design's "refuse and say why".
2. **`placementStatus != 0` is a silent swallow** (CFC parity: no message, no pull). A player holding the
   pull key on a piece no storage can pay for gets no feedback at all. Adding `MissingMessage` there would
   be one line, but it would be a behaviour CFC does not have, so I left it as CFC has it.
3. **Whole requirement vs shortfall asymmetry (inherited from Task 14).** The placement gate is
   shortfall-based (`CraftPatches.CanReach` → `BuildRequests(..., held)`), while `PullToPlayer` asks storage
   for the **whole** requirement. A player already carrying part of the piece can therefore have a
   placeable-looking ghost and still be refused, with a message naming items they already partly hold. This
   is exactly the recipe path's behaviour and the brief pins `PullToPlayer` as the only entry point, so it is
   reported, not changed.
4. **Fail-closed when `m_placementStatus` cannot be read** (a future rename): the press is swallowed and the
   build pull stops working silently while the pull key is held. Fail-closed matches this codebase's stated
   philosophy (`DestinationAllows`: "an unprovable case fails closed") and cannot place a piece nothing paid
   for, but it does mean a renamed field is a quiet feature loss. The alternative — treat unreadable as
   valid, or hand the press back to vanilla — is a one-line switch if the author prefers it.
5. **Reflection on the trigger path.** `m_placementStatus` is read once per accepted attack press (not per
   frame), mirroring CFC's own reflection. Negligible, and only on the branch that actually pulls.
6. **Double planning on the refusal path.** `PullToPlayer` plans, then `FailureReason` plans again to name
   the shortfall; the sources are cached for the frame, so the cost is small and only on a refused pull.
   Same shape as Task 14's recipe refusal.
7. **One guard beyond CFC: `ReferenceEquals(__instance, Player.m_localPlayer)`.** CFC does not check it, and
   its `PullResources` ignores its `player` argument (it always uses `m_localPlayer`'s inventory) — so CFC
   would pull into the local player's inventory even when invoked for another `Player`. Requiring `__instance`
   to be the local player avoids pulling into a non-local inventory and avoids swallowing a non-local
   player's input. It cannot change the local player's behaviour.
8. **No build-path readout.** Deliberate: CFC's flash is the crafting panel's `SetupRequirement`, and the
   build HUD has no per-requirement readout to flash. Nothing was invented in its place.

---

# Fix round 1

Commit `49b431e` ("fix: pull only the shortfall a gate validated (Task 14b fix round 1)"), parent
`eba689e`. No Critical; the two Importants had **one root cause** and are closed by one change, plus the
two bundled Minors. Files: `Pull/PullEngine.cs`, `Patches/PullKeyPatches.cs`, `Patches/CraftPatches.cs`,
`Logic/DrawerTake.cs`, `Logic/DestinationCapacity.cs`, `Logic/BuildPullRules.cs`, `Logic/PullKeyRules.cs`
(doc), and the two test files. No new files, so no csproj change; `Logic/**` still references no
UnityEngine/Valheim/BepInEx/Jotunn.

## 1. The root cause and what changed

`PullToPlayer` planned through `PlanFor` → `BuildRequests(requirements, qualityLevel, multiplier)` — the
overload with **no held counts** (the `held: null` path) — so the pull demanded the *entire* requirement
while the gate that allowed the action was shortfall-based (`CraftPatches.CanReach` → `BuildPlan`).

R27, as implemented:

- **`PullEngine.BuildShortfallRequests(requirements, qualityLevel, multiplier, inventory)`** is the new
  request builder. It reads the player's held amounts through `DrawerTake.MatchingAmount` and passes them
  to the existing `BuildRequests(..., Func<string,int> held)`, which subtracts with the existing pure
  `PullMath.Shortfall(needed, held)`. A requirement the player already covers yields no request, so the
  whole thing is a no-op rather than a demand.
- **`PullEngine.PlanFor` now takes the `Player`** and plans that shortfall (it previously took a `Vec3`
  origin and asked for the whole requirement). It returns the collected sources via `out`, so the caller
  can hand the same list to `Execute`.
- **`PullToPlayer` gained `out bool requested`.** False means a no-op pull (no requirements, or the player
  holds every one): the press is still swallowed, but nothing is reported. Otherwise the refusal reason is
  planned from the **same shortfall**, so it can no longer name an item the player is carrying.

### The matching predicate reused (not invented)

`DrawerTake.MatchingAmount(name, quality, worldLevel, stacks)` sums the stacks that are **the same item**
as the requirement names. "Same item" is the project's existing three-part predicate — shared name by
ordinal, **quality**, world level — which was already inside `DrawerTake.MatchingFreeStackSpace` and
duplicated in `DestinationCapacity.Matches`. Rather than add a third copy, the predicate was **extracted**
to a public `DrawerTake.IsSameItem(stack, name, quality, worldLevel)`, and both of those now call it. So
the held-stock count (source half) and the delivery's free-space check (destination half) match on exactly
the same rule and cannot disagree about which stacks are the item. The requirement's identity comes from
its own item (`m_resItem.m_itemData`: `m_shared.m_name`, `m_quality`, `m_worldLevel`) — the same identity
`DestinationAllows` already builds its `Delivery` from — keyed by the requirement's prefab name, which is
what the requests and the storage layer use.

### Why this closes both Importants

1. **False refusal.** Piece needs 10 Wood, player carries 10, no storage with Wood: the shortfall is 0, so
   the plan has zero lines, `PullToPlayer` returns true with `requested == false`, and neither the pulled
   message nor "Not enough materials within range: Wood." is shown. The pull asks storage for nothing it
   was going to demand of the gate.
2. **Over-pull.** A 10 Wood + 2 Iron piece with the player carrying 2 Iron requests only 10 Wood from
   storage, and a second press re-plans from the (now larger) held counts rather than taking another full
   requirement. Storage is drained only by what was actually missing.

### The recipe path shares the fix

`OnCraftPressed` → `PullToPlayer` had the same `held: null` call and the same defect (Task 14 recorded it
as a concern). It now goes through the identical shortfall path, so a recipe's pull also asks only for the
missing ingredients and no longer over-pulls or falsely refuses a recipe the player can already pay for.

### Gate and executor share one rule

`CraftPatches.BuildPlan` (the requirement gate's planner, also used by the consume prefix) was switched
from its own `CountHeld`-name-only request build to the same `PullEngine.PlanFor`, so the gate's "yes" and
the pull's demand are planned by one call with one shortfall rule. This was the reviewer's "do not invent a
second rule" applied to the half of the pair that had drifted: the gate's *destination* half already used
the three-part match, and only its *source* half was name-only. `PullEngine.CountHeld` remains for the two
questions that are genuinely about the prefab (`HasAnyOfEach`'s "has any of this material", and the
readout's available count), which are not shortfall counts. No existing test asserted on `CraftPatches`
(it is a Harmony patch), and the full suite is unchanged and green.

## 2. CFC divergence and reasoning

CFC's `PullResources` asks storage for the whole requirement per resource
(`requirement.GetAmount(qualityLevel) * multiple`, CFC **1175** — the earlier report's `1198` was wrong)
with no held-subtraction, and prints its pulled message unconditionally once per requirement. This fix
diverges deliberately: **the shortfall is requested, not the whole requirement.** The direction is the safe
one — it matches this project's own gate, whose entire purpose is that a "yes" means the pull can deliver
what the action actually needs — and it removes the two user-visible defects CFC has. The success message
still shows (once) on a real pull; the only message removed is the one a no-op pull never earned.

## 3. Bundled Minors

- **Test coverage.** Added `ShouldIntercept_PullKeyNotHeldWithSwitchOff_IsFalse` and
  `ShouldIntercept_PullKeyNotHeldWithSwitchOn_IsFalse` (the pull key is required whichever way
  `Switch prevent` is set, including the state where the prevent gate alone would allow). More
  importantly, the **reader** half is now pinned: the value→number step was extracted to the pure
  `BuildPullRules.PlacementStatusOf(object)`, which returns the `-1` sentinel for a null field, and
  `PullKeyPatches.PlacementStatus` now calls it. Tests: `PlacementStatusOf_NullField_IsMinusOne` (plus that
  `IsPlacementValid` of the sentinel is false), `PlacementStatusOf_BoxedStatus_IsItsNumber` (a boxed enum
  converts through its underlying value, as the reflected field does) and
  `PlacementStatusOf_UnconvertibleValue_IsMinusOne`. Only the predicate side (`IsPlacementValid(-1)`) was
  pinned before; the reader is the half that would degrade silently on a field rename.
- **Resourceless piece.** A piece with null/empty `m_resources` now has no shortfall requests, so
  `PullToPlayer` returns true with `requested == false`; the patch returns false (the press is swallowed,
  as CFC does) and shows **no** message — the misleading "not enough room in your inventory" is gone,
  because the failure reason is only ever computed for a pull that had something to fetch.
- New pure tests also cover the extracted predicate: `IsSameItem_*` (name/quality/world level, ordinal
  compare, no name) and `MatchingAmount_*` (sums only matching stacks, zero for no inventory/name, and
  `MatchingAmount_IsTheMirrorOfMatchingFreeStackSpace`).

## 4. Build and test, fix round 1

```
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```

```
Test summary: total: 186, failed: 0, succeeded: 186, skipped: 0, duration: 1.1s
```

`MSB3274` for `MultiUserChest` printed as always; not touched. 10 tests added (5 in
`DrawerTakeTests`, 5 in `BuildPullRulesTests`); the pre-existing suite is unchanged. 186 ≥ the brief's
expected 176.

## 5. Still divergent from CFC

- Feedback: CFC shows no reason on a refusal and prints its success message once per requirement
  (inside its loop); this mod shows one message — the pulled message on a real pull, the missing-items/room
  reason on a refused pull, and **nothing** for a no-op pull. (Concern 1 and 2 of the original report
  stand; the no-op case is new.)
- Shortfall vs whole requirement: as in §2.
- The `placementStatus != 0` swallow remains silent on the build path (CFC parity), and the refusal
  message still names raw prefab names (`IronScrap`) — both explicitly deferred.

---

# Fix round 2

Commit `df532f2` ("fix: count a pull's held amount by shared name, not quality (Task 14b fix round
2)"), parent `49b431e`. One Critical and one Important with one root cause — the source half of the
shortfall rule — closed by splitting the two item identities. No new production file, so no csproj
change; `Logic/**` still references no UnityEngine/Valheim/BepInEx/Jotunn.

## 1. Which count feeds the source half now, and why it is a superset

Round 1's `PullEngine.HeldCounts` read the held amount with `DrawerTake.MatchingAmount(sharedName,
quality, worldLevel, stacks)` — name **+ quality + world level**, a **subset** of what vanilla
consumes. It now reads:

```csharp
held[prefab] = DrawerTake.HeldAmount(data.m_shared.m_name, stacks);
```

`DrawerTake.HeldAmount(string name, IEnumerable<InventoryStack> stacks)` (**new**, in `Logic/`) sums
every stack whose **shared name** matches by ordinal, **ignoring quality and world level**. That is
the pre-fix notion restored, in the pure stack view rather than through `m_dropPrefab`. The request's
*key* is still the prefab name (Ruling 1: what the requests and the storage layer share); only the
*amount* is counted by shared name, taken from the requirement's own item.

Why that is a superset, from the assembly (sanctioned probe, `R26` parameter types at each call
site):

| Probe output | Meaning |
|---|---|
| `Inventory::CountItems(String, Int32, Boolean) : Int32` | one definition, no overload |
| `Player::HaveRequirementItems -> Inventory::CountItems(String, Int32, Boolean)` | the requirement test's own count |
| `Player::HaveRequirements -> Inventory::CountItems(String, Int32, Boolean)` | (the piece overload, same signature) |
| `Inventory::RemoveItem(String, Int32, Int32, Boolean) : Void` | the consume identity is a **string**, not an `ItemData` |
| `Player::ConsumeResources -> Inventory::RemoveItem(String, Int32, Int32, Boolean)` | confirmed at the call site |

Vanilla's requirement test passes quality `-1` (= any) to a **name-keyed** count, and
`Player.ConsumeResources` removes by **shared name**. So any stack vanilla would count or eat has the
same shared name as the requirement; `HeldAmount` counts *every* such stack, and the only further
filters vanilla could apply (quality, world level) select a **subset** of what `HeldAmount` counts.
Hence `HeldAmount ≥ vanilla` always. Over-counting held can only ask storage for *less* — the
remainder is paid from the player's own inventory by vanilla's own consume, the safe and recoverable
direction — whereas under-counting is what produced the free craft.

**The chain now holds by construction.** Vanilla returns `true` only when its count ≥ the requirement
⇒ `HeldAmount ≥` the requirement ⇒ `Shortfall(needed, held) = 0` ⇒ `BuildRequests` emits no request
for it ⇒ `plan.Lines.Count == 0` ⇒ `PullToPlayer` returns `true` with `requested == false` ⇒ the
patch shows nothing and does **not** decline anything; and the consume prefix re-plans the same zero
shortfall, so it cannot return `false` on a path vanilla had cleared. The two postfixes' `__result ==
true` early-returns (`CraftPatches.cs:84`, `:119`) are therefore no longer reachable with an inflated
shortfall, because there is no inflated shortfall. `BuildPlan` in `CraftPatches`
already routes through the same `PullEngine.PlanFor`, so the gate and the pull are one rule, as in
round 1 — only the source half of that rule changed.

## 2. The property test that pins it

`Tests/Logic/HeldAmountTests.cs` (**new**, 6 tests) pins the invariant instead of asserting it. It
generates inventories from a deterministic `Random` (`System.Random`, fixed seeds — reproducible):
0–6 stacks, shared names drawn from `{ "$item_wood", "$item_stone", "$item_iron" }` (with an
occasional `null` name), **qualities 0–4**, **world levels 0–2**, stack sizes that are mostly one
material stack (20) or a double one (30) and sometimes one, stack contents from 0 to `maxStack`, so
partial, full and empty stacks and same-name/different-quality mixed stacks all occur.

1. `HeldAmount_IsNeverBelowVanillasCount_ForAnyIdentityVanillaCouldUse` — 3000 iterations. Vanilla is
   modelled *purely and faithfully* by `VanillaCountItems(name, quality, matchWorldLevel,
   worldLevel, stacks)`, mirroring `Inventory.CountItems(String, Int32, Boolean)`: ordinal name,
   quality `-1` = any, `matchWorldLevel` false = any. For each generated state it is run over **every**
   identity vanilla could be using — quality `-1` or the requirement's own × world level ignored or
   pinned — and the test asserts `HeldAmount ≥ VanillaCountItems`. It then asserts the **chain**:
   whenever `VanillaCountItems ≥ needed`, `PullMath.Shortfall(needed, HeldAmount) == 0`.
2. `HeldAmount_DominatesTheStrictThreePartCount_ThatWasTheRegression` — 3000 iterations asserting
   `HeldAmount ≥ DrawerTake.MatchingAmount(name, quality, worldLevel, stacks)` for generated
   `(name, quality, worldLevel)`. `MatchingAmount` is the round-1 rule, so this is the regression
   pinned generally rather than by example.
3. `HeldAmount_CountsASameNameStackAtADifferentQualityAndWorldLevel` — the exact free-craft state
   (`HeldAmount == 12` while `MatchingAmount == 5`), plus `IncludesFullAndEmptyStacks`,
   `MatchesTheNameByOrdinal` and the null/empty contract.

The property exercises the **production** function: `PullEngine.HeldCounts` calls this very
`DrawerTake.HeldAmount` with the requirement's shared name (the `Pull/` half is the only part that
cannot be linked into the test assembly, and all it does is supply the argument).

## 3. Only one held notion survives

Three sites count "held" and there is now **one rule** — *the item, ignoring quality and world level*:

| Site | Function | Identity |
|---|---|---|
| shortfall (gate **and** pull, one call) | `PullEngine.HeldCounts` → `DrawerTake.HeldAmount` | **shared name**, quality/world level ignored |
| crafting readout (`PullKeyPatches.cs:263`) | `PullEngine.CountHeld(inventory, prefab)` | **prefab name**, quality/world level ignored |
| `HasAnyOfEach` (`CraftPatches.cs:337`) | `PullEngine.CountHeld(inventory, prefab)` | **prefab name**, quality/world level ignored |

The two readers differ only in the key (`shared name` in the pure stack view, `prefab name` on the
live `Inventory`) and each is the key its own caller already needs; the *notion* — count the material,
ignore quality and world level — is identical, so Minor 5 is moot. **No third rule survives.**
`DrawerTake.MatchingAmount` (name + quality + world level) has **no production caller**: it is
retained deliberately, per the brief, as the strict mirror of `MatchingFreeStackSpace` (both match on
`IsSameItem`, pinned by `MatchingAmount_IsTheMirrorOfMatchingFreeStackSpace`) and as the reference
subset the property test above proves `HeldAmount` dominates. Its doc, `IsSameItem`'s doc,
`DestinationCapacity.Matches`'s doc and the two stale test comments now say explicitly that it is the
**destination** identity and must not be used for the source half; `CountHeld`'s doc says the same
from the other side.

## 4. The Minors

- **Stale doc.** `PullEngine.BuildRequests`'s "Pass null to request the full requirement (the 'pull
  the whole recipe' case)" is gone; it now reads "A null `held` simply reads as 'carries none' …
  production callers always supply the shortfall count (`BuildShortfallRequests`), so there is no
  longer a 'pull the whole recipe' path." The defensive null branch stays (it means "holds none").
- **The reader's real degradation path.** `PullKeyPatches.PlacementStatus` no longer checks
  `PlacementStatusField == null` itself: the resolution step and the value step are both the new pure
  `BuildPullRules.PlacementStatusFrom(bool fieldResolved, object fieldValue)`, which returns the `-1`
  sentinel when the field did not resolve (or the value will not convert). Two tests pin it:
  `PlacementStatusFrom_UnresolvedField_FailsClosed` (a renamed field ⇒ `-1` ⇒ `IsPlacementValid`
  false ⇒ the press is swallowed, and a stray value alongside it is ignored) and
  `PlacementStatusFrom_ResolvedField_ReadsItsValue`. `PlacementStatusOf(null)`'s test is kept as the
  value-shape contract with its comment corrected to point at the real path.
- **Redundant assertion.** The second assertion of each guard test is gone.
  `ShouldIntercept_PullKeyNotHeldWithSwitchOff_IsFalse` now keeps only
  `preventKeyHeld: false, switchPrevent: false` (the prevent gate *allows* there, so the pull key is
  what refuses); `ShouldIntercept_PullKeyNotHeldWithSwitchOn_IsFalse` keeps only
  `preventKeyHeld: true, switchPrevent: true` (the gate *allows* there too). The removed pair both
  had the prevent key alone refusing.

**Deferred, untouched:** the lost no-op feedback (`PullKeyPatches.cs:109-110`, `:208-209`) — no
message added.

## 5. Build and test, fix round 2

```
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```

```
Test summary: total: 194, failed: 0, succeeded: 194, skipped: 0, duration: 1.6s
```

`MSB3274` for `MultiUserChest` printed as always; not touched. 8 tests added (6 in the new
`HeldAmountTests`, 2 in `BuildPullRulesTests` at net zero from the removed assertions); the
pre-existing suite is unchanged. 194 ≥ the brief's expected 186.

Files: `Logic/DrawerTake.cs`, `Logic/DestinationCapacity.cs` (doc), `Logic/BuildPullRules.cs`,
`Pull/PullEngine.cs`, `Patches/PullKeyPatches.cs`, `Patches/CraftPatches.cs` (comment), `Tests/Logic/
{DrawerTakeTests,BuildPullRulesTests}.cs`, and the new `Tests/Logic/HeldAmountTests.cs`. Nothing in
`Logic/` gained a dependency; the new test file is in the SDK-style test project (globbed), and no new
production file was needed, so neither csproj changed. No scratch scripts; `.references` and
`.superpowers` remain ignored; nothing pushed.

## 6. Concerns

1. **`MatchingAmount` is now test-only in production.** Kept on the brief's explicit "do not delete
   it", and it does real work in the guarantee (the subset the superset is proven against), but a
   reader looking for its caller will find only tests. If the author prefers a smaller surface it can
   be deleted without touching the source half.
2. **The `shared name` vs `prefab name` key split is unchanged.** `HeldAmount` counts by
   `m_shared.m_name` while `CountHeld`, the requests and the storage layer key by prefab name. They
   agree for every droppable material this mod pulls; the brief allowed either, and the shared-name
   form is what could be tested purely. A stack with no drop prefab still counts towards the
   shortfall (the safe direction) but not towards the readout.
3. Everything else from the round-1 report's Concerns stands (feedback divergence from CFC, the
   shortfall-vs-whole-requirement asymmetry, the silent `placementStatus != 0` swallow, the
   `m_localPlayer` guard, no build-path readout).
