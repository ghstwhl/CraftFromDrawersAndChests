# Task 12 — Per-storage switch — Report

**Status:** DONE_WITH_CONCERNS (in-game step not runnable here; one brief premise documented as wrong)
**Branch:** `feature/craft-from-drawers-and-chests`
**Commit:** `1786216` — *feat: per-storage pull switch on the ZDO* (parent `4198333`)
**Files committed (5):**
- `CraftFromDrawersAndChests/Logic/ToggleRules.cs` (new)
- `CraftFromDrawersAndChests/Toggle/StorageToggle.cs` (new)
- `CraftFromDrawersAndChests/Toggle/TogglePatches.cs` (new)
- `Tests/Logic/ToggleRulesTests.cs` (new)
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` (modified: +3 `<Compile>`, +`UnityEngine.InputLegacyModule` reference)

## 1. Scratch-file cleanup
Deleted all eleven throwaway scripts before anything else:
`tools/_inspect_azu.ps1`, `tools/_inspect_harmony.ps1`, `tools/_inspect_hover.ps1` … `tools/_inspect_hover7.ps1`, `tools/_inspect_patchflow.ps1`.
`tools/` now holds only `Build.ps1`, `New-ValheimRefs.ps1`, `README.md`, `.cache/`.
`git show --name-only HEAD` contains no `_inspect_*` file, and the working tree is clean (`git status --porcelain -uall` empty).

## 2. Rulings compliance
- **Ruling 1 — ZDO key constant lives in `ConfigModel`.** `StorageToggle` declares **no** `ZdoKey` string; it derives `public static readonly int ZdoHash = ConfigModel.ZdoKey.GetStableHashCode();` (`ConfigModel.ZdoKey == "CFDAC_NoPull"`). Tasks 9/10 read the same constant. ✔
- **Ruling 2 — `StorageToggle` owns only `Flip`, the hotkey and the hover text.** Reading the flag stays in `VanillaChestSource.ToggledOff` / `DrawerSource`; nothing was re-plumbed. ✔

## 3. What was verified (not assumed)
The prior agent built but never tested/committed/reported, so every patch target was re-derived from IL with Mono.Cecil (run in a child `powershell` process; sources were read-only caches, nothing was locked).

`assembly_valheim_publicized` 1.0.16:
- `Container.GetHoverText () -> System.String` — `public virtual final newslot` (patchable; postfix with `ref string __result`).
- `Hoverable` — **interface**; `GetHoverText () -> String`.
- `Player.Update () -> Void` — `public nonvirtual` (patchable); `Player.GetHoverObject () -> UnityEngine.GameObject` — `public virtual`.
- `ZDO.Set (System.Int32, System.Boolean)` and `ZDO.GetBool (System.Int32, System.Boolean)` exist.
- `ZNetView.ClaimOwnership () -> Void` — `public`.
- `Hud.UpdateCrosshair (Player, System.Single)` — exists (the sole producer of hover text).

Item Drawers, **both** shipped versions on this machine
(`…\com.kesomannen.gale\cache\KG-ItemDrawers\1.4.0\…\kg_ItemDrawers.dll` and
`…\Thunderstore Mod Manager\…\KGvalheim-ItemDrawers\1.0.9\kg_ItemDrawers.dll`):
- `kg_ItemDrawers.DrawerComponent : MonoBehaviour, Interactable, Hoverable`.
- `GetHoverText () -> System.String` — `public virtual final newslot`, i.e. an **implicit** interface implementation named exactly `GetHoverText`, so `AccessTools.Method(type, "GetHoverText")` resolves it and a postfix applies at runtime.

### Drawer hover path — brief premise is wrong; what is kept instead
The brief said the drawer hover is reached from `Player.Update` via `FindApiContainerView`. That does not hold for this version: hover text is produced by `Hud.UpdateCrosshair`, which does `GetComponentInParent<Hoverable>()` then interface-dispatches `Hoverable.GetHoverText()`. A drawer is **not** a `Container`, so `Container.GetHoverText` never fires for it; its text comes from `DrawerComponent.GetHoverText`. Because `Hoverable` is an interface there is no single method covering both, so `TogglePatches` uses **two** hover patches: a postfix on `Container.GetHoverText` (chest) and `DrawerHoverTextPatch`, a runtime `[HarmonyTargetMethod]` patch on `kg_ItemDrawers.DrawerComponent.GetHoverText` guarded by `[HarmonyPrepare]` (skipped entirely when Item Drawers is absent — soft dependency, no compile-time type). `Player.Update` is used **only** for the hotkey, and the hovered storage is resolved by `DrawerApi.IsDrawer`, never by assuming a `Container`. This is **kept as the implementation**; it is the concern to validate in game (see AUTHOR-VERIFY).

### Hotkey safety
`Toggle storage key` binds to `string.Empty` (`PluginConfig.cs:134`). `StorageToggle.IsToggleKeyPressed()` first calls `ToggleRules.IsKeyConfigured` (null/empty/whitespace → false) and wraps `Input.GetKeyDown(key)` in try/catch, so an empty or unrecognised key is "not held" and never throws. `PlayerUpdatePostfix` also ignores every player but `Player.m_localPlayer`, so a remote player's `Update` can never flip anything locally.

### World-shared, ownership, scope
`Flip` calls `view.ClaimOwnership()` **before** `zdo.Set(ZdoHash, !zdo.GetBool(ZdoHash, false))`; the value lives on the object's own ZDO, so it is shared and save-persisted. Only `CFDAC_NoPull` is written — Item Drawers' own automation is untouched.

## 4. Bug found and fixed
**`ToggleRules.HoverSuffix` was inverted.** It returned `" (pull: on)"` when `off == true` and `" (pull: off)"` when `off == false`; since the caller passes `StorageToggle.IsOff(view)`, every *normal* storage would have read "pull: off". The colour constants were already named correctly (red `#FF5555` = off, green `#55FF55` = on) — only the ternary was reversed, and the two tests had been written to match the wrong code. Fixed the ternary and corrected `HoverSuffix_Off_SaysPullOff` / `HoverSuffix_On_SaysPullOn` to assert the true mapping.
Pure decision logic stays in `Logic/ToggleRules.cs` (`HoverSuffix`, `IsKeyConfigured`), so all of it is unit-tested; `Toggle/**` holds only the game-facing glue.

