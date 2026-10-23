# GitHub release — 1.0.5

Tag: `v1.0.5` (annotated, object `8fb09c8`) · Target: `main` · Peeled to commit `141315e`
Tag commit = package commit: this release has **no docs-only commits** on top of it (unlike `v1.0.1`–`v1.0.4`).
Asset: `Release/CraftFromDrawersAndChests-1.0.5.zip` (65,563 bytes,
`sha256:eb265c35aa40ed6f51ae2b31f3aab7fe639849f26e677728f727d956a83a6f01`) — also copied to
`Release/CraftFromDrawersAndChests-release.zip`, byte-identical.
Title: **Craft From Drawers And Chests 1.0.5**

## What this version is

A bug fix, reported from play on 2026-10-23: feeding `BoneFragments` to a `piece_shieldgenerator` pulled
nothing from nearby storage, its hover had no fill-all line, and the fill-all key only ever added one item.

**Cause, established from the shipping assembly** (`cecil.ps1` / `dump-api.ps1` over
`.references/Valheim/1.0.20/Managed/assembly_valheim.dll`): `Smelter` and `ShieldGenerator` are the **only**
two types in `assembly_valheim.dll` that own an `OnAddFuel` — the call-site scan finds exactly two, in
`Smelter.Awake` and `ShieldGenerator.Start` — and this mod hooked only the smelter's. The generator's fuel is
added by the private `OnAddFuel(Switch, Humanoid, ItemData)` wired into its add-fuel `Switch` (`Interact`
passes a null item, `UseItem` the held one); `ShieldGenerator.Interact` is the **attack** action, not fuel.
Vanilla's add removes one item and adds exactly one fuel (`RPC_AddFuel` → `SetFuel(GetFuel()+1)` →
`RPC_SetFuel` → `ZDO.Set(s_fuel, …)`), which is why an unpatched generator adds a single item per press
however the modifier is held. Its hover is the private `OnHoverAddFuel()`, wired into `Switch.m_onHover`.

**The fix** (`CraftFromDrawersAndChests/Patches/StationPatches.cs`): a prefix on `ShieldGenerator.OnAddFuel`
with the smelter fuel patch's guards and origin (the **generator**'s position), and a postfix on
`OnHoverAddFuel` appending the `[shift+Use] … max` hint using the generator's own `m_add` action word. One
structural difference from a smelter: the generator declares a **list** of fuels (`m_fuelItems`) rather than a
single `m_fuelItem`, so the request walks that list with the helpers the smelter's ore patch already uses
(`InventoryHasInput` / `PullFirstInputs` / `FirstInputInInventory`). `GetFuel()` is **private** in the shipping
assembly (reflected once, failing closed); `m_fuelItems`, `m_maxFuel`, `m_add`, `m_defaultFuel` are public;
`m_nview` is private, hence Harmony's `___m_nview` injection. Gated on the existing `Pull fuel` setting and
`Fuel disallow types` list — **no new configuration surface**, and CraftFromContainers 4.0.7 has no
shield-generator patch that could have supplied a fourth setting.

Two things deliberately carried over unchanged: range is measured from the station (as for a fire or a
smelter), and an Adventure Backpacks backpack remains **not** a storage source — by the owner's standing
requirement — and Adventure Backpacks opens its contents only inside a crafting action, which a fuel switch
is not. Backpack fuel is usable by neither mod on this path; documented in the release body.

## Readiness at publication

- `dotnet test` **262/262**; `tools/Build.ps1 -Configuration Debug` and `-Configuration Release` both
  `BUILD OK - CraftFromDrawersAndChests 1.0.5` (MSB3274 expected). Release DLL 86,016 bytes — distinct from
  the 84,480-byte 1.0.2/1.0.3/1.0.4 binaries, as the version string and the new patch code both changed.
- Packaged `manifest.json` reads `1.0.5`; packaged DLL SHA256 equals `bin\Release\CraftFromDrawersAndChests.dll`
  (`e01592d5c299d1c37ce0a69caeb3d92b52795914973b75db8a87bf501c9e3598`); its ASCII scan contains
  `ShieldGenerator` and the `1.0.5` attribute string, and `RPC_AddFuel` is present as a UTF-16 literal (the
  `#US` heap — an ASCII-only scan of a string literal is a false negative).
- **Confirmed in game by the owner on 2026-10-23**, Valheim **1.0.20** (`Modheim` profile session at 21:53,
  log line 139 onward): `Loading [CraftFromDrawersAndChests 1.0.5]`, the game reports
  `Valheim version: 1.0.20 (network version 41)`, **`Harmony patched 24 game method(s)`** — the list now
  contains `ShieldGenerator.OnAddFuel` and `ShieldGenerator.OnHoverAddFuel` between `Player.UpdatePlacement`
  and `Smelter.OnAddFuel` — and the session logged **0 errors**. The owner confirmed the fix works in game.
- `author-verify.md` gained a seven-step "1.0.5 — shield generator feeding" section (one unit from a chest,
  the hint, fill-all to capacity, range-from-the-generator, `Pull fuel` off, the 24-patch log line, and the
  backpack-is-not-a-source pin). The checks beyond the owner's confirmation are not individually recorded in
  the log, so they stand as the remaining owner pass.

## Published

- **GitHub release published 2026-10-23** at the owner's instruction:
  <https://github.com/ghstwhl/CraftFromDrawersAndChests/releases/tag/v1.0.5> — verified from the public API:
  `draft = false`, `prerelease = false`, `tag_name = v1.0.5`, `target_commitish = main`, one asset
  `CraftFromDrawersAndChests-1.0.5.zip`, **65,563 bytes**, `state = uploaded`, `digest =
  sha256:eb265c35…` equal to the local zip. Published as the repository's latest release.
- Release notes: `release-1.0.5-body.md` (the GitHub body, passed with `--notes-file`).

## Still to do

- **Thunderstore**: state unknown — the web tools are unavailable in this environment, so this record could
  not check it. The 1.0.3 notes recorded Thunderstore serving **1.0.1**, and 1.0.4 was never uploaded as far
  as this record knows. A Thunderstore version is immutable, so upload `CraftFromDrawersAndChests-1.0.5.zip`
  only once the version number is certain.
- Run the remaining `author-verify.md` 1.0.5 steps against the released build (the multiplayer and `Pull fuel`
  off paths in particular). Any failure goes to `/investigate`, not to a patch from a guess.
- Tell players that the server and every client must move to **1.0.5** together — the declared plugin version
  changed from 1.0.4, and the connection is refused on a mismatch.
