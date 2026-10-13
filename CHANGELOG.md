# Changelog

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2026-10-13

### Fixed

- **Pulling the same recipe twice now supplies materials twice.** The ctrl-click pull — and the
  pull-key build press — asked storage only for the *shortfall* below what you were already carrying,
  so once a recipe's materials were in your inventory a second pull did nothing at all: you could not
  fetch the materials for a second portal, or a second anything. Both now ask for the recipe's or
  piece's **whole cost** on every press, which is what CraftFromContainers does.
- **A base that can only partly cover the cost now contributes the part it has** instead of the pull
  being refused outright, so a fetch still brings back something when the chests are nearly empty.
  When the base cannot supply it at all, the message still names what is missing — computed from what
  you are actually short of, so it never names materials you are carrying.

### Changed

- **A full inventory no longer cancels the pull.** The amount is capped to what your inventory can
  accept, so a fetch delivers what fits. A recipe with several ingredients proves the capped plan as a
  whole, so it can still report the room failure rather than delivering a partial set.
- **Crafting and placing are untouched.** Their availability checks and their material consumption
  still count what you already carry, so a recipe you can partly supply is still offered, and is still
  paid from your own inventory first. Only the deliberate pull changed.

## [1.0.1] - 2026-10-13

Supersedes `1.0.0`, which was packaged locally but never published.

### Changed

- **The per-storage toggle now defaults to `x`** instead of `left shift + e`.
  The old default collided with Item Drawers' own "withdraw single item" hotkey, which is really
  **LeftShift + E** — its hover text calls it "Left Alt + E", but its code reads KeyCode 304, and in
  Unity's enum 304 is LeftShift while 308 is LeftAlt — so on a drawer one press did both. `x` is read
  by neither the game nor Item Drawers (`E`, LeftShift) nor Adventure Backpacks (`I`, `Y`).
  **Existing installs keep their configured value** — BepInEx preserves a value once it is written, so
  this only affects a fresh config. If yours is bound to a movement key (`W`, `A`, `S` or `D`), change
  it: the toggle flips whatever storage is under your crosshair, so a movement key switches storage off
  silently as you walk.
- **Debug logging is now off by default.** It was on, which wrote this mod's diagnostics into every
  player's log. A fresh config is now quiet; an existing config keeps whatever it has.

### Fixed

- Comment only, no behaviour change: `CountHeldBySharedName` claimed that station feeding never reads
  the held count. It does — a station's shortfall is planned through the same planner — and that is
  correct only because Adventure Backpacks' craft scope is closed on those interaction paths, so its
  backpack contribution is absent exactly where a station could not consume it. The comment now records
  that dependency, because relying on it silently is how a station would end up under-fed.

## [1.0.0] - 2026-10-13

First stable release. Behaviourally this is the build validated in game as `0.1.13` — the version bump
changes no code — with the README brought up to date.

### What it does

- **Craft and build from storage.** Chests and **KG Item Drawers** drawers within one range (default
  10 m; `0` or less means unlimited, matching CraftFromContainers) supply the materials for crafting
  stations, the crafting menu and the build hammer.
- **Pull on demand.** Hold **left ctrl** and ctrl-click a recipe or a placement to move its ingredients
  into your inventory instead of crafting it.
- **Station feeding.** Fireplaces, smelters, cooking stations and turrets draw fuel, ore and cooking
  ingredients from storage; **left shift** asks for the station's whole remaining capacity rather than
  one item per interaction, and the hover text names the key.
- **Your backpack counts.** With **Adventure Backpacks**, materials in your equipped backpack count
  while crafting and building, under that mod's own rules — including its *Leave One Item In Backpack*
  option, which is why a carried stack of 25 can read as 24.
- **All-or-nothing.** If the whole requirement cannot be secured, nothing is consumed, so a craft is
  never paid for by nothing. Storage yes is not enough either: the destination inventory must have room
  for what the pull would deliver.
- **Per-storage switch.** Any chest or drawer can be taken out of pulling for everyone with a hotkey
  while hovering it. The switch lives on the object, so it is shared and saved with the world.
