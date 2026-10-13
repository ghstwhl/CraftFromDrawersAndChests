Craft and build straight out of your storage: chests **and** KG Item Drawers drawers within one range of you, instead of only from your inventory.

**Install from Thunderstore:** <https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/> — the same package is attached below for a manual install.

A bug-fix release for the on-demand pull. Everything else is unchanged from 1.0.1.

## Fixed

- **Pulling the same recipe twice now supplies materials twice.** The **Left Ctrl** pull — and the same key while placing a building piece — asked storage only for what you were *short* of, so once a recipe's materials were in your inventory a second pull did nothing at all: you could not fetch the materials for a second portal. Both now bring the recipe's or piece's **whole cost** on every press, which is what CraftFromContainers does, so two clicks give you two portals' worth.
- **A base that can only partly cover the cost now contributes the part it has** instead of the pull being refused outright, so nearly empty chests still bring back something. When the base cannot supply it at all, the message still names what is missing — computed from what you are actually short of, so it never names materials you are carrying.

## Changed

- **A full inventory no longer cancels the pull.** The amount is capped to what your inventory can accept, so a fetch delivers what fits; if nothing fits, it does nothing. A recipe with several ingredients is still proved as a set, so it can report the room failure rather than delivering a partial set.
- **Crafting, building and station feeding are untouched.** Their availability checks and their material consumption still count what you already carry, so a recipe you can partly supply is still offered, and is still paid from your own inventory first. Only the deliberate pull changed.

## Requirements

- **BepInEx** 5.4.2350 (BepInExPack_Valheim)
- **Jötunn** 2.30.2 — required, and provides the version enforcement below
- **MultiUserChest** 0.6.2 — required; all chest reads and removals go through it, so two players can use one chest at once without desync
- **KG Item Drawers** 1.4.0 — optional; without it the mod runs chest-only
- **Adventure Backpacks** 2.3.x — optional; with it, materials in your equipped backpack count

**Uninstall CraftFromContainers and AzuCraftyBoxes first.** This mod replaces them and refuses to load alongside either.

**Every player and the server must install this mod at the same version** — the connection is refused otherwise.

## Notes

- The pull brings the **full** materials each time, whatever you are already carrying — in your inventory or in a backpack. That is the point of it: it is a fetch, not a top-up.
- Delivery goes into your inventory, so it needs room there: a matching free stack, or an empty slot for an item that does not stack. A nearly full inventory gets part of the materials.
- Only **your own** backpack is ever counted — another player's is never used, whether you are crafting, building or pulling.
