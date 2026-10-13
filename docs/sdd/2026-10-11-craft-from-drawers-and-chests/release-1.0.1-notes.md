# GitHub release draft — 1.0.1

Tag: `v1.0.1`  ·  Target: `main` — its tip when you publish; the 1.0.1 code commit is `52429fa`, with
`a71179c` (a `.gitignore` change, not part of the package) on top
Asset: `Release/CraftFromDrawersAndChests-1.0.1.zip`

Title: **Craft From Drawers And Chests 1.0.1**

---

Craft and build straight out of your storage: chests **and** KG Item Drawers drawers within one range of
you, instead of only from your inventory.

This is the first public release — `1.0.0` was packaged locally but never published, so all of it is new
to Thunderstore and GitHub.

## Requirements

- **BepInEx** 5.4.2350 (BepInExPack_Valheim)
- **Jötunn** 2.30.2 — required, and provides the version enforcement below
- **MultiUserChest** 0.6.2 — required; all chest reads and removals go through it, so two players can use
  one chest at once without desync
- **KG Item Drawers** 1.4.0 — optional; without it the mod runs chest-only
- **Adventure Backpacks** 2.3.x — optional; with it, materials in your equipped backpack count

**Uninstall CraftFromContainers and AzuCraftyBoxes first.** This mod replaces them and refuses to load
alongside either.

**Every player and the server must install this mod at the same version** — the connection is refused
otherwise.

## What it does

- Craft, build and feed stations from nearby chests and drawers, on one range setting (default 10 m;
  `0` or less means unlimited, matching CraftFromContainers).
- Hold **Left Ctrl** and click a recipe or a placement to pull its ingredients into your inventory.
- Hold **Left Shift** at a fireplace, smelter or cooking station to ask storage for the station's whole
  remaining capacity instead of a single item.
- Press **X** while hovering a chest or drawer to switch that storage off for everyone, permanently, saved
  on the object. Its hover text reports the state.
- Materials in an equipped **Adventure Backpacks** backpack count while crafting and building, under that
  mod's own rules — nothing is moved into your inventory to make it work.
- All-or-nothing: if the whole requirement cannot be secured, nothing is consumed, so a craft is never paid
  for by nothing.

## Changes in this release

Both are since the unpublished `1.0.0`:

- **The per-storage toggle now defaults to `X`** instead of `Left Shift + E`. The old default collided with
  Item Drawers' own "withdraw single item" hotkey, which is really **LeftShift + E** — its hover text calls
  it "Left Alt + E", but its code reads KeyCode 304, and in Unity's enum 304 is LeftShift while 308 is
  LeftAlt — so on a drawer one press did both. `X` is read by neither the game, nor Item Drawers
  (`E`, LeftShift), nor Adventure Backpacks (`I`, `Y`), and it is not a movement key.
- **Debug logging is off by default.** It was writing this mod's diagnostics into every player's log.

## Notes

- **Existing configs keep their values** — BepInEx preserves a setting once written, so a changed default
  only applies to a fresh config file. If your toggle key is bound to a movement key (`W`, `A`, `S`, `D`),
  change it: the toggle applies to whatever storage is under your crosshair, so a movement key switches
  storage off silently as you walk.
- The on-demand ctrl-click **pull** counts your inventory, not your equipped backpack; crafting and building
  do count the backpack.
- With Adventure Backpacks' *Leave One Item In Backpack* enabled, one item of each type is deliberately
  left uncounted **and** unconsumed, so 25 carried reads as 24. That is that mod's rule, reported faithfully.
- Drawer support is pinned to KG Item Drawers 1.4.0's ZDO contract and RPCs. If they change, drawer pulling
  degrades to chest-only rather than failing.

Full history in [CHANGELOG.md](CHANGELOG.md).