- **Server-authoritative config.** The server's values are pushed to clients and non-admins cannot
  change them, and every player and the server must run the same version.

### Notes

- Chest reads and removals go through **MultiUserChest**, so two players can use one chest at once
  without desync; drawer removals use Item Drawers' own `ForceRemove` RPC and never write a drawer's
  ZDO amount.
- Item Drawers support is pinned to that mod's 1.4.0 ZDO contract and RPCs. If they change, drawer
  pulling degrades to chest-only rather than failing.
- The README's Troubleshooting section covers the per-storage switch, `Maximum range`, and the
  Adventure Backpacks interaction.

## [0.1.13] - 2026-10-13

### Fixed

- **Materials in an AdventureBackpacks backpack now count, so a recipe that needs them is offered and
  craftable.** Reported from play: a cauldron recipe wanting one Boar Meat read `0/1` and could not be
  crafted with all 25 in the backpack, and the forge ignored backpack contents too. The mod counted
  "held" by scanning `Player.m_inventory` itself, which cannot see the backpack — that is a *separate*
  `Inventory` — so the requirement gate concluded the player had nothing and refused the craft, while
  the crafting readout overwrote AdventureBackpacks' own number with its own.
- "Held" is now the game's own `Inventory.CountItems(name, -1, false)`, at every place it is read: the
  shortfall a pull plans for, the crafting and building readout, and `HasAnyOfEach`'s "has any of this
  material". AdventureBackpacks postfixes that very method to add the equipped backpack's contents, and
  does so only while its craft scope is open — which covers every method this mod reads held inside — so
  a backpack item is counted exactly when that mod will also pay with it, and never counted when it will
  not. Nothing is pulled *out* of the backpack into the player's inventory.
- Counting the game's own method also makes "held" a superset of what vanilla consumes **by
  construction** rather than by a hand-rolled scan, which is the invariant that keeps a craft from ever
  costing nothing. The pure `DrawerTake.HeldAmount` rule and its fixture went with it: one held notion
  rather than two.

### Added

- **Each requirement's readout is logged once per distinct answer** while `Debug` is on, and an
  unanswered requirement lists every in-range source with its kind, its distance, the count it reported
  for the prefab, and — for a drawer — the raw prefab string and amount its ZDO actually holds. A
  drawer storing an item under a *different* prefab string than a requirement names is otherwise
  indistinguishable from one that is out of range or switched off, and neither half of that comparison
  is visible in the game's own UI.

### Notes

- **Holding `Left alt` with the crafting or build panel open deliberately blanks storage from the
  readout.** That is the `Prevent mod key`, a CraftFromContainers port whose default is `left alt`: with
  it held this mod does not count storage at all, so the panel shows AdventureBackpacks' own number —
  the backpack counts and drawers and chests do not. Rebind or clear that key to change it.
- Station feeding still counts the player's inventory only, deliberately: a fireplace, smelter or
  cooking station takes its input out of `player.GetInventory()` alone, so an item in a backpack is
  genuinely not available to it.

## [0.1.12] - 2026-10-13

### Fixed

- **A single-item feed printed the game's "you don't have any …" message even though the item was fed.**
  0.1.11's per-unit confirmation compared the station's remaining capacity against *this call's* unit
  bound — `1` on the single-item path, the whole capacity on the fill path — so on `E` any station with
  room at all read as "did not take it". The feed then reported nothing fed, the patch left the
  interaction to vanilla, and vanilla printed its own message (`$msg_outof` for a fire,
  `$msg_donthaveany` / `$msg_noprocessableitems` for a smelter) **after** the item had already arrived.
  That is exactly why the fill path looked correct and `E` did not: there the bound happened to equal
  the capacity. The check now reads the station's capacity before the unit and requires it to fall.

### Added

- **A fireplace's hover now names the fill key**, e.g. `[left shift+E] Add`, so a fire says how to fill
  it. CraftFromContainers adds this hint to the smelter's two hovers only, which left a fire with no way
  to discover the key. All three hovers go through one helper and use the game's own "Add" token, so
  they read alike.

### Notes

