# Task 15b — fill-all key and console command (report)

Repo: `C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests`
Branch: `feature/craft-from-drawers-and-chests` · base HEAD `ab50bfc`
Oracle: `.references\Mods\CraftFromContainers\BepInExPlugin.cs` (CraftFromContainers 4.0.7, plain C#).
No assembly was decompiled. Every assembly fact below came from the sanctioned probe
(`.superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\tools\cecil.ps1`) run against the
**unpublicized** `.references\Valheim\1.0.16\Managed\assembly_valheim.dll` (and, for the two gui
types, `assembly_guiutils.dll` / `gui_framework.dll`).

---

## 1. Audit of the uncommitted draft — what was wrong, what I completed

The draft was an **unverified, non-compiling** intermediate. What I found:

| # | Finding | Severity | Action |
|---|---|---|---|
| A1 | **The tree did not compile.** `StationPatches` renamed `PullOne(...)` → `PullFromStorage(..., int amount)` and widened `PullFirstInputs(...)` with an `amount` parameter, but left **two stale call sites**: the cooking-station fuel patch still called `PullOne(...)` (a method that no longer exists), and the cooking-station food patch still called `PullFirstInputs(...)` with 5 arguments. | blocking | Fixed both to `PullFromStorage(..., 1)` / `PullFirstInputs(..., 1)` — CFC reads no fill-all modifier in either cooking patch (see §2), so one item is correct. |
| A2 | **`Terminal.m_input` is `gui_framework.GuiInputField`**, not `UnityEngine.UI.InputField`, and `gui_framework.dll` was not referenced → `CS0012: The type 'GuiInputField' is defined in an assembly that is not referenced`. This is exactly where the previous worker stopped ("Now register the new files…") — it never got past the first build. | blocking | Added a `gui_framework` reference to the csproj (see §5). `GuiInputField` is `public` in the shipped, unpublicized `gui_framework.dll` and derives from `TMPro.TMP_InputField`, whose `Text` property is the one read. |
| A3 | **Both new files were unregistered** in the legacy csproj's explicit `<Compile>` items: `Logic\ConsoleCommandRules.cs` and `Patches\TerminalPatch.cs`. | silent (build/tests stay green, product assembly omits them) | Registered both; verified on the built DLL (§5). |
| A4 | `Logic\ConsoleCommandRules.cs` and `Patches\TerminalPatch.cs` had **no tests**, and `StationFeedRules.FillAmount` / `FillAllHint` had none either. | requirement gap | Added 23 tests (§6). |
| A5 | The draft's fill-all gating, hint postfixes, command parse and status report were otherwise **correct against CFC** and against the constraints — audited line by line against the oracle, not rewritten. | — | Kept; citations verified (§2, §3). |

Everything else in the draft (the `PullKeyPatches.KeyHeld` visibility change, the `FillAmount`
arithmetic, the `PullFromStorage`/`PullFirstInputs` shapes, the command parse/report) survived audit.

---

## 2. `fillAllModKey` — real semantics from CFC, and every gated call site

**Binding (CFC 4.0.7).** `fillAllModKey` is a `ConfigEntry<string>` (CFC line 41) bound in section
`"Hot Keys"`, key `"FillAllModKey"`, default `"left shift"` (line 112). This project's
`ConfigModel.FillAllModKey` (`"Fill all mod key"`, default `left shift`, already bound in
`PluginConfig.Init`/`ReadInto`) mirrors it.

**Polarity and trigger.** CFC reads it as a **held modifier**, once per patch entry point:
`bool pullAll = CheckKeyHeld(fillAllModKey.Value);` (lines **278**, **464**, **580**), where
`CheckKeyHeld(string value, bool req = true)` (line **150**) is `Input.GetKey(value.ToLower())` and
returns `false` on a bad key. So: **held = fill all**; unbound/unrecognised = not held. This project's
`StationPatches.FillAllHeld(flags)` uses the very same read — `PullKeyPatches.KeyHeld(...)`, which also
lower-cases and swallows an unknown key — so all three held modifiers are read by one implementation.

**Where it applies (CFC reads `pullAll` in exactly three patches and nowhere else):**

| # | Call site (this project) | Gate / amount | CFC lines |
|---|---|---|---|
| 1 | `StationPatches.FireplaceInteractPrefix` | amount `pullAll ? Min(m_maxFuel − CeilToInt(fuel), stack) : 1`; **also lifts** the "player already carries the fuel" guard | read **278**; guard `inventory.HaveItem(...) && !pullAll` **280**; amount **317** |
| 2 | `StationPatches.SmelterOnAddOrePrefix` | amount `pullAll ? Min(m_maxOre − GetQueueSize(), available) : 1`; **does not** widen the "player already carries an input ore" pre-check | read **464**; guard `(!AllowByKey() && !pullAll)` **465**; pre-check `return true` whatever `pullAll` **471-482**; amount **503** |
| 3 | `StationPatches.SmelterOnAddFuelPrefix` | amount `pullAll ? Min(m_maxFuel − GetFuel(), stack) : 1`; **also lifts** the "player already carries the fuel" guard | read **580**; guard `(!AllowByKey() && !pullAll)` + `inventory.HaveItem(...) && !pullAll` **582**; amount **624** |

**Where it does NOT apply (verified):** CFC's cooking-station patches
(`CookingStation_OnAddFuelSwitch_Patch` lines 348-385, `CookingStation_FindCookableItem_Patch`
lines 388-427) never mention `pullAll`; its turret patch (lines 1129-1156) does not either. So the two
cooking patches and the turret keep amount `1` — which is what the two compile-fix call sites above
now pass. The two cooking patches still pass `1` and are otherwise untouched.

**Hover hint.** CFC appends the modifier to the **smelter's** fuel and ore hovers only:
`Smelter_OnHoverAddFuel_Patch` (lines 429-441: gates `modEnabled`/`pullFuel`, then
`fillAllModKey.Value?.Length > 0`, line **436**) and `Smelter_OnHoverAddOre_Patch` (lines 444-456:
`modEnabled`/`pullOre`, then the same key check, line **451**), each appending
`"\n[<color=yellow><b>{key}+$KEY_Use</b></color>] $piece_smelter_add max"` (**438**) /
`… {__instance.m_addOreTooltip} max` (**453**). Implemented as `StationFeedRules.FillAllHint(key, action)`
+ the two `StationPatches` postfixes; the fireplace, cooking and turret get none, as in CFC.

**Two deliberate, documented divergences (both in `StationPatches` class remarks):**
1. CFC lets the modifier **override the prevent key** on the two smelter patches
   (`!AllowByKey() && !pullAll`, lines 465/582) but **not** on the fireplace (line 280). This port keeps
   the prevent gate authoritative everywhere — a held prevent key always wins.
2. CFC's fireplace fill-all consumes from the **player's own stack** first (line 289) and only then tops
   up from containers; this port delivers into the player's inventory and lets vanilla consume one per
   interaction, so the fetched stack is fed over the following interactions. Same end state (station to
   max), no items on the ground, and the planner asks storage only for the shortfall.

---

## 3. The console command (`Patches\TerminalPatch.cs`)

**Shape mirrored from CFC.** CFC's `InputText_Patch` (CFC lines 1497-1512) prefixes
`Terminal.InputText`, gates on `modEnabled` (line **1503**), matches **one whole lower-cased line**
`text.ToLower().Equals("craftfromcontainers reset")` (line **1506**), and on a match
`context.Config.Reload(); context.Config.Save();` (1508-1509), echoes the typed line
`__instance.AddString(text)` (1510), prints `"Craft From Containers config reloaded"` (1511) and
returns `false` (1512) so the line is consumed. This port uses the same game method, the same
echo-then-answer output, and the same "consume a matched line" return.

**Name.** `ModInfo.ConsoleCommand` = `craftfromdrawersandchests` (named after the mod, like CFC) plus
`ModInfo.ConsoleCommandAlias` = `cfdac`. Matched case-insensitively (a superset of CFC's `ToLower().Equals`).

**Subcommands.** CFC's single subcommand `reset` implies a command token + subcommand form, so:
- `status` — this project's own report (§4). CFC has no equivalent.
- `reset` — CFC's meaning: reload the config (and save it, as CFC does), then re-read it into the
  shared `PluginConfig.Flags` snapshot (this port reads configuration through the snapshot, so without
  the re-read a reload would not take effect), then `StorageRegistry.Clear()`.
- no subcommand, or an unknown one → the usage text (`ConsoleCommandRules.Help`), never a silently
  consumed line.

**Output.** The typed line is echoed first (as CFC's line 1510 does), then the answer:
- `status` → the report of §4;
- `reset` → `ConsoleCommandRules.ResetMessage`:
  `"Config reloaded; storage tracking cleared. Containers re-join the tracked set as they re-awake, so it stays incomplete until they do."`
  — the `StorageRegistry.Clear()` caveat ("for tearing a world down … rather than for routine use")
  preserved in the command's own output **and** in its help text, not hidden;
- otherwise → the usage, which also repeats the re-awake caveat.

**Deliberate divergence:** CFC gates the whole command on `modEnabled` (line 1503); this port does not,
so the report stays readable precisely when something is misconfigured. Every other console line is
returned `true` and left to vanilla.

---

## 4. How the status report counts sources and reports the drawer API

`ConsoleCommandRules.StatusReport(playerLoaded, chestCount, drawerCount, drawerApiResolved)` (pure) is
fed by `TerminalPatch.StatusReport()` from the live world:

- `Player.m_localPlayer` is the origin. With no player (main menu / dedicated server) the count is
  reported as **`unknown (no player is loaded)`** rather than a lie of `0`, and the drawer line is
  still reported because it is a property of the loaded assemblies, not of the world.
- The counts come from **`PullEngine.Collect(origin, PluginConfig.Flags)`** — the pull engine's own
  eligible-source list for that exact origin — and are split by `IStorageSource.Kind` into chests and
  drawers. So the number the player sees is the number a pull would actually use, not a second estimate
  that could disagree with one.
- The drawer line is **`DrawerApi.IsAvailable`** (`resolved` / `not resolved (chest-only)`).
- Negative counts are clamped at zero, and each count is pluralised.

---

## 5. csproj registration and the built-DLL check

### 5a. Registration
Added to `CraftFromDrawersAndChests\CraftFromDrawersAndChests.csproj`:
- `<Compile Include="Logic\ConsoleCommandRules.cs" />`
- `<Compile Include="Patches\TerminalPatch.cs" />`
- a `<Reference Include="gui_framework">` pointing at
  `$(ReferencesRoot)\Valheim\$(ValheimGameVersion)\Managed\gui_framework.dll` (see §1 A2).

### 5b. Registered-file audit (every product `.cs` vs the csproj)
All `.cs` under `CraftFromDrawersAndChests\**` excluding `bin\`/`obj\`, compared with the
`<Compile>` list:

```
On disk (product) .cs count: 36
Declared Compile count: 36
--- on disk but NOT declared ---   (none)
--- declared but NOT on disk ---   (none)
```

A 36-to-36 bijection — no product file is omitted, and no declared file is missing.

### 5c. Built-DLL verification (the registration is proven on the artefact, not in the file)
Probe run against `CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll`:

```
public  ConsoleCommandRules::TryParse(String, String, String, ConsoleSubcommand&) : Boolean
public  ConsoleCommandRules::ParseVerb(String) : ConsoleSubcommand
public  ConsoleCommandRules::IsCommand(String, String, String) : Boolean
public  ConsoleCommandRules::StatusReport(Boolean, Int32, Int32, Boolean) : String
public  ConsoleCommandRules::Help(String, String) : String
private TerminalPatch::StatusReport() : String
private TerminalPatch::InputTextPrefix(Terminal) : Boolean
private TerminalPatch::Reset(Terminal) : Void
internal PullKeyPatches::KeyHeld(String) : Boolean
private StationPatches::FillAllHeld(FeatureFlags) : Boolean
private StationPatches::AppendFillAllHint(String&, String, PullSetting) : Void
private StationPatches::SmelterOnHoverAddFuelPostfix(String&) : Void
private StationPatches::SmelterOnHoverAddOrePostfix(Smelter, String&) : Void
private StationPatches::PullFromStorage(Player, Vector3, String, ItemDrop, FeatureFlags, Int32) : Boolean
private StationPatches::PullFirstInputs(Player, Vector3, List`1, String, FeatureFlags, Int32) : Boolean
public  StationFeedRules::FillAmount(Boolean, Int32) : Int32
public  StationFeedRules::FillAllHint(String, String) : String
```

`ConsoleCommandRules` (and the `ConsoleSubcommand` enum, visible as its second parameter type) and
`TerminalPatch` are present in the product assembly, with the new `StationFeedRules` /
`StationPatches` members. Had the two files been left unregistered, none of the first eight lines
would exist.

---

## 6. What went into `Logic/` (with tests)

Pure, no UnityEngine/Valheim/BepInEx/Jötunn:

- **`Logic\ConsoleCommandRules.cs`** (new) — `ConsoleSubcommand` enum; `TryParse` (line → command +
  subcommand, case-insensitive, whitespace-tolerant); `IsCommand`; `ParseVerb`; `StatusReport`;
  `Help`; `ResetMessage`. **17 tests** in `Tests\Logic\ConsoleCommandRulesTests.cs` (new).
- **`Logic\StationFeedRules.cs`** (extended) — `FillAmount(fillAll, remaining)` (`0` when full,
  `remaining` when the modifier is held, else `1`) and `FillAllHint(fillAllKey, action)` (CFC's exact
  markup, `null` when the key is unbound). **6 tests** added to `Tests\Logic\StationFeedRulesTests.cs`.

Game-facing (not pure): `Patches\TerminalPatch.cs` (new), `Patches\StationPatches.cs`,
`Patches\PullKeyPatches.cs`.

---

## 7. Complete visibility table — every vanilla member/field touched

Probed against the **unpublicized** 1.0.16 assemblies. R29/R30: only members `public` there are called
directly; private/protected ones are *patch targets* (Harmony resolves them by name — a patch target is
not a direct call), never invoked directly.

| Member | Kind | Visibility (unpublicized) | How 15b touches it |
|---|---|---|---|
| `Terminal::InputText()` | method | **protected** | Harmony prefix target (by name) |
| `Terminal::AddString(String)` | method | **public** | direct call |
| `Terminal::m_input` | field | **public** (`GuiInputField`) | direct field read |
| `gui_framework::GuiInputField` | type | **public** (base `TMPro.TMP_InputField`) | referenced type |
| `TMPro.TMP_InputField::text` | property | public (Unity/TMP) | direct property read |
| `Smelter::OnHoverAddFuel()` | method | **private** | Harmony postfix target |
| `Smelter::OnHoverAddOre()` | method | **private** | Harmony postfix target |
| `Smelter::m_addOreTooltip` | field | **public** | direct field read |
| `Smelter::OnAddOre(Switch, Humanoid, ItemData)` | method | **private** | Harmony prefix target |
| `Smelter::OnAddFuel(Switch, Humanoid, ItemData)` | method | **private** | Harmony prefix target |
| `Smelter::m_maxOre`, `m_maxFuel`, `m_fuelItem`, `m_conversion` | fields | **public** | direct field reads |
| `Fireplace::Interact(Humanoid, Boolean, Boolean)` | method | **public** | Harmony prefix target |
| `Fireplace::m_maxFuel`, `m_fuelItem` | fields | **public** | direct field reads |
| `CookingStation::OnAddFuelSwitch(Switch, Humanoid, ItemData)` | method | **private** | Harmony prefix target |
| `CookingStation::OnInteract(Humanoid)` | method | **private** | Harmony prefix target |
| `CookingStation::m_maxFuel`, `m_fuelItem`, `m_requireFire`, `m_conversion` | fields | **public** | direct field reads |
| `Turret::UseItem(Humanoid, ItemData)` | method | **public** | Harmony prefix target |
| `Turret::GetAmmoType()` | method | **public** | direct call |
| `Turret::GetAmmo()` | method | **public** | direct call |
| `Turret::m_maxAmmo` | field | **public** | direct field read |
| `Player::m_localPlayer` | field | **public** (static) | direct field read |
| `Humanoid::GetInventory()` | method | **public** | direct call |
| `Inventory::HaveItem(String, Boolean)` | method | **public** | direct call |
| `Inventory::GetItem(String, Int32, Boolean)` | method | **public** | direct call |
| `Inventory::CountItems(String, Int32, Boolean)` | method | **public** | direct call |
| `Localization::instance` | property | **public** (static, `assembly_guiutils`) | direct property read |
| `Localization::Localize(String)` | method | **public** (`assembly_guiutils`) | direct call |

Unchanged 15a reflections (not re-checked here beyond confirming they are still private):
`Smelter.GetQueueSize/GetFuel`, `CookingStation.GetFuel/HaveDoneItem/GetFreeSlot/IsFireLit`,
`Player.m_placementStatus` — all reflected, correct by R29/R30.

**No direct call to any non-public vanilla member was introduced by 15b.** The only non-public members
in the table are Harmony patch targets.

---

## 8. Build and test results

```
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug
  … warning MSB3274 (expected, MultiUserChest 4.7.2 vs net462 — left untouched)
  CraftFromDrawersAndChests -> …\bin\Debug\CraftFromDrawersAndChests.dll
  Built …\CraftFromDrawersAndChests.dll (78,848 bytes)
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)

dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj
  NUnit3TestExecutor discovered 228 of 228 NUnit test cases
  Test summary: total: 228, failed: 0, succeeded: 228, skipped: 0, duration: 1.2s
```

228 ≥ 205 expected; the 23 new tests are the ones added in §6.

---

## 9. Files changed

| File | Change |
|---|---|
| `CraftFromDrawersAndChests\CraftFromDrawersAndChests.csproj` | register `Logic\ConsoleCommandRules.cs` + `Patches\TerminalPatch.cs`; add `gui_framework` reference |
| `CraftFromDrawersAndChests\Logic\ConsoleCommandRules.cs` | **new** — pure command parse/report/help |
| `CraftFromDrawersAndChests\Logic\StationFeedRules.cs` | add `FillAmount`, `FillAllHint` |
| `CraftFromDrawersAndChests\Patches\TerminalPatch.cs` | **new** — console command patch |
| `CraftFromDrawersAndChests\Patches\StationPatches.cs` | fill-all gating + hover hints; rename `PullOne`→`PullFromStorage(…, amount)`; **fix two stale call sites** |
| `CraftFromDrawersAndChests\Patches\PullKeyPatches.cs` | `KeyHeld` private→internal (shared read) |
| `Tests\Logic\ConsoleCommandRulesTests.cs` | **new** — 17 tests |
| `Tests\Logic\StationFeedRulesTests.cs` | +6 tests for `FillAmount`/`FillAllHint` |
| `.superpowers\sdd\2026-10-11-craft-from-drawers-and-chests\task-15b-report.md` | **new** — this report |

Committed **locally only**. No push was performed (this environment pushes automatically; that is not
something I triggered).

---

## 10. AUTHOR-VERIFY (in-game; not run here)

1. **Fill-all, fireplace.** In a world, put fuel (e.g. Wood) in a chest next to a fireplace. Hold
   **Left Shift** and press E on the fireplace: the interaction asks the chest for the fire's whole
   remaining capacity and takes as much of it as the chest can supply **into your inventory** — so the
   fire itself still advances one fuel per interaction, consuming the fetched stack over the following
   presses (see Fix round 1 §F; station delivery is Task 15c). Release Shift and press E again: exactly
   one fuel moves. Repeat while also carrying fuel — with Shift held the interaction still tops up;
   without Shift it consumes your own item only. With a chest that holds *less* than the remaining
   capacity, the held key must still take that part (and never less than the released key would).
2. **Fill-all, smelter.** Same on the smelter's add-ore and add-fuel switches. Confirm the smelter
   **hovers** show the `[left shift+$KEY_Use] … max` line only when the mod is enabled and *Pull ore* /
   *Pull fuel* are on, and that the fireplace, cooking station and turret show no such hint.
3. **Non-applicability.** Confirm a cooking station (fuel switch and food) and a turret still move one
   item per interaction while Shift is held.
4. **Command.** Open the console (F5): `cfdac` → usage; `cfdac status` → "Sources in range of the
   player: N (x chests, y drawers)" plus the "Item Drawers API:" line; `cfdac reset` → config reloaded
   + tracking cleared + the re-awake caveat. Confirm an unrelated line (`help`) is passed through
   untouched, and that a mistyped subcommand shows the usage rather than being swallowed.
5. **Reset caveat.** After `cfdac reset` mid-session, confirm pulls stop finding storage until the
   containers re-awake (e.g. after a zone reload), exactly as the message warns.
6. **Dedicated server / main menu.** `cfdac status` with no player loaded reports
   `unknown (no player is loaded)` and still reports the drawer API.

---

## 11. Self-review

- Re-read the full diff against the oracle line by line; every CFC citation in the code comments and in
  §2/§3 was re-derived from `BepInExPlugin.cs`, not from the draft's own claims.
- The two compile errors were found by actually building; the artefact (not the file) was then used to
  prove registration.
- Registration audited exhaustively (36/36 bijection), not sampled.
- Every vanilla member touched was probed, with emphasis on the newly touched set; the only non-public
  ones are Harmony patch targets.
- No scratch script left in the tree (probe helpers were written under `$env:TEMP` and removed).
- No `Container` inventory and no drawer ZDO `"Amount"` is written by any 15b code; removal still goes
  only through `MultiUserChestBridge.RemoveFromChest` / `ForceRemove`. `-1` never enters the amounts
  here (`FillAmount` returns `0`/`remaining`/`1`).

## 12. Concerns

1. **All-or-nothing interaction with fill-all.** When the modifier is held and storage cannot cover the
   whole requested amount, the planner takes nothing, so the interaction falls back to the player's own
   item — strictly less than CFC, which tops up from each container in turn. This is a consequence of
   the existing engine's all-or-nothing sequencing, not of 15b, but it is user-visible: holding Shift
   with a nearly-empty base can pull *fewer* items than not holding it. Flagged for the author.
2. **Fireplace fill-all distribution differs from CFC** (documented divergence §2.2): the fuel is
   delivered to the player's inventory and consumed one per interaction, rather than RPC'd into the
   fire immediately. Correct and ground-safe, but slower to fill.
3. **`gui_framework` is a new reference** (unpublicized game assembly). It is now copied next to the
   other game assemblies in `bin\Debug`, consistent with the existing `assembly_valheim_publicized` /
   `assembly_guiutils_publicized` references. If the release staging must contain only the mod DLL,
   confirm the packaging target does not sweep `bin\*`.
4. **The command is intentionally not gated on `modEnabled`** (documented divergence §3). If the author
   prefers strict CFC parity, add the gate.
5. **Hint text is not localized at the source** — it is handed to `Localization.instance.Localize`
   exactly as CFC does, so `$KEY_Use` and the smelter's tooltip token resolve at display time; the key
   name itself (e.g. "left shift") is shown verbatim, as in CFC.

---

# Fix round 1 — the fill key inverted on any shortfall (R31)

Commit **`e1e0850`** on `feature/craft-from-drawers-and-chests` (parent `b413a40`). **Local only, not
pushed.** 11 files, +474/-38. Nothing around the fill key was touched: the binding, the polarity, the
three gated call sites (fireplace, smelter ore, smelter fuel) and the *deliberate* absence on the two
cooking patches and the turret, the hover hints, the command parse/echo/output and its `reset` caveat,
and the csproj registration (no new files were added this round, so the 36/36 bijection is unchanged).

## F1. What was wrong

`StationPatches` asked for `FillAmount(fillAll, remaining)` — the station's *whole* remaining capacity —
built one `Piece.Requirement` from it and sent it through `PullEngine.PullToInventory`, whose planner is
all-or-nothing: `PullPlan` discarded the whole plan on a single shortfall (`PullEngine.PullToInventory`
returned before `DestinationAllows`/`Execute`), and the patch ignored that result and returned `true`.
With the key held and storage short of `capacity − held`, nothing moved and vanilla found nothing in
hand: **holding the key fuelled a fire 0 where releasing it fuelled 1.**

## F2. How the fill is planned now

A fill is not a craft (R31), so it is planned as a fill and not as a fixed amount:

| Layer | Change |
|---|---|
| `Logic\PullPlan.cs` | `Plan(..., bool fillUpTo = false)`. A **fixed-amount** request is byte-for-byte the old plan: any shortfall discards it. A **fill** request accepts `min(available, requested)` — it keeps the lines the walk found, records nothing as `MissingItemNames`, and stays feasible even when storage had nothing at all (zero lines). |
| `Logic\DestinationCapacity.cs` | `AcceptableAmount(stacks, emptySlots, delivery, limit)` — how much the destination can *actually* accept, i.e. the largest `n ≤ limit` for which `CanAccept` holds. Bisected **through** `CanAccept` rather than re-deriving its chunking, so the two cannot drift (the predicate is monotone in the amount: taking less can only need less room). |
| `Pull\PullEngine.cs` | New `PullToInventoryFill`, sharing one private core with `PullToInventory`, so the craft pull and the fill cannot diverge. The core builds the shortfall requests, **caps each to `AcceptableAmount`** when filling, plans with `fillUpTo`, and still runs `DestinationAllows` over the resulting lines before `Execute`. |
| `Patches\StationPatches.cs` | `PullFromStorage`/`PullFirstInputs` take the modifier's own flag and pick `PullToInventoryFill` when it is held; the five single-item call sites (smelter ore/fuel are the other two fill sites; cooking fuel, cooking food, turret pass `false`) keep the old all-or-nothing plan exactly as before. |

Why it can no longer pull less than the released interaction, in both directions the amount can fall
short:

1. **Storage is the constraint.** The fill request is the whole remaining capacity, but the plan only
   claims what storage holds. Storage can supply part ⇒ that part is taken; storage can supply none ⇒
   zero lines, which `PullToInventory` reports as a **no-op** (`true`, `requested = false`) — exactly
   what the released interaction does with empty storage — rather than as a refusal. So the held key is
   never worse than the released one, and both are the vanilla interaction when there is nothing to
   pull.
2. **The destination is the constraint.** Capping the request to `AcceptableAmount` is what keeps the
   bar when the *inventory*, not storage, is short: an uncapped fill for the whole capacity would be
   refused wholesale by `DestinationAllows` whenever the inventory had room for some but not all of it,
   moving nothing while the released one-item request moved its item. With the cap the fill asks for
   `min(capacity, room)` and the destination proof still covers whatever is taken.

Preserved as required: the `DestinationAllows` destination proof for every line actually taken; the
shortfall basis (storage is still asked only for the part the player does not already carry); the
existing `IStorageSource.Take` paths, `PullRunner`'s three answer shapes and the `-1`
(`ChestTake.Deferred`) rule (untouched); and the station-capacity guard (`FillAmount` still returns 0
for a full station, so a full station is never fed and never costs the player stock). **Nothing may be
pulled that cannot be delivered** still holds: a fill's lines are capped to what the destination has
room for and are still proved by `DestinationAllows` before the first `Take`.

## F3. The test that covers the refusal path

The refusal lives in the planner, so the tests are there — and they are the ones that would have
caught the inversion:

- `Tests\Logic\PullPlanTests.cs` — `Fill_PartlyCoveredRequest_TakesWhatIsAvailable` (a request for 5
  with 2 in stock now yields **one line of 2** instead of an infeasible empty plan),
  `Fill_StoreCannotCoverAnyOfIt_IsEmptyButFeasible` (zero stock ⇒ feasible with zero lines, i.e. the
  no-op the unmodified interaction gives), `Fill_AcrossSources_TakesEveryOneOfThem` (2 + 3 of a 9
  request ⇒ lines of 2 and 3), and `Fill_NeverTakesMoreThanItAskedFor`.
- `Fill_HoldingTheFillKeyNeverPullsLessThanReleasingIt` — **the minimum bar as a test.** It composes the
  station's own request (`StationFeedRules.FillAmount`) with the planner for every storage amount
  0..6 × remaining capacity 1..6 and asserts the held amount ≥ the released amount, i.e. it generates
  the 42 comparisons in which the old code delivered 0 against the released 1. This is the "no test can
  catch #1" gap: round 1's `StationFeedRulesTests` pinned `FillAmount`'s arithmetic but never fed it to
  the planner.
- `Tests\Logic\DestinationCapacityTests.cs` — `AcceptableAmount_IsTheMatchingFreeSpacePlusOneStackPerEmptySlot`
  (matching room + one stack per empty slot), `_IsCappedByTheRequest`, `_IsZeroWhenNothingCanBePlaced`,
  and `AcceptableAmount_IsTheLargestAmountCanAcceptTakes`, which checks the bisection against a full scan
  with `CanAccept` over 5 inventories × 4 empty-slot counts × 46 limits (920 comparisons) — that also
  pins the monotonicity the bisection relies on.
- `Tests\Logic\StationFeedRules.cs` and `ConsoleCommandRulesTests` gained one doc/one behaviour test
  each (see F4/F5).

## F4. The tooltip, and the `AUTHOR-VERIFY` wording

Both promised an effect the code cannot produce — vanilla's `Interact` still advances **one** item per
interaction, so a successful fill merely pre-loads the player's inventory.

- `PluginConfig.cs`, `fillAllModKey`: was *"Held key that fills a station to its maximum instead of a
  single item."* now **"Held key that asks storage for a station's whole remaining capacity instead of a
  single item, taking as much of it as the storage can supply."** It describes what the key *requests*,
  not where the items end up, so it is true both before and after 15c.
- `AUTHOR-VERIFY` step 1 was corrected in place (above): the fire's remaining capacity is fetched into
  the inventory and the fire then consumes it one fuel per interaction — no longer "the fire fills
  toward its maximum" — plus a new sub-step for the partial base.
- `PullPlan`/`StationFeedRules`/`StationPatches` remarks gained the fill-vs-fixed-amount distinction, so
  the code's own documentation does not promise station delivery either.

**Task 15c will revisit the wording** once delivery into the station exists: at that point the key
genuinely fills the station and the tooltip can say so, and the destination cap in F2 becomes moot for
the station path (the items no longer land in the player's inventory).

## F5. The null-guard fix

`TerminalPatch.StatusReport()` passed `PluginConfig.Flags` straight into `PullEngine.Collect`
(`StorageRegistry.Collect` dereferences `flags.MaxRange`/`flags.EnableChests`), while `Reset` guarded
the same snapshot. It now reads the snapshot once and guards it exactly as `Reset` does; with a null
snapshot (or no player) it reports the counts as unknown instead of throwing inside the prefix, and
`ConsoleCommandRules.StatusReport` gained an `unknownReason` (default: the old "no player is loaded")
so the report names *which* piece was missing rather than blaming the player. Unreachable today, as the
brief says, but the two methods are now consistent. One new test pins the custom reason; the four
existing `StatusReport` messages are unchanged.

## F6. Deferred divergences (documented, not fixed)

| # | Divergence | Where | Why it stays |
|---|---|---|---|
| D1 | **Delivery into the player's inventory, not the station.** A successful fill pre-loads the inventory and vanilla consumes one per interaction, so the station reaches its maximum over the following interactions rather than in one. | Task 15c (scheduled by the controller) | 15c owns true fill-to-max parity; the fill request, the planner and the destination proof here are already the shape it needs. |
| D2 | **The prevent key is authoritative everywhere.** CFC lets `pullAll` override `AllowByKey()` on the two smelter patches (CFC 465/582, `!AllowByKey() && !pullAll`); the fireplace never did (280). | `StationPatches` class remarks, `Enabled` | The port's gate is stricter and uniform: a held prevent key always wins. Behaviour kept deliberately; only listed here for durability. |
| D3 | **The console command is not gated on `modEnabled`.** CFC gates `InputText_Patch` on it (CFC 1503). | `TerminalPatch.cs:44-48` | The report is meant to be readable exactly when something is misconfigured; the command consumes only lines that name this mod. |
| D4 | **Destroyed-source skip, leave-one, by-distance, drawer handling** (round 1 §2) | unchanged | Not this round's scope; listed so the divergence set stays in one place. |

## F7. R29/R30

**No new vanilla member or field is touched.** `CapToDestination` reuses only members the project
already reads (`ItemDrop.ItemData.m_shared`/`m_quality`/`m_worldLevel`, `Piece.Requirement`), all
`public` in the unpublicized 1.0.16 assembly and already exercised by `DestinationAllows`. No probe was
run because no new member was introduced; the sanctioned probe remains the only decompiler.

## F8. Build and test

```
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug
  … warning MSB3274 (expected, MultiUserChest 4.7.2 vs net462 — left untouched)
  CraftFromDrawersAndChests -> …\bin\Debug\CraftFromDrawersAndChests.dll
    Built …\CraftFromDrawersAndChests.dll (80,384 bytes)
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)

dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj
  NUnit3TestExecutor discovered 238 of 238 NUnit test cases
  Test summary: total: 238, failed: 0, succeeded: 238, skipped: 0, duration: 1.4s
```

238 ≥ 228 (round 1) and ≥ 205 (expected); the +10 are F3/F5's new tests. No scratch or inspection
script was written to disk; no `Container` inventory and no drawer ZDO `"Amount"` is written by this
round (the fill still removes only through `IStorageSource.Take`); `-1` never enters an amount.

