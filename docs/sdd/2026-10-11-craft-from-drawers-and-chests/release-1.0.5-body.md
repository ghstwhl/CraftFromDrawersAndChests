Craft and build straight out of your storage: chests **and** KG Item Drawers drawers within one range of you, instead of only from your inventory.

**Install from Thunderstore:** <https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/> — the package is also attached below for a manual install.

A bug-fix release: **shield generators now take their fuel from your storage**, like every other fuelled station.

## Fixed

- **Feeding a `piece_shieldgenerator` pulls the fuel from nearby chests and drawers.** Before this release its add-fuel switch behaved exactly as vanilla: one `BoneFragments` per press, taken from your inventory, with nothing pulled from storage and no effect from the fill-all key. The generator was the one fuelled station this mod did not patch — `Smelter` and `ShieldGenerator` are the only two types in the game assembly that own an `OnAddFuel`, and only the smelter's was hooked.
- **The fill-all key fills a generator in one press.** Hold it (`left shift` by default) and use the switch to fill the generator to its capacity instead of feeding one item at a time.
- **The fill-all hint is shown on the generator's add-fuel hover**, the same `[shift+Use] … max` line the smelter's hovers carry, so the key is discoverable there too.

Two things are deliberately unchanged:

- **Range is measured from the generator**, not from you — same rule as a fire or a smelter. A chest beside the generator feeds it while you stand back; a chest beside *you* but outside the generator's range does not.
- **A generator is gated on the same settings as a fire or a smelter**: the `Pull fuel` setting and the `Fuel disallow types` list. With `Pull fuel` off, it behaves exactly as vanilla did. There is no new setting.

> ⚠️ **Every player and the server must update together.** The declared plugin version changes from `1.0.4` to `1.0.5`, and this mod refuses a connection whose version does not match — as Jötunn's enforcement is configured to do.

## Verified

- **In game by the owner on 2026-10-23**, Valheim **1.0.20**, single player (host): the plugin loaded as **1.0.5**, all **24** Harmony patches applied — including the two new `ShieldGenerator` targets — and the session logged **0 errors**.
- `dotnet test` 262/262; the packaged `manifest.json` reads `1.0.5`, and the packaged DLL is byte-identical to the release build.
- The patch targets were checked against the shipping assembly, not assumed: `OnAddFuel(Switch, Humanoid, ItemData)` and `OnHoverAddFuel()` are both `private` there (so the mod patches them by name and reflects the private `GetFuel()`), and the generator holds a *list* of fuels (`m_fuelItems`) rather than the smelter's single `m_fuelItem`.

## Station fuel still does not come from a backpack

An Adventure Backpacks backpack is deliberately **never** treated as storage by this mod — nothing is ever pulled out of your own backpack. Adventure Backpacks exposes its contents to the game's own counts only while a craft is being evaluated, which a fuel switch is not, so backpack fuel cannot be used by a station in either mod. Keep the fuel in a chest (or in your inventory) for a station to use it.

## Requirements

- **BepInEx** 5.4.2351 (BepInExPack_Valheim) — `5.4.2350` works too
- **Jötunn** 2.30.2 — required, and provides the version enforcement below
- **MultiUserChest** 0.6.2 — required; all chest reads and removals go through it, so two players can use one chest at once without desync
- **KG Item Drawers** 1.4.0 — optional; without it the mod runs chest-only
- **Adventure Backpacks** 2.3.x — optional; with it, materials in your equipped backpack count

**Uninstall CraftFromContainers and AzuCraftyBoxes first.** This mod replaces them and refuses to load alongside either.

**Every player and the server must install this mod at the same version** — the connection is refused otherwise.

## Notes

- A pull brings the **full** materials each time, whatever you are already carrying — in your inventory or in a backpack. That is the point of it: it is a fetch, not a top-up.
- Delivery goes into your inventory, so it needs room there: a matching free stack, or an empty slot for an item that does not stack. A nearly full inventory gets part of the materials.
- Only **your own** backpack is ever counted — another player's is never used, whether you are crafting, building or pulling.
- Station feeding never leaves fuel behind in your inventory: the generator's unit is removed and fed to the generator in the same step, so a full inventory does not block it.