- The smelter's two hints still read "Add max item" for ore and "Add max" for fuel. That difference is
  CraftFromContainers', and the wording is the game's own (`m_addOreTooltip` for ore, the
  `$piece_smelter_add` token for fuel), so it is kept rather than smoothed over.

## [0.1.11] - 2026-10-13

### Changed

- **The player's inventory is the station feed's staging area, by the owner's decision.** Using it as a
  pass-thru is acceptable provided nothing extra is left in it, and nothing is: a take is capped by the
  inventory's own free room, so it can never over-take; every unit taken is fed to the station in the
  same interaction, so the net inventory is unchanged; and with no room at all nothing is taken and
  nothing is fed. A full inventory therefore stops a station being fed — accepted over giving the mod a
  staging inventory of its own, which risked stranding items outside any inventory.
- The feed now confirms each unit **actually reached the station** before removing another, by re-reading
  the station's remaining capacity (a station gains exactly one unit of capacity per unit fed). The
  order — remove, then invoke the station's add RPC — means an RPC that does not land destroys an item,
  and losing a whole batch silently would be far worse than the failure itself. Checked per unit, the
  most a broken feed can cost is one, and it logs an error naming the item instead of doing nothing
  visible.

## [0.1.10] - 2026-10-12

### Fixed

- **The station feed could leave the unit in the player's inventory.** The feed removed it by stack
  reference (`Inventory.RemoveItem(stack, 1)`). Vanilla's own consumption and CraftFromContainers both
  remove by **name** (`Inventory.RemoveItem(<shared name>, <amount>)`), and a reference-based removal
  that quietly does nothing left station fuel and ore in the player's backpack — reported from play on
  both the single-item and the fill path, which is what pointed at the feed itself rather than at the
  amount. The removal now uses the same overload vanilla uses.
- The feed checks that each removal actually took effect (an item count before and after) and stops
  with a logged warning if it did not, so a removal that does nothing can never again starve a station
  while quietly filling an inventory. It also stops rather than looping on such a removal.

## [0.1.9] - 2026-10-12

### Fixed

- **Station feeding no longer routes items through the player's inventory.** 0.1.8 wired the *fill*
  path to deliver into the station but left the single-item path — one `E` press — pre-loading the
  player's inventory for vanilla to consume. Reported from play: with a full inventory that put the
  item in the player's backpack instead of the station, and with no room at all it left the station
  unfed. CraftFromContainers feeds the station at *both* amounts (`pullAll ? Min(capacity - used,
  available) : 1`), so all four feedable paths — a fireplace's fuel, a smelter's fuel and ore, and a
  cooking station's fire — now remove the unit and drive the station's own replicated add RPC
  directly, at one unit or at the whole remaining capacity. The unit is fed in the same frame it is
  taken, so the inventory never holds it: a full inventory now means only that no more can be taken,
  not that items land in the wrong place.
- The Smelter and CookingStation prefixes report the interaction as handled (`__result = true`) once
  they feed, which is the value CraftFromContainers sets; the fireplace reports it consumed, its own
  value there.

### Notes

- Cooking *food* still stages through the player's inventory and cannot avoid it: the game hands a
  cookable over as an item reference (`FindCookableItem`) with no add RPC to drive, and this mod may
  not mutate a container's inventory directly (Ruling 5). A completely full inventory can therefore
  still stop a cooking station being stocked.

## [0.1.8] - 2026-10-12

### Added

- **Fill-to-max now fills the station itself.** Holding `Fill all mod key` on a fireplace, a smelter's
  fuel or a smelter's ore asks storage for the station's whole remaining capacity and delivers it
  **into the station**, so one interaction reaches the maximum instead of pre-loading the player's
  inventory for vanilla to consume one unit at a time. This is CraftFromContainers' behaviour, and it
  is the parity the owner ruled for in Task 15c.
  - The feed drives the station's own replicated add RPC (`RPC_AddFuel`, `RPC_AddOre`) — the same two
    shapes CraftFromContainers invokes — and it removes each unit from the inventory itself, so exactly
    one unit leaves the inventory per unit the station gains and nothing can be duplicated.
  - It works in batches: a pass moves only what the inventory can accept at that moment, so a station
    needing a hundred ore still fills from a base whose player has three free slots.
  - A cooking station keeps CraftFromContainers' one-item-per-interaction behaviour. CFC has no fill
    branch for it, so this matches the oracle rather than "improving" on it.

