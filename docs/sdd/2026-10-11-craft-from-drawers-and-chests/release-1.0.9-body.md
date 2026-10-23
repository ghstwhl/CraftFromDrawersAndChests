Craft and build straight out of your storage: chests **and** KG Item Drawers drawers within one range of you, instead of only from your inventory.

**Install from Thunderstore:** <https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/> — the package is also attached below for a manual install.

Station feeding works properly now. This release covers everything since 1.0.5: **the fill-all key feeds the material you are already carrying**, **every ore type reaches a smelter from chests and drawers**, **shield generators take fuel from storage**, and **the smelter tells you what it added**.

## Added

- **A message says what a smelter feed added.** Loading ore into a smelter or blast furnace prints one line naming every ore and how many of each went in, in the station's own conversion order:

  > `Added 1 Copper, 1 Tin, 1 Iron, 1 Silver`

  A fill-all press that draws on several ores lists **all** of them with their counts on that single line, however many types the smelter converts — not a message per unit swapping itself off the screen. Item names are the game's own localized names, and nothing is printed when the feed added nothing. Vanilla's `$msg_added` still covers the single unit it consumes out of your own hands.

## Fixed

- **The fill-all key now fills a station from the material you are already carrying.** Holding it while using a station whose input was in your inventory used to add **one** item per press — most visible on a **blast furnace** with flametal in hand, but true of a fireplace's and a smelter's fuel and a shield generator's fuel too. With the key held, the station now takes its whole remaining capacity from your inventory and storage together. The released interaction is unchanged: without the key, an input you carry is still vanilla's to consume, one per press.
- **Every ore type is fed from storage again, from chests and from drawers.** 1.0.6's fix above briefly broke this for any ore that was not the first one the smelter converts: the walk over the smelter's accepted ores stopped at the first one the base did not hold, so tin, scrap iron, silver and scrap copper were never pulled, while a blast furnace (one ore) was unaffected. The walk now stops only at an ore that actually reached your inventory.
- **Shield generators take their fuel from storage.** `piece_shieldgenerator` was the one fuelled station this mod did not patch: its add-fuel switch behaved exactly as vanilla, taking one `BoneFragments` per press from your inventory with nothing pulled from storage and no effect from the fill-all key. It is now fed like a fire or a smelter — fuel from storage within range **of the generator**, the fill-all key filling it to capacity in one press, and the `[shift+Use] … max` hint on its hover. Gated on the same `Pull fuel` setting and `Fuel disallow types` list; there is no new setting.

> ⚠️ **Every player and the server must update together.** The declared plugin version changes from `1.0.5` to `1.0.9`, and this mod refuses a connection whose version does not match — as Jötunn's enforcement is configured to do.

## Verified

- **In game by the owner on 2026-10-24**, Valheim **1.0.20**: the plugin loaded as **1.0.9**, all **24** Harmony patches applied (including the shield generator's two), and the session logged **0 errors**.
- `dotnet test` **277/277**; the packaged `manifest.json` reads `1.0.9` and the packaged DLL is byte-identical to the release build.
- Every game member these changes touch was checked against the shipping assembly rather than assumed: `ShieldGenerator.OnAddFuel(Switch, Humanoid, ItemData)` and its `OnHoverAddFuel()` are private there (patched by name, with the private `GetFuel()` reflected), a shield generator declares a **list** of fuels rather than a smelter's single item, and `MessageHud.MessageType.TopLeft` is where vanilla prints its own add line.

## Station fuel still does not come from a backpack

An Adventure Backpacks backpack is deliberately **never** treated as storage by this mod — nothing is ever pulled out of your own backpack. Adventure Backpacks exposes its contents to the game's own counts only while a craft is being evaluated, which a fuel or ore switch is not, so backpack contents cannot be used by a station in either mod. Keep the fuel in a chest (or in your inventory) for a station to use it.

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
- Station feeding never leaves fuel or ore behind in your inventory: each unit is removed and fed to the station in the same step, so a full inventory does not block it.
