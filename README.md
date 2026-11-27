# Craft From Drawers And Chests

Craft and build straight out of your storage. This mod lets Valheim's crafting stations,
the build hammer and the crafting menu pull their materials from every **chest** — and every
**KG Item Drawers** drawer — within one range of you, instead of only from your inventory.

In short: CraftFromContainers, but for drawers too.

It also bridges **Adventure Backpacks**: materials in your equipped backpack count while crafting and
building, arrows and bolts fire straight out of it, and a **Plateautem** can be fuelled from it — none
of which the game or those mods do on their own.

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
- **Station feeding.** Fireplaces, smelters, cooking stations and shield generators pull fuel, ore and
  cooking ingredients from storage. Hold the fill-all key (`left shift` by default) and use a station to
  fill it to capacity in one press. Plateautems (erkle64's terrain-flattening totems) are fed the same
  way: the mod asks the totem which item its current selection consumes — wood, coal, resin or stone —
  and a held press fills it to the totem's own capacity.
- **One range, both storage kinds.** A single range setting (default 10 m, and `0` or less
  means unlimited, matching CraftFromContainers) applies to chests and drawers alike.
- **Per-storage switch.** Flip a chest or drawer off with a hotkey while hovering it, and
  this mod never pulls from it. The switch lives on the object, so it is shared with
  everyone and saved with the world.
- **Your backpack counts.** With **Adventure Backpacks**, materials in your equipped backpack
  count as held while crafting and building — under that mod's own rules, and without moving
  anything into your inventory to make it work. Only your own backpack is ever counted: another
  player's is never used, whether you are crafting, building or pulling.
- **Plateautem ↔ Adventure Backpacks bridge.** Plateautem counts its fuel in your main inventory only
  and never asks Adventure Backpacks for your backpack, so a totem with fuel only in the backpack
  answers "no fuel". While this mod is installed the totem's fuel interaction runs inside Adventure
  Backpacks' own crafting scope, so your equipped backpack both counts and *is* debited, exactly as it
  is for a craft. Neither mod needs configuring for this; it applies only to the Plateautem, not to
  other third-party stations.
- **Backpack ammunition.** Arrows and bolts in your equipped backpack can be fired. Vanilla only looks
  for ammunition in your inventory and never in the backpack, so a bow with its arrows packed away
  reports you are out of them. Each shot is spent straight out of the backpack — the stack stays where
  it is, exactly as Adventure Backpacks pays for a craft — and an ordinary shot from your inventory is
  untouched.
- **Server-authoritative config.** Settings are registered with Jötunn, so the server's
  values are pushed to clients and non-admins cannot change them.

## Requirements

> **Every player *and* the server must install this mod.** Multiplayer compatibility is
> enforced (`EveryoneMustHaveMod`): if a client is missing the mod — or is running a
> different version — the connection is refused. The exact same `CraftFromDrawersAndChests.dll`
> is used on the server and on every client.

- **BepInEx 5.4.2351** (the current BepInExPack_Valheim). `5.4.2350` works too: both packs ship the
  same BepInEx 5.4.23.5 core and HarmonyX 2.9.0.
- **Jötunn 2.30.3** — required.
- **MultiUserChest — must NOT be installed.** MultiUserChest's chest-sharing code is now **built into this
  mod** (`Chest/`, taken from MultiUserChest 0.6.2 under the MIT licence), so the two would patch the same
  methods and register the same network routes. This mod declares it an incompatible dependency, which means
  BepInEx refuses to load this mod while MultiUserChest is present. **Remove MultiUserChest before
  upgrading.** It is built in on purpose: the code is in-tree so its decisions — who owns a chest, which
  branch answered an open, what a pull actually did — can be logged with this mod's own **Debug** switch and
  fixed here, instead of being observable only through another plugin's compile-time debug build. See
  *MultiUserChest is now part of this mod* below.
- **KG Item Drawers 1.4.0** — optional. With it absent the mod runs chest-only; with it
  present, drawers are pulled from as well.
- **Adventure Backpacks 2.x** (tested with 2.4.7) — optional. With it installed, materials in your equipped
  backpack count as held when crafting and building, using Adventure Backpacks' own rules
  (including its *Leave One Item In Backpack* setting), and arrows and bolts can be fired straight out
  of the backpack.
- **Plateautem 0.4.3** — optional. With it installed, a plateautem is fed from nearby chests and
  drawers — its selected fuel, up to its own capacity — and an equipped Adventure Backpacks backpack
  can pay for that fuel (see *Plateautem ↔ Adventure Backpacks bridge* above). Tested against
  Plateautem 0.4.3 and Adventure Backpacks 2.4.7; if either mod changes shape the bridge logs a warning
  and switches itself off rather than guessing.

### Who owns a chest, and what everyone else can do with it

A chest belongs to the player who claimed it — the first one to open it while it was unclaimed — and it stays
theirs when someone else opens it. **Every** player can craft and build from it: a cost that has to be taken
from a chest another player owns is fetched over the network first and the craft completes the moment those
items arrive (a fraction of a second later). Nothing is crafted until the materials are actually in hand.

### MultiUserChest is now part of this mod

MultiUserChest's chest-sharing layer is compiled into `CraftFromDrawersAndChests.dll` (absorbed from
MultiUserChest 0.6.2, MIT — see [LICENSE-MULTIUSERCHEST.txt](LICENSE-MULTIUSERCHEST.txt)). **Uninstall
MultiUserChest**; this mod declares it an incompatible dependency, so BepInEx will refuse to load this mod
while it is present.

