Craft and build straight out of your storage: chests **and** KG Item Drawers drawers within one range of you, instead of only from your inventory.

**Install from Thunderstore:** <https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/> — the same package is attached below for a manual install.

A packaging release: **no behaviour change**. It carries everything from `1.0.2` — the fix that lets the ctrl-click pull be used repeatedly — and moves the BepInExPack dependency to the current pack.

## Changed

- **BepInExPack_Valheim `5.4.2351`** is now the declared dependency instead of `5.4.2350`, so mod managers stop pointing players at the older pack. **`5.4.2350` keeps working**: both packs ship the same BepInEx 5.4.23.5 core and HarmonyX 2.9.0, which is what this mod binds to, so the plugin itself was not changed and did not need rebuilding.

## Included from 1.0.2

- **Pulling the same recipe twice now supplies materials twice.** The **Left Ctrl** pull — and the same key while placing a building piece — brings the recipe's or piece's **whole cost** on every press, so two clicks give you the materials for two portals. Previously the second pull did nothing at all.
- **A base that can only partly cover the cost contributes the part it has**, instead of the pull being refused outright, so nearly empty chests still bring back something.
- **A full inventory no longer cancels the pull.** The amount is capped to what your inventory can accept, so a fetch delivers what fits. Crafting, building and station feeding are untouched: their checks and consumption still count what you already carry.

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