### Changed

- **`Maximum range` now defaults to 10, and 0 (or less) means unlimited** — both matching
  CraftFromContainers (CFC's `ContainerRange` default and its `maxRange.Value <= 0` test). A player
  arriving from CFC no longer silently loses storage that this mod's 4-unit default excluded, and a
  `0` carried over from CFC no longer disables the mod with nothing in the log to explain it. Both
  range consumers — the chest/drawer filter and the drawer query's own filter — now read one rule, so
  `0` cannot mean unlimited for one kind of storage and nothing for the other.

### Notes

- One **deliberate** parity gap: a fill with a *completely* full inventory takes nothing, where CFC
  would still succeed because its items never pass through the inventory. That is the same limit this
  mod's single-item station feeding already has. See the Task 15c design record in `docs/sdd/`.

## [0.1.7] - 2026-10-12

### Added

- **Key combinations for the per-storage switch, defaulting to `left shift + e`.** AzuCraftyBoxes uses
  LeftShift+E for the same feature, so the mod now matches it and the switch works out of the box
  instead of needing a key to be bound first. A combination is written with `+`: every name but the
  last must be held and the last is pressed, and a single key is just the one-name case of that rule.
  Spacing and casing do not matter, so `LeftShift+E`, `left shift+e` and `left shift + e` are the same
  setting, and the hover line shows it as `(press Left Shift + E to toggle)`.
- Unity spells the modifier keys as two words (`left shift`, `left ctrl`, `left alt`) while mod
  documentation usually writes them closed up, so the common closed-up spellings are accepted as well
  and normalised. Every hotkey in the mod shares that normalisation, so the pull, prevent and fill-all
  keys accept them too.

### Notes

- Shift+E also drives other mods' "store all of this item" convenience, and `E` is Valheim's interact
  key, so toggling while a chest is under the crosshair may also open it. That overlap is shared with
  AzuCraftyBoxes, whose convention this is.

## [0.1.6] - 2026-10-12

### Fixed

- **A setting changed while the game is running now takes effect at once.** The mod reads its
  configuration through a snapshot that was only rewritten by Jötunn's server push, so a value edited
  in the ConfigurationManager (F1) changed the entry but not the snapshot: a toggle key set in game
  still read as unset on the storage hover line, and pressing it did nothing, until the next world
  load or a `cfdac reset`. Every entry now re-reads the snapshot when it changes.
- **A toggle key written with a capital letter was silently dead.** The input system matches key names
  case-sensitively and the toggle hotkey passed the configured value through unchanged, so `N` or
  `Left Shift` never fired — while the pull and prevent keys lower-cased theirs. Every hotkey now
  reads through one shared normalisation (`ToggleRules.InputKeyName`), which also trims spaces.

### Notes

- A key setting takes a **single** Unity key name (`n`, `left shift`, `mouse 0`), not a combination,
  so a config editor's key-capture cannot express something like Ctrl+E.

## [0.1.5] - 2026-10-12

### Added

- **The storage hover line now names the key that flips the per-storage switch.** A chest or drawer
  reads `Wood chest (pull: on) (press N to toggle)`, and when no key is bound it says
  `(no toggle key is set)` instead. The suffix reported the state but not how to change it, and because
  `Toggle storage key` is a server-authoritative setting a client cannot necessarily read it out of
  their own configuration file - so the hover line is the only place a player can learn it.
- Both kinds of storage now build that text through one function. A chest and a drawer produce hover
  text through different game methods, and two call sites each appending their own string is how the
  drawer half came to show a state with no key while the chest half showed neither.

## [0.1.4] - 2026-10-12

### Fixed

- **The per-storage switch still did nothing on chests.** `TogglePatches` carried its two targets
  (`Container.GetHoverText` for the hover suffix and `Player.Update` for the hotkey) on its methods
  only, so it was skipped for the same reason as the four classes fixed in 0.1.3 — the class-level
  `[HarmonyPatch]` in that file sat on `DrawerHoverTextPatch`, the drawer half, not on `TogglePatches`.
  Drawers therefore showed their switch state while chests did not, and the hotkey never fired. The
  class now carries a bare `[HarmonyPatch]` too; no code moved.
- The start-up patch report made this visible: 0.1.3 listed 19 patched methods, and the only two
  declared sites absent from that list were exactly these.

## [0.1.3] - 2026-10-12

### Fixed

- **The real reason the mod did nothing in game: almost every patch was silently never applied.**
  HarmonyX 2.9.0 only treats a type as a patch class when it carries a **class-level**
  `[HarmonyPatch]`. A class whose only `[HarmonyPatch]` attributes sit on its *methods* is skipped
  entirely: it applies nothing, throws nothing and logs nothing, while BepInEx reports the plugin
  loaded and "0 failed". `CraftPatches`, `PullKeyPatches`, `StationPatches` and `TerminalPatch` were
  written that way, so crafting, building, stations, the pull key and the console command were all
  inert. Each of those classes now carries a bare `[HarmonyPatch]` as well; the attribute states no
  target, so the existing method-level targets still supply it and no code moved.
- A live patch count was what found this: the start-up line reported **3** patched methods (the two in
  `ContainerTrackingPatches` and one in `TogglePatches`, the only classes already annotated at class
  level) where roughly twenty were intended.

### Added

- The start-up report now names the patched methods, so a patch whose target fails to resolve shows up
  as a gap rather than being hidden by a count.
- `Tests/PatchApplicationTests.cs` settles patch placement with throwaway targets and no game
  assemblies: it proves both class-level forms are applied by the shipped HarmonyX, so this class of
  failure can never pass the test suite again. The Logic tests could not see it, because the test
  project compiles the Logic sources in rather than referencing the product assembly.

## [0.1.2] - 2026-10-12

### Fixed

- **The mod did nothing at all in game, for a second reason.** `Plugin.Awake` called Harmony's
  no-argument `Harmony.PatchAll()`, which does not patch the calling assembly — it *infers* one with
  `new StackTrace().GetFrame(1).GetMethod().ReflectedType.Assembly`. When that inference does not
  resolve to this mod's assembly, Harmony patches the wrong assembly instead: it finds no
  `[HarmonyPatch]` classes there, applies **zero patches**, throws nothing and logs nothing. The
  assembly is now passed explicitly (`Harmony.PatchAll(typeof(Plugin).Assembly)`), so the inference is
  gone. This is why 0.1.1 could fix the config fault and still do nothing in game.

### Added

- A permanent start-up line reporting how many game methods this mod's Harmony instance patched (and
  what the old inference would have chosen), with an error when the count is zero. 0.1.1 loaded
  cleanly, threw nothing and patched nothing, and nothing in the log could tell that apart from a mod
  that works and simply finds no storage nearby — which cost two test cycles to discover.
- A one-shot breadcrumb when the container-tracking patch first runs, naming the first container and
  whether it was tracked, and a one-shot breadcrumb in the console hook naming the first console line
  it sees.

## [0.1.1] - 2026-10-12

### Fixed

- **The mod did nothing at all in game.** `PluginConfig` registered its own config file with Jötunn's
  `SynchronizationManager.RegisterCustomConfig`, which always throws for a plugin's default mod config
  ("already synchronized"). The exception aborted `Awake` *before* `Harmony.PatchAll()`, so **no patches
  were applied** — no chest pulling, no drawer pulling and no per-storage switch — and BepInEx still
  reported the plugin as loaded, so nothing in the log marked it as a failure.
- Config initialisation is now guarded: a failure there can no longer prevent patching, and it logs an
  error and disables the mod explicitly rather than leaving it silently inert.
- Declared `[SynchronizationMode(AdminOnlyStrictness.Always)]` so the server-authoritative settings are
  enforced as intended.

### Notes

- The server and every client must run the **same patch version**
  (`NetworkCompatibility(EveryoneMustHaveMod, VersionStrictness.Patch)`), so update both sides together
  or the join is refused by design.

## [0.1.0] - 2026-10-11

### Added

- Initial release: mod identity and Thunderstore packaging.
