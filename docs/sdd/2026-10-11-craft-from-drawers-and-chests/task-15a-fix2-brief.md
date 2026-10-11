# Task 15a — fix round 2

Round 1 correctly reflected the six named members, cached them in `static readonly` fields and made
every guard fail closed, and all four bundled doc corrections are accurate. One Important remains: the
R29 sweep for this file was **incomplete**, so a fresh instance of the same defect survives.

## Important — two more direct calls to `private` vanilla methods

`StationPatches.cs:254` and `:418` call these directly:

- `Smelter::FindCookableItem(Inventory)` — `:254`
- `CookingStation::FindCookableItem(Inventory)` — `:418`

**Visibility settled by the sanctioned probe against `.references\Valheim\1.0.16\Managed\assembly_valheim.dll`
(what the game loads). Do not re-derive:**

| Member | Visibility in the unpublicized assembly |
|---|---|
| `Smelter::FindCookableItem(Inventory) : ItemData` | **`private`** |
| `CookingStation::FindCookableItem(Inventory) : ItemData` | **`private`** |
| `Fermenter::FindCookableItem(Inventory) : ItemData` | `private` (unused by us) |
| `Inventory::GetItem(String, Int32, Boolean) : ItemData` | **`public`** |
| `Inventory::HaveItem(String, Boolean) : Boolean` | **`public`** |

Both calls bind against the publicized reference, are unresolvable in the shipping assembly, and the
direct `call` would raise `MethodAccessException` out of the prefix on `Smelter.OnAddOre` /
`CookingStation.OnInteract`. Both are reachable — each runs after the earlier guards.

### R30 — prefer removing the dependency over reflecting it

**Do not reflect these.** A public equivalent exists and CraftFromContainers already uses it: CFC's
`Smelter_OnAddOre_Patch` never calls `FindCookableItem` — it tests the player's inventory with the
**public** `Inventory.GetItem(<shared name>, -1, false) != null` over the station's `m_conversion` list
(CFC `BepInExPlugin.cs:470-481`). Reproduce that for both the smelter and the cooking station, and mirror
CFC's equivalent for the cooking guard.

`m_conversion` is **`public`** on both `Smelter` and `CookingStation` (verified), so this removes the
non-public dependency entirely and shrinks the direct-access surface — strictly better than caching a
`MethodInfo` for a private member.

**Do not call `Fermenter::FindCookableItem`** — it is private too. If any code path wants a fermenter,
use the same public-API shape.

## Minor to fix (both from the same review)

- **`Resolve` is not wrapped in try/catch** (`:90-93`). A throw there — `AmbiguousMatchException`, for
  instance — surfaces as `TypeInitializationException` out of a prefix, bypassing the fail-closed design
  entirely. Wrap it and return `null` on throw.
- **The typed helpers log nothing on an unexpected return type** (`TryInvokeInt/Float/Bool`, `:128-158`).
  A right-name/wrong-signature lookup would therefore disable the feature *silently* — the one case where
  fail-closed is indistinguishable from "the member is absent". Add a one-shot log so the two failure
  modes stay distinguishable.

## Field reads — already settled, one left

I verified every station field this file reads directly, against the unpublicized assembly, and **all are
`public`** — no field needs reflecting:

`Fireplace/CookingStation/Smelter::m_maxFuel`, `Smelter::m_maxOre`, `Turret::m_maxAmmo`,
`Turret::m_ammoType`, `SharedData::m_ammoType`, `Smelter/CookingStation/Fireplace::m_fuelItem`,
`Smelter/CookingStation::m_conversion`, `CookingStation::m_requireFire`.

For reference, the convention this confirms is real rather than theoretical:
**`Player::m_placementStatus` is `private`** — which is precisely why `PullKeyPatches.cs:64` reads it via
`AccessTools.Field`.

**Still to check:** `ZDOVars.s_fuel` (`:203`) — I did not probe it. Check it with
`tools\cecil.ps1 -Method s_fuel -Fields`; if it is not public, use the public overload
`zdo.GetFloat("fuel", 0f)` that CFC itself uses (CFC `:302`) instead of the hash constant.

## Deferred — do not fix

Nothing new. The previously deferred items stand: the duplicate `InputItems` overloads, `PullOne`'s
log-only `prefab` parameter, and the `RemainingCapacity` restatement test.

## Scope and verification

Everything that bound Task 15a still binds: `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; `Logic/**`
free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`; **never write a `Container`'s inventory or a drawer's
ZDO `"Amount"`**; storage keyed by **prefab name**; `-1` (`ChestTake.Deferred`) never a count, never
summed, never retried; **items must never end up on the ground and nothing may be pulled that cannot be
delivered**; new files registered in the legacy csproj's explicit `<Compile>` items; no scratch or
inspection scripts in the tree; no Nexus artefacts; `MSB3274` expected and untouched; `fillAllModKey` and
the console command remain **Task 15b**; **do not push to any remote** — something in this environment
repository owner pushes commits in batches; commit locally and never push.

**R25/R29:** assembly inspection goes through `tools\cecil.ps1` only, never an ad-hoc probe. It now reports
**visibility for both methods (`-Definitions`) and fields (`-Fields`)** — so *every* member this file
touches must be checked with it, not just the ones a review names. That incompleteness is what this round
is correcting.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 205 or more)
3. Commit, then append a **Fix round 2** section to `task-15a-report.md`: the public-API replacement for
   both `FindCookableItem` guards and the CFC lines mirrored; the `Resolve` try/catch; the return-type
   log; `ZDOVars.s_fuel`'s verified visibility and what you did if it is not public; and a **complete
   visibility sweep** — every vanilla member and field this file now touches, with its visibility in the
   unpublicized assembly — so the class is demonstrably closed rather than sampled.

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report
path. Do not narrate your work in the reply.
