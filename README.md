# Craft From Drawers And Chests

Craft and build straight out of your storage. This mod lets Valheim's crafting stations,
the build hammer and the crafting menu pull their materials from every **chest** — and every
**KG Item Drawers** drawer — within one range of you, instead of only from your inventory.

In short: CraftFromContainers, but for drawers too.

**Install from Thunderstore:** <https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/>

## Features

- **Craft from storage.** When you craft, the required materials are pulled from nearby
  chests and drawers before the recipe is checked and consumed.
- **Build from storage.** The build hammer's "do I have the materials?" check and the
  material consumption both draw from storage.
- **Ctrl-click to pull.** Hold the pull key and ctrl-click to move ingredients into your
  inventory on demand. It brings the recipe's **full** materials every time — whatever you are
  already carrying — so you can click twice for the materials for two portals. There's no excuse
  for sailing to another continent without portal mats again!
- **Station feeding.** Fireplaces, smelters and cooking stations pull fuel, ore and cooking
  ingredients from storage.
- **One range, both storage kinds.** A single range setting (default 10 m, and `0` or less
  means unlimited, matching CraftFromContainers) applies to chests and drawers alike.
- **Per-storage switch.** Flip a chest or drawer off with a hotkey while hovering it, and
  this mod never pulls from it. The switch lives on the object, so it is shared with
  everyone and saved with the world.
- **Your backpack counts.** With **Adventure Backpacks**, materials in your equipped backpack
  count as held while crafting and building — under that mod's own rules, and without moving
  anything into your inventory to make it work. Only your own backpack is ever counted: another
  player's is never used, whether you are crafting, building or pulling.
- **Server-authoritative config.** Settings are registered with Jötunn, so the server's
  values are pushed to clients and non-admins cannot change them.

## Requirements

> **Every player *and* the server must install this mod.** Multiplayer compatibility is
> enforced (`EveryoneMustHaveMod`): if a client is missing the mod — or is running a
> different version — the connection is refused. The exact same `CraftFromDrawersAndChests.dll`
> is used on the server and on every client.

- **BepInEx 5.4.2351** (the current BepInExPack_Valheim). `5.4.2350` works too: both packs ship the
  same BepInEx 5.4.23.5 core and HarmonyX 2.9.0.
- **Jötunn 2.30.2** — required.
- **MultiUserChest 0.6.2** — required. All chest reads and removals go through it so two
  players can use the same chest at once without desync.
- **KG Item Drawers 1.4.0** — optional. With it absent the mod runs chest-only; with it
  present, drawers are pulled from as well.
- **Adventure Backpacks 2.3.x** — optional. With it installed, materials in your equipped
  backpack count as held when crafting and building, using Adventure Backpacks' own rules
  (including its *Leave One Item In Backpack* setting).

### Remove these before installing

This mod does what they do, and replacements must not overlap. **Uninstall them first** or
you will get conflicting patches and double pulls:

- **CraftFromContainers**
- **AzuCraftyBoxes**

This mod declares both as incompatible dependencies, so BepInEx will refuse to load while
either is installed. Remove them from `BepInEx\plugins` (or your mod-manager profile) and
restart before installing this mod.

## Installation

**From Thunderstore — recommended:** <https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/>.
Install it with your mod manager, or download the zip and extract it into `BepInEx\plugins`.

**By hand:**

1. Install **BepInExPack_Valheim** (BepInEx 5.4.2351) into your Valheim folder.
2. Drop `CraftFromDrawersAndChests.dll` into `BepInEx\plugins`.
3. Install **MultiUserChest** and, optionally, **KG Item Drawers**.
4. Do the same on the server. Restart the server and each client.

## Configuration

Settings are written to `BepInEx\config\NorCal_Nerds.CraftFromDrawersAndChests.cfg` on first
launch and can be edited in-game with the BepInEx ConfigurationManager. The server's values
win; clients receive them on connect.

See [CHANGELOG.md](CHANGELOG.md) for the release history.

## Troubleshooting

**A storage's contents are not being counted.** Hover it. If the hover text ends with
`(pull: off)`, that storage is switched off — for everyone, permanently, on that object — and this
mod will never pull from it. The switch is flipped by the *Toggle storage key* while the storage is
under your crosshair, so **do not bind that key to a movement key**: the shipped default is
`x`, and a key you press while walking (such as `W` or `S`) will silently toggle whatever you happen
to be looking at.

**Storage that is further away is ignored.** Only storage within *Maximum range* of you — or of the
station, for the fuel, ore and cooking features — is used at all. `0` or less means unlimited, but
that also means every storage you own is scanned.

**The available amount is one below what I am carrying.** Materials in your equipped
**Adventure Backpacks** backpack are counted as held, which means they follow that mod's rules too.
With its *Leave One Item In Backpack* option enabled, one item of each type is deliberately left
uncounted **and** unconsumed, so 25 carried legitimately reads as 24. Turn that option off in
Adventure Backpacks if you want the whole stack usable.

**An ingredient is in my backpack but the panel shows none.** Check Adventure Backpacks' *Enable
Craft From Backpack* setting. With it disabled the backpack is not counted here either — and that
mod will not consume from it either.

**Nothing is pulled while I hold a modifier.** The *Prevent mod key* (default `left alt`) suspends
this mod entirely while it is held, so only your inventory is used.

**A pull did not deliver everything.** Delivery goes into your inventory, so it needs room there: a
matching free stack, or an empty slot for an item that does not stack (such as a Dvergr lantern). The
pull takes what your inventory can accept rather than refusing the whole thing, so a nearly full
inventory brings back part of the materials; if nothing at all fits, it does nothing. A recipe with
several ingredients has to fit as a set.

## Bugs and feature requests

**Both belong in the issue tracker:** <https://github.com/ghstwhl/CraftFromDrawersAndChests/issues>.

A report that can be acted on usually carries:

- the version this mod loaded as, which `BepInEx\LogOutput.log` states on startup;
- that log, or at least the part of it around the problem — it is where any diagnosis starts;
- whether you are playing alone or on a server, and if on a server whether you are the host or a
  client;
- the versions of the rest of the chain: **MultiUserChest** (required), and **KG Item Drawers** and
  **Adventure Backpacks** (both optional).

A fault in another mod belongs on that mod's own tracker rather than here — Item Drawers' hotkeys and
hover text, for instance, are not ours to change.

## Building from source

Build and packaging tooling lives under [`tools/`](tools/README.md); the local reference
assemblies it needs are documented in [REFERENCES.md](REFERENCES.md).

## Credits

- **CraftFromContainers** (aedenthorn) — the behavioural baseline this mod ports.
- **AzuCraftyBoxes** (Azumatt) — reference for the storage abstraction and the toggle.
- **MultiUserChest** (MSchmoecker) — the chest concurrency protocol this mod builds on.
- **Jötunn** — the mod framework and server-synced configuration.

## Licence

GPL-3.0 — see [LICENSE.md](LICENSE.md).