It was rolled in for debugging: a chest problem used to be a black box behind another plugin whose only
diagnostic switch was a compile-time `DEBUG` symbol, so a shipped build could say nothing about what it
decided. Now every chest decision is this mod's own code and follows its **Debug** setting at run time —
who owns a container and whether an open was granted or handed over, which branch answered it, what a pull
took or routed, and what it waited for.

### Remove these before installing

This mod does what they do, and replacements must not overlap. **Uninstall them first** or
you will get conflicting patches and double pulls:

- **MultiUserChest** — its chest-sharing code now ships inside this mod (see above). Two copies would patch
  the same methods and register the same `MUC_*` network routes, so this mod declares it an incompatible
  dependency and BepInEx refuses to load this mod while it is installed.
- **CraftFromContainers**
- **AzuCraftyBoxes**

This mod declares all three as incompatible dependencies, so BepInEx will refuse to load while
any of them is installed. Remove them from `BepInEx\plugins` (or your mod-manager profile) and
restart before installing this mod.

## Installation

**From Thunderstore — recommended:** <https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/>.
Install it with your mod manager, or download the zip and extract it into `BepInEx\plugins`.

**By hand:**

1. Install **BepInExPack_Valheim** (BepInEx 5.4.2351) into your Valheim folder.
2. Remove **MultiUserChest** if you have it, then drop `CraftFromDrawersAndChests.dll` into
   `BepInEx\plugins`. Optionally install **KG Item Drawers**.
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

**A chest another player filled still reads as empty.** Known limitation: a chest's contents reach a
client when the game sends them, and it sends a chest when a player takes ownership of it — opening it —
not when a teammate fills it. Opening the chest yourself clears it for your client. A diagnostic build
(`Debug` enabled) logs what each chest's local copy and ZDO claim, which is how this is being tracked;
see [CHANGELOG.md](CHANGELOG.md).

**A requirement is tinted amber and marked `*`, but I can see the item in a drawer.** That item is in
storage this mod will not take from: the storage is switched off, or it refuses you — another
player's private chest, or a chest a ward denies. The marker is *Blocked hint suffix*, the tint is
*Blocked color*, and the one-off message names the item and the reason. Unlock or switch the storage
back on and the row returns to normal; a genuine "you have none" stays unmarked.

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
- the versions of the rest of the chain: **KG Item Drawers** and **Adventure Backpacks** (both
  optional), and confirmation that **MultiUserChest** is **not** installed (since 2.0.0 its layer is
  part of this mod, and BepInEx will refuse to load this mod while it is present).

A fault in another mod belongs on that mod's own tracker rather than here — Item Drawers' hotkeys and
hover text, for instance, are not ours to change.

## Building from source

Build and packaging tooling lives under [`tools/`](tools/README.md); the local reference
assemblies it needs are documented in [REFERENCES.md](REFERENCES.md).

## Credits

- **CraftFromContainers** (aedenthorn) — the behavioural baseline this mod ports.
- **AzuCraftyBoxes** (Azumatt) — reference for the storage abstraction and the toggle.
- **MultiUserChest** (MSchmoecker, MIT) — the chest concurrency protocol this mod builds on. Since
  2.0.0 its source is compiled into this mod (`Chest/`) rather than required as a separate plugin, so
  that its behaviour can be logged and fixed here. Copyright © 2022 Maximilian Schmöcker; the MIT
  notice ships as [LICENSE-MULTIUSERCHEST.txt](LICENSE-MULTIUSERCHEST.txt).
- **Jötunn** — the mod framework and server-synced configuration.

## Licence

GPL-3.0 — see [LICENSE.md](LICENSE.md).

