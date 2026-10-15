# Author verification checklist

Every task's in-game step, consolidated. The author runs these on a real Valheim
server and client before a release. Build, test and static verification
belong to the controller and are recorded in `progress.md`; the steps here cannot
be automated and do not block a task's review.

**Deploy for every check:** the built plugin to `BepInEx\plugins\` on **both** the
server and the client, using the same build on each side.

---

## Validation log — what a build has actually been through in game

The checkboxes below are the full list; this log records which builds the owner has run, so that
"reviewed clean" is never mistaken for "works".

- **Early 0.1.x, single player (host):** the `cfdac` console command; crafting from chests and drawers;
  the ctrl-click pull with the hammer and at crafting benches; the per-storage hover naming its state
  and key.
- **0.1.12, single player (host):** `left shift+E` fills a fireplace's and a smelter's fuel and ore
  correctly and with no spurious message; the fireplace's fill hint and the smelter's tooltips are
  present.
- **0.1.13, single player (host):** backpack materials count for crafting — a recipe wanting 1 Boar
  Meat reads `24/1` with 25 carried, the −1 being Adventure Backpacks' *Leave One Item In Backpack*
  working as designed — and 53 carried Chain reads `52/7` for a recipe needing 7. Single-item station
  adds print no "you don't have any …" message. A drawer whose pull switch was **off** correctly
  contributed nothing; that investigation (it cost a full diagnostic round) is why the readout log
  exists.
- **1.0.1, multiplayer — this mod on the server and on a client:** station feeding works when the
  **client** feeds a station, not only the host. That settles the fresh-eyes review's I3 (the station
  confirmation re-reads the station's ZDO immediately after `InvokeRPC`, which is trustworthy only on
  the peer that executes the call), which does not reproduce in play.
- **1.0.1, multiplayer — a second player's backpack is not a pull source** (owner, 2026-10-13): with a
  second player wearing an Adventure Backpacks backpack in range, the ctrl-click pull takes nothing out
  of it. This holds by construction as well as by observation: every player reference in `Patches/`,
  `Pull/` and `Storage/` is `Player.m_localPlayer` (the build-pull prefix returns early on
  `!ReferenceEquals(__instance, Player.m_localPlayer)`), and no path enumerates another player's
  inventory. The **station-feed** half of the same check was not reported separately; the same
  construction covers it, but that half is not a play test.
- **1.0.2, single player (owner, 2026-10-14):** the reported defect is fixed — repeating the ctrl-click
  pull on the same recipe fetches the materials again each time, so the materials for a second portal
  come from a second click. The other two 1.0.2 checks — a base that can only partly cover the cost, and
  a nearly full inventory — were not reported on this pass.
- **1.0.4, single player (host), 2026-10-16 — run on Valheim 1.0.20:** the owner installed the release DLL into
  the `Modheim` profile by hand and launched. The game itself reports `Valheim Version: 1.0.20`; the plugin
  loaded, all 22 Harmony patches applied, the container-tracking patch was live and tracked a
  `piece_chest_wood(Clone)`, and the session logged **0 errors** (12 warnings). This is the **load** path: no
  feature appears in the log, so the five checks under "Valheim 1.0.20 update" stay open. No code change was
  needed — the update renamed `ItemDrop.ResetVariantIfInvalid` → `EnsureValid` and added a cheat-flag system,
  touching none of the 24 members this mod binds.

Settled by owner decision, not defects:

- **The ctrl-click pull does not count the equipped backpack** (review I2): crafting and building count
  it, the on-demand pull counts the player's inventory only. The owner's ruling: "fine the way it is".
  The README line this asked for landed in 1.0.2: *Ctrl-click to pull* now states that the pull brings
  the recipe's **full** materials whatever the player is already carrying, which covers this case.

---

## Task 7 — plugin bootstrap

- [ ] Launch with the plugin on server and client. The BepInEx log shows the plugin
      loading with no Harmony patch errors.
- [ ] Remove MultiUserChest from one client and try to join. Expected: the connection
      is **refused** — that is Jötunn enforcement (`EveryoneMustHaveMod` +
      `VersionStrictness.Patch`) working.

## Task 8 — server-authoritative config

- [ ] On the server, change `Maximum range` through the ConfigurationManager window.
- [ ] Join from a client whose own local config has a **different** value for it.
      Expected: the client behaves with the **server's** value, and the entry is read-only
      for that non-admin client.
- [x] Settled in 1.0.1, both by the owner: `Toggle storage key` ships as **`x`** — a single key, and
      never a movement key, because the toggle fires on a key-down with whatever storage is under the
      crosshair — and `Debug` ships **off** so a fresh install does not write this mod's diagnostics
      into the player's log.

## Task 9 — drawer discovery and the pull invariant

- [ ] With Item Drawers installed on the server **and** the client, place a few drawers.
      Expected: they are discovered as storage, and their prefab and amount read correctly.
- [ ] Fill a drawer with **more than one stack** of its item, empty a player's inventory, and
      pull the whole amount. Expected: every stack arrives, nothing is lost.
- [ ] Edge case worth reproducing deliberately: give the player a same-name partial stack at
      a **different quality**, fill the inventory so there is no empty slot, then pull.
      Expected: that chunk is **refused** — nothing is removed from the drawer.
- [ ] With a second player, have both pull from the same drawer at the same moment.
      Expected: the item leaves the drawer exactly once.

## Task 10 — chest registry and storage collection

- [ ] **Range filtering (the brief's in-game step).** Place two chests — one inside `Maximum
      range` of the player, one outside — and a drawer inside range. Add temporary logging to
      `StorageRegistry.Collect` printing each surviving `IStorageSource`'s `ObjectName`, `Kind`
      and distance from the origin. Expected: the far chest is **absent**, the near chest and the
      drawer are **present**.
- [ ] **Empty world.** With no chests in the world, confirm `Collect` returns an empty list and
      logs no exception.
- [ ] **Chest-name-prefix spot-check** (deferred from Task 2, still outstanding): confirm the four prefixes `piece_chest_wood(`, `piece_chest_private(`, `piece_chest_blackmetal(` and `piece_chest(` match live Valheim prefab names, so the ignore-this-type settings work.
- [ ] **Tracking predicate.** Place a normally built chest (`piece_chest(Clone)`): it must be
      tracked. Confirm a container that is neither a `piece_`/`Container`-named object nor carries a
      non-zero `creator` in its ZDO is **not** tracked.
- [ ] **Loose container skipped.** A container that is not inside a `Piece` must be skipped by
      `Collect`, not thrown on.
- [ ] **Guard stone.** Inside another player's guarded area, a chest with guard-stone privacy must
      be skipped where `PrivateArea` denies access, and included where it does not.
- [ ] **Chest-name-prefix spot-check (carried from Task 2).** Confirm `piece_chest_wood(`,
      `piece_chest_private(`, `piece_chest_blackmetal(` and `piece_chest(` still match live
      Valheim prefab names, so `ChestTypeRules`' four rules exclude the chests they name.

## Task 11 — MultiUserChest removal bridge

- [ ] With a second player holding a chest open, trigger the removal path. Expected: the item moves
      **exactly once**; both clients show consistent chest contents; the second player sees the
      change; and the bridge returns **`-1`** (routed), not `0`.
- [ ] Confirm the synchronous-availability predicate reports false for the chest that player holds
      open, and true for a chest nobody has open.
- [ ] With the chest closed, remove an item and confirm it reaches the destination and the return
      is the true count.

## Task 12 — per-storage switch

- [ ] `Toggle storage key` ships as **`x`** (`ConfigModel.DefaultToggleStorageKey`), so the switch is live on a fresh config — clear the entry if you want it inert. **Do not** bind it to a movement key: it applies to whatever storage is under the crosshair, so `W`/`A`/`S`/`D` would switch storage off silently as you walk (changed in 1.0.1; the original port shipped `left shift + e`).
- [ ] Hover a chest and press it. Expected: the hover text flips to "(pull: off)" and the chest
      stops being a pull source; then confirm an unset key throws nothing.
- [ ] Have a second player look at the same chest. Expected: they see it off too — the state is
      shared, not local.
- [ ] Restart the server. Expected: still off (the ZDO persisted).
- [ ] Hover a **drawer** and do the same. Expected: the drawer's hover line appears and flips.
      This path is confirmed only out of game, so it is the check most likely to fail.
- [ ] Toggle a chest the second player currently has open. Expected: it works and their client is
      not disturbed (`Flip` claims ownership to write the ZDO).

## Task 13 — craft and build pulls

- [ ] **Split across stacks.** Put 2 × 30 Wood in one chest and require 50, carrying none. Expected:
      it crafts, 50 leave the chest, and **no free item** appears. Repeat with 3 × 20 so the request
      spans three stacks.
- [ ] **Split chest + drawer.** Materials split between a chest and a drawer, carrying none: crafts,
      and the materials leave both.
- [ ] **Nothing in range.** A requirement larger than everything in range: the recipe is not marked
      available and crafting consumes nothing.
- [ ] **Full inventory, no room for the shortfall.** Carry none of a recipe's materials and fill the
      inventory so no slot is empty and no partial stack holds the material at the same quality and
      world level. Expected: the recipe is **not** offered and crafting consumes nothing. Repeat for
      building: a piece whose materials are in a chest must not be placeable.
- [ ] **One empty slot, shortfall that fits.** Expected: the craft/placement still succeeds — the gate
      must not be over-strict when the shortfall genuinely fits.
- [ ] **Output reservation.** Fill the inventory so the pulled shortfall would fit but the crafted
      output has nowhere to go. Expected: the craft is declined (vanilla's own guard would refuse it
      too).
- [ ] **Crafting bonus.** At a station with a crafting skill, with a nearly-full inventory and the
      materials only in storage, craft until the bonus actually fires (you will see the "+N" text at
      the station). Expected: the craft is not offered, or completes with **nothing kept unpaid**.
- [ ] **Upgrade — first press only.** With the panel **not yet pressed** — the first time you select
      the upgrade recipe after opening the station — put the materials in storage and fill the
      inventory so only those materials fit. Expected: the upgrade button is **interactable** and the
      upgrade succeeds. **Re-open the station between repeats**: `m_craftUpgradeItem` is null until
      the button is pressed, so a repeat in the same session would pass even with the defect.
- [ ] **Single-ingredient recipe.** A `requireOnlyOneIngredient` recipe whose ingredients are all in a
      chest (none carried) must not show as available and must craft nothing; with the ingredient
      carried it still crafts.
- [ ] **Residual race (deliberately not closed).** With a second player, empty a chest between the
      panel showing the recipe as available and the craft landing. Expected: confirm what happens and
      whether an item is awarded unpaid. This is the one hole left open by design.

## Task 14 — ctrl-click pull, messages and flash

- [ ] **Range: the default is 10 m and `0` means unlimited.** `ConfigModel.DefaultMaxRange = 10f` already matches CraftFromContainers' `ContainerRange` (CFC 4.0.7 line 93), so nothing needs adjusting to compare like-for-like, and `Eligibility.IsWithinRange` treats a **non-positive range as unlimited** (`range <= 0f || sqrDistance <= range * range`) — CFC parity (CFC 173), so **both** gaps Task 15e tracked are closed. Worth one pass in play: set `0`, stand well away from a chest, and confirm it is still used; set `10` and confirm a chest beyond 10 m is not. Unlimited means every storage you own is scanned.
- [ ] **`Pull items key` ships as `left ctrl`** (`PluginConfig.cs:133`), so ctrl-click pull works out of the box. **`Toggle storage key` ships as `x`** (`PluginConfig.cs:135`, `ConfigModel.DefaultToggleStorageKey`), not empty, so the switch is live on a fresh config — and it must never be a movement key, because it fires on a key-down with whatever storage is under the crosshair (1.0.1).
- [ ] Ctrl-click a recipe whose materials are in a chest and a drawer. Expected: the recipe is **not** built, the
      ingredients land in your inventory, and the pulled message shows.
- [x] **Repeat pulls fetch repeat materials (1.0.2, R32).** Ctrl-click the same recipe twice with enough in
      the base for both. Expected: the second pull supplies the whole cost again — the ingredients for two
      portals, not one. Before 1.0.2 the second pull did nothing at all, silently. — **Confirmed by the
      owner, 1.0.2 single player, 2026-10-14.**
- [ ] **A base that can only partly cover the cost still contributes (1.0.2).** Ask for a recipe whose cost
      is 4 with only 2 left in the chests. Expected: the 2 arrive and the pulled message shows, rather than
      nothing arriving behind a refusal.
- [ ] **A nearly-full inventory no longer cancels the pull (1.0.2).** With room for part of the cost, the
      pull delivers the part that fits. With no room at all nothing moves and nothing is dropped — and for a
      single-ingredient recipe nothing is said (known rough edge, recorded in R32).
- [ ] **Readout flash.** With the panel open on a recipe the inventory cannot cover but storage can, the
      requirement's amount text flashes between `Flash color` and `Unflash color`. CFC's only flash is this
      readout — there is no world-object highlight to look for.
- [ ] **`Resource string` takes effect.** Change it (default `{0}/{1}`) and confirm the readout follows; a malformed
      format such as an unbalanced brace must fall back to the plain amount rather than throw.
- [ ] Fill your inventory completely and repeat the ctrl-click. Expected: nothing is pulled, **nothing is on the
      ground**, and a message names the missing items — each named **once**, even when a recipe lists it twice.
      **1.0.2 changes the message for a single-ingredient recipe:** the request is now capped to what the
      inventory can accept, so it caps to zero, moves nothing and says nothing — the rough edge recorded in
      R32 and the check above. For a multi-ingredient recipe the plan still fails as a set, so the room
      message still appears.
- [ ] Hold `Prevent mod key` (default `left alt`) and ctrl-click. Expected: the pull is suppressed and the click
      crafts as vanilla. With `Switch prevent = true`, the pull must happen **only** while the key is held.
- [ ] With the prevent key held, confirm the crafting panel shows **vanilla** availability — storage no longer makes
      an uncraftable recipe look craftable — and that the panel updates as you hold and release the key.
- [ ] Type a key with different capitalisation in the config (e.g. `Left Ctrl`). Expected: it still works.
- [ ] **The building-piece pull exists (Task 14b, shipped).** Equip the hammer, select a piece, hold the pull key
      and press attack: the piece's materials are pulled and the piece is **not** placed, exactly as CFC's
      `UpdatePlacement_Patch` does. Press again and expect a second piece's worth — 1.0.2 asks for the whole
      cost on every press (see the 1.0.2 checks above) — and releasing the key restores ordinary placement.

## Cross-cutting — recipe discovery (found 2026-10-11, needs a decision)

- [ ] The build gate attaches to `Player.HaveRequirements(Piece, RequirementMode)`, and the game calls
      that overload from **six** sites, not just placement: `BuildUiPieceButton.UpdateRequirements`,
      `Hud.UpdatePieceBuildStatus`, `Hud.UpdatePieceBuildStatusAll`, `PieceTable.UpdateAvailable`,
      `Player.UpdateKnownRecipesList` and `Player.UpdatePlacement`. `UpdateKnownRecipesList` is the
      **recipe-discovery** path. Expected consequence: a recipe whose materials are only in storage can
      be *discovered* where vanilla would require them in your inventory. **Decide whether that is
      wanted**, and compare against CraftFromContainers. It is not a safety defect — nothing is
      duplicated or lost — but it changes vanilla discovery semantics and nobody planned it.

## Task 14b — build-piece pull key

In game, on a world with a chest and a drawer holding the piece's materials, player carrying none:

- [ ] Equip the hammer, select a piece, hold the pull key and press attack. Expected: the piece is **not**
      placed, its materials land in your inventory, and the pulled message shows once.
- [ ] With the pull key held, confirm pressing attack again does **not** place the piece; release the key
      and confirm a normal attack **does** place it — the intercept must not outlive the key.
- [ ] Hold the pull key and attack for a piece **no** storage in range can pay for. Expected: nothing
      happens — CFC parity, the press is swallowed with no message and no pull.
- [ ] **Carry part of the materials** so the ghost still reads placeable from storage, then attack with the
      pull key. Expected: **only the shortfall** is pulled, and nothing you already hold is pulled again.
      This is what two review rounds turned on: pressing repeatedly must not drain storage stack by stack,
      and you must never be told you lack something you are holding.
- [ ] Repeat that with a **quality-mismatched** stack you are carrying (e.g. a quality 2 Wood stack where
      the recipe expects quality 1). Expected: it still counts as held and is still consumed as payment —
      vanilla consumes by name, so the mod must count it by name too. A free craft or a false refusal here
      is the regression the property test guards.
- [ ] Fill your inventory completely, then attack with the pull key on a piece storage can pay for.
      Expected: nothing pulled, **nothing on the ground**, and the message reports the room failure.
- [ ] Hold the prevent key and attack with the pull key (default `Switch prevent = false`). Expected: the
      press is vanilla and the piece places normally, paid from your own inventory.
- [ ] Set `Switch prevent = true`, reload, repeat both key states. Expected: the pull only happens while the
      prevent key is held.
- [ ] Open the build-piece picker so it is visible, then attack with the pull key. Expected: the picker
      behaves normally and nothing is pulled.
- [ ] A repair piece: attack with the pull key held. Expected: vanilla repairs; nothing is pulled.
- [ ] Repeat Task 14's ctrl-click recipe pull for a recipe you **partly** hold. Expected: only the shortfall
      is pulled — the same fix covers both sites.

## Task 15a — station feeding

Deploy the same build to the server and client. For each check, stand within `Maximum range` of the station.

- [ ] **Campfire.** A chest of Wood in range, carrying none: interact → 1 Wood leaves the chest and the fire
      gains 1 fuel. No duplicate, no message, and the Wood must **not** land on the ground or linger in your
      inventory.
- [ ] **Range is measured from the station.** A chest out of the *station's* range but within *yours* → nothing
      is pulled.
- [ ] **Smelter ore.** Chest of CopperOre in range, empty-handed: interact with the ore switch → 1 ore queued;
      the queue stops at `m_maxOre`; a `FuelDisallowTypes` entry blocks it.
- [ ] **Smelter and cooking fire fuel.** Fuel is pulled and the fire lights; a full fire pulls nothing.
- [ ] **Cooking station.** Chest with BoarMeat in range → 1 ingredient added to a free slot and it cooks; no pull
      while a done item is pending; an `OreDisallowTypes` entry blocks it.
- [ ] **Turret.** No ammo carried, a chest of matching arrows in range → 1 arrow loaded. Confirm it still feeds
      with `PullFuel`, `PullOre` and `PullCooking` all false — a turret has no flag of its own (CFC parity).
- [ ] **Prevent key.** Hold it → no station feeds at all.
- [ ] **Drawers feed stations too.** A drawer in range of a station feeds it, via `ForceRemove`.
- [x] **Two players, and no other player's backpack** — **partly confirmed in 1.0.1** (owner, 2026-10-13):
      with a second player wearing an AdventureBackpacks backpack standing in range, the ctrl-click pull
      takes nothing out of it. The **station feed** half is still unconfirmed in play.

## Task 15b — fill-all key and console command

- [ ] **Fill-all, fireplace.** Fuel (e.g. Wood) in a chest beside a fireplace. Hold **Left Shift** and press
      E: the interaction asks the chest for the fire's whole remaining capacity and takes as much as the
      chest can supply **into your inventory** — so the fire still advances one fuel per interaction,
      consuming the fetched stack over the following presses. Release Shift and press E: exactly one fuel
      moves. Repeat while also carrying fuel — with Shift held it still tops up; without Shift it consumes
      your own item only.
- [ ] **A chest holding less than the remaining capacity must still give that part**, and holding the key
      must **never** take less than releasing it. (This is the inversion two review rounds fixed: on a
      shortfall the old code pulled 0 where the released key pulled 1.)
- [ ] **Fill-all, smelter.** Same on the smelter's add-ore and add-fuel switches.
- [ ] **Hover hints.** The smelter shows the `[left shift+$KEY_Use] … max` line only when the mod is enabled
      and *Pull ore* / *Pull fuel* are on. The fireplace, cooking station and turret show no such hint.
- [ ] **Non-applicability.** A cooking station (both its fuel switch and its food) and a turret still move
      one item per interaction while Shift is held.
- [ ] **Command.** Console (F5): `cfdac` → usage; `cfdac status` → "Sources in range of the player: N
      (x chests, y drawers)" plus the "Item Drawers API:" line; `cfdac reset` → config reloaded, tracking
      cleared, and the re-awake caveat shown. An unrelated line (`help`) passes through untouched, and a
      mistyped subcommand shows usage rather than being swallowed.
- [ ] **Reset caveat.** After `cfdac reset` mid-session, pulls stop finding storage until containers
      re-awake — as the caveat warns.
- [ ] **Re-test all of the above after Task 15c.** Today the fill key pre-loads your **inventory**; the
      tooltip was reworded to say so honestly. Task 15c delivers into the station, which is when "fill to
      max" becomes literally true.

## Task 15c — fill-to-max parity

_Appended when Task 15c completes._

## Task 15d — AdventureBackpacks coexistence (0.1.13)

The backpack half of this is **implemented**; it still needs the owner's in-game pass, and the
two-player half needs a second player.

- [ ] **The backpack counts in the crafting panel.** Boar Meat (say 25) in an equipped backpack,
      nothing in the inventory: a cauldron recipe wanting 1 must read `25/1` (not `0/1`), the recipe must
      be offered, and pressing craft must work.
- [ ] **The backpack counts for a forge recipe too**, and for **building**: with an ingredient only in
      the backpack, the piece must be offered in the build menu.
- [ ] **Nothing is pulled *out* of the backpack.** Wood in the backpack and Wood in a chest, both in
      range: the recipe must be paid from the backpack, and **no Wood may end up sitting in the
      inventory** — pulling it out of the backpack and replacing it from the chest is the failure mode
      this design forbids.
- [ ] **The backpack is never a pull source.** A ctrl-click pull takes from chests and drawers only; the
      backpack's contents never move as a result of the pull key, whatever is in it.
- [ ] **`Left alt` is the prevent key, by design.** Holding it with a crafting or build panel open makes
      the panel show AdventureBackpacks' own number — **backpack counted, storage not**. This is the
      CraftFromContainers-port `Prevent mod key`, not a bug; rebind or clear that key if it is in the way.
- [ ] **The debug readout.** With `Debug` on, each requirement logs one `Readout '<prefab>' ('<shared
      name>'): held N + storage M of K required` line **per distinct answer** (not per frame).
- [ ] **The drawer-vs-recipe name check (the reported Dvergr lantern).** If an ingredient that is visibly
      in a drawer still reads 0, the same log line must print `nothing holds it;` followed by every
      in-range source with its kind, distance, count and each drawer's raw `zdo='<prefab>'x<amount>`.
      Compare that stored prefab string with the requirement's prefab — that is the whole point of the
      line, and it is the only place both halves are visible.
- [x] **Two players — the ctrl-click pull half is confirmed in 1.0.1** (owner, 2026-10-13): with a
      second player wearing a backpack in range, the pull takes nothing out of it. The **station feed**
      half was not reported separately; it should hold for the same reason (`Player.m_localPlayer` is the
      only player the station paths read) but is not yet a play test.

## Task 15c — AdventureBackpacks coexistence

_Appended when Task 15c completes._

## Valheim 1.0.20 update

Run the **same 1.0.4 build on the server and on the client**. Nothing in this mod changed for the update —
these checks exist because the game did: it added a cheat-flag system (`CheatUtils`, over the pre-existing
`ZDOVars.s_cheated`) and new save-version stamps (`Version/World::CheatCleanSlate`,
`Version/Player::CheatCleanSlate`). The first load of an existing world under the new version runs a
one-shot clean-slate migration this mod has never been exercised against, which is why check 1 comes first.
Evidence and reasoning: `game-1.0.20-compat-analysis.md` beside this file.

1. [ ] **Load a pre-update world.** A world saved under the previous version, with existing chests and
   drawers the mod has already tracked. Expected: it loads, the log still shows
   `Harmony patched 22 game method(s): …`, every container's contents are intact, and ctrl-click pull
   works. **Count one known chest before and after the load** — item loss or duplication is the failure
   this pins.
2. [ ] **No cheat flag left behind.** After pulling from a chest, storing into one, and placing a piece
   with storage materials, nothing the mod touched may read as cheated; in game that would surface as the
   object being cleaned up by the new migration on the next load. Expected: `cfdac status` and a normal
   save/reload leave everything in place. (The mod neither sets nor reads `CheatUtils`.)
3. [ ] **Costs are unchanged.** Craft a multi-ingredient recipe and place a piece with materials only in
   storage. Expected: the whole cost is consumed on every press, a second press still delivers (the 1.0.2
   fix), and no craft or placement ever costs nothing. Pins the new `Player.GetItemUpgradeChance` /
   `m_itemUpgradeSeeds` upgrade path against the `ConsumeResources` and `HaveRequirementItems` hooks.
4. [ ] **Client-side station feeding on a dedicated server.** Have the **client** (not the host) feed a
   fireplace and a smelter. Expected: fuel and ore arrive, the hover text is right, no spurious
   `you don't have any …` message, and nothing lands on the ground.
5. [ ] **MultiUserChest as a non-host client.** Store into and pull from a chest. Expected: contents
   survive a rejoin — no loss, no duplication, no duplicate delivery.

**Verification state at release (2026-10-16):** the **load** path is confirmed in game (see the validation log),
and the compatibility evidence is the assembly diff in `game-1.0.20-compat-analysis.md`. These five functional
checks were **not** run before 1.0.4 was published — the owner released on the strength of the load and that
analysis. Run them against the released build; any failure goes to `/investigate`, not to a patch from a guess.
