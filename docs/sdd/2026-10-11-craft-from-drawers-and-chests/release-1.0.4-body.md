Craft and build straight out of your storage: chests **and** KG Item Drawers drawers within one range of you, instead of only from your inventory.

**Install from Thunderstore:** <https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/> — the package is also attached below for a manual install.

A compatibility release for **Valheim 1.0.20**: **no behavioural change**. The game update was an anti-cheat and save-migration release — it renamed `ItemDrop.ResetVariantIfInvalid` to `EnsureValid`, retyped `CharacterDrop.s_pseudoCounter`, and added an internal `CheatUtils` plus new `Version/*` migration stamps over the pre-existing `ZDOVars.s_cheated` flag. Of the **24** members this mod patches or reflects, **none** changed: not a signature, not an access modifier, anywhere in the assembly. Nothing needed fixing, so no behaviour was touched.

## Changed

- **Built against the Valheim 1.0.20 assemblies**, up from 1.0.16. The plugin is recompiled and stamped **1.0.4**.

> ⚠️ **Every player and the server must update together.** The declared plugin version changes from `1.0.3` to `1.0.4`, and this mod refuses a connection whose version does not match — as Jötunn's enforcement is configured to do.

## Verified on 1.0.20

- Loaded on Valheim **1.0.20**, single player (host), 2026-10-16: the plugin loaded, all **22** Harmony patches applied, container tracking was live and tracked a `piece_chest_wood(Clone)`, and the session logged **0 errors**. Dependencies present in that session: BepInExPack 5.4.2351, Jötunn 2.30.2, MultiUserChest 0.6.2, Adventure Backpacks 2.3.4.
- `dotnet test` 262/262, and the packaged `manifest.json` reads `1.0.4`.
- The compatibility case is an assembly diff, not an impression: the update changed 6 and added 21 members, none of them one this mod binds. Full reasoning and raw evidence: `docs/sdd/2026-10-11-craft-from-drawers-and-chests/game-1.0.20-compat-analysis.md`.

**Not yet exercised on 1.0.20** (open on the checklist): the first load of a pre-update world under the game's new clean-slate migration, a pull on a nearly empty base, client-side station feeding on a dedicated server, and MultiUserChest as a non-host client.

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