## 5. Build & tests
- Build: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
  → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`. `MSB3274` (MultiUserChest is 4.7.2, project is 4.6.2) printed as expected and left unfixed.
- Tests: `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
  → **102 total, 0 failed, 102 succeeded** (94 baseline + 8 new `ToggleRules` tests).

## 6. Constraints
`net462` / BepInEx 5.4.2350 / Jötunn 2.30.2 honoured. `Logic/**` references none of `UnityEngine`, `Valheim`, `BepInEx`, `Jötunn`. `Toggle/**` is game-facing. All three new source files are explicit legacy-style `<Compile>` items. No Nexus/other-mod identity or URL introduced. Nothing pushed.

---

## AUTHOR-VERIFY (brief Step 4 — in-game, not runnable from here)
1. Bind `Toggle storage key` in **2 - Keys** (it ships empty, so the hotkey does nothing until set).
2. Stand in front of a chest; confirm the hover shows `(pull: on)` in green.
3. Press the key while hovering; confirm it flips to `(pull: off)` in red, and that the chest is then skipped as a pull source (Tasks 9/10).
4. Repeat for a **drawer** — this is the case the brief's premise got wrong and the one most worth confirming: does the drawer's hover line gain the suffix and flip on the key? (Two hover patches are used because a drawer is not a `Container`.)
5. With a **second player**: they must see the same state on the same chest/drawer.
6. Restart the server and re-check: the state must survive (ZDO persisted).
7. Negative test: leave `Toggle storage key` empty and confirm nothing throws while hovering/pressing keys.
