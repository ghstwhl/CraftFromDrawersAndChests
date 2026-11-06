Craft and build straight out of your storage: chests **and** KG Item Drawers drawers within one range of you, instead of only from your inventory.

**Install from Thunderstore:** <https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/> — the package is also attached below for a manual install.

This release covers everything since 1.0.9: **Valheim 1.0.21 support**, and **a requirement that is nearby but locked now says so** instead of looking like "you have none".

## Added

- **A requirement that is nearby but blocked no longer looks like one you do not have.** When storage does not cover a requirement but the item sits in storage you cannot use, the row keeps its numbers, takes an amber tint and gains a marker (`0/1 *` by default), and a one-off message names the item and the reason:

  > `Seal Blubber is nearby but the storage holding it is switched off.`

  Both locks count: this mod's own per-storage switch, and storage that refuses you — another player's private chest, or a chest a ward denies (`… the storage holding it is locked.`, and `switched off or locked` when both occur in range). The hint fires even when the locked storage covers only part of the shortfall, because it explains why the count looks wrong; it never claims the item is reachable.

  New settings, all in `1 - Server`: **Blocked hint suffix** (`" *"`; empty leaves the numbers bare), **Blocked color** (amber), **Blocked message** (on).

- **Nothing changes for storage you can use or for an item you simply do not have:** covered requirements still flash, and a genuinely absent ingredient still reads plain `0/N`. Pulls are untouched — a blocked storage supplies nothing.

## Changed

- **Built against Valheim 1.0.21**, up from 1.0.20. No behaviour change from that: of the **26** game members this mod patches or reflects, **not one** changed signature or access. The update's changes sit in station and save-system internals — `CookingStation.SetSlot` gained an `Int64`, `Ship.UpdateSail`/`UpdateSailSize` gained a `Boolean`, `PlayerProfile.GetStat` gained a `DifficultyRequirement` overload, and `SaveSystem`, `SaveCollection` and `WearNTear` gained private members. Every patch here is a prefix or postfix (**zero transpilers**), so a changed callee signature cannot affect them. Full evidence, member by member: [`game-1.0.21-compat-analysis.md`](docs/sdd/2026-10-11-craft-from-drawers-and-chests/game-1.0.21-compat-analysis.md).

## Fixed

- **The debug readout can name a storage that is switched off.** "I have it, but the panel says I do not" was previously unanswerable from the log when the culprit was the per-storage switch: a switched-off storage is dropped before the pullable list is built, so it never appeared in the readout's source list — and the readout's own `OFF` marker could never fire. With **Debug** enabled, an unanswered requirement now lists in-range storages *including* switched-off ones, with their contents and an `OFF` mark:

  > `[Drawer kg_ItemDrawer_Marble(Clone) 3m OFF count=78 zdo='SealBlubber'x78]`

> ⚠️ **Every player and the server must update together.** The declared plugin version changes from `1.0.9` to `1.0.11`, and this mod refuses a connection whose version does not match — as Jötunn's enforcement is configured to do. (1.0.10 was never published; it is folded into this release.)

## Verified

- **In game by the owner on Valheim 1.0.21**: the 1.0.21 baseline build (1.0.10) loaded with all **24** Harmony patches applied and **0 errors**, and the blocked-storage hint build (1.0.11) was verified by the owner locally before this release.
- `dotnet test` **308/308**; the packaged `manifest.json` reads `1.0.11`, and the packaged DLL is the release build.
- The compatibility claim is an assembly diff, not an impression: API dumps of the installed `assembly_valheim.dll` and the 1.0.20 reference copy differ by **17 added / 5 removed** members, none of them one this mod binds; all **26** touchpoints compare identical including parameter types, return types and access flags.

## Requirements

- **BepInEx** 5.4.2351 (BepInExPack_Valheim) — `5.4.2350`/`5.4.2360` work too
- **Jötunn** 2.30.2 — required, and provides the version enforcement below
- **MultiUserChest** 0.6.2 — required; all chest reads and removals go through it, so two players can use one chest at once without desync
- **KG Item Drawers** 1.4.0 — optional; without it the mod runs chest-only
- **Adventure Backpacks** 2.3.x — optional; with it, materials in your equipped backpack count
