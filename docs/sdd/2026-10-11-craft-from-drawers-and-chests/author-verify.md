# Author verification checklist

Every task's in-game step, consolidated. The author runs these on a real Valheim
server and client before the 1.0.0 release. Build, test and static verification
belong to the controller and are recorded in `progress.md`; the steps here cannot
be automated and do not block a task's review.

**Deploy for every check:** the built plugin to `BepInEx\plugins\` on **both** the
server and the client, using the same build on each side.

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
- [ ] Settle two config defaults before release: `Toggle storage key` ships **empty**, so the
      per-storage switch cannot be triggered until it is set; `Debug logging` ships **on**
      (CraftFromContainers parity), which makes the log noisy by default.

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

- [ ] Set `Toggle storage key` first — it ships **empty**, so the feature is inert until then.
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

- [ ] **Set `Maximum range` before you start.** It defaults to **4**, where CraftFromContainers' `ContainerRange` defaults to **10** (CFC 4.0.7 line 93) — a 2.5× tighter default, so raise it to 10 to compare like-for-like. **Never set it to 0:** CFC treats 0 as *unlimited* (CFC 173), ours treats 0 as *nothing in range* — a known parity gap, tracked as Task 15e.
- [ ] **`Pull items key` ships as `left ctrl`** (verified in `PluginConfig.cs:132`), so ctrl-click pull works out of the box. **`Toggle storage key` ships empty** (`:134`), so the per-storage switch does nothing until you set it.
- [ ] Ctrl-click a recipe whose materials are in a chest and a drawer. Expected: the recipe is **not** built, the
      ingredients land in your inventory, and the pulled message shows.
- [ ] **Readout flash.** With the panel open on a recipe the inventory cannot cover but storage can, the
      requirement's amount text flashes between `Flash color` and `Unflash color`. CFC's only flash is this
      readout — there is no world-object highlight to look for.
- [ ] **`Resource string` takes effect.** Change it (default `{0}/{1}`) and confirm the readout follows; a malformed
      format such as an unbalanced brace must fall back to the plain amount rather than throw.
- [ ] Fill your inventory completely and repeat the ctrl-click. Expected: nothing is pulled, **nothing is on the
      ground**, and a message names the missing items — each named **once**, even when a recipe lists it twice.
- [ ] Hold `Prevent mod key` (default `left alt`) and ctrl-click. Expected: the pull is suppressed and the click
      crafts as vanilla. With `Switch prevent = true`, the pull must happen **only** while the key is held.
- [ ] With the prevent key held, confirm the crafting panel shows **vanilla** availability — storage no longer makes
      an uncraftable recipe look craftable — and that the panel updates as you hold and release the key.
- [ ] Type a key with different capitalisation in the config (e.g. `Left Ctrl`). Expected: it still works.
- [ ] **Not built yet (CFC parity gap):** there is no pull for a *building piece*. CFC pulls the selected piece's
      materials when you attack with the pull key held. Scheduled as Task 14b — re-confirm after it lands.

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
- [ ] **Two players, and no other player's backpack.** With a second player wearing an AdventureBackpacks
      backpack standing in range, neither a station feed nor a ctrl-click pull may take anything out of it.

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

## Task 15d — AdventureBackpacks coexistence

_Appended when Task 15d completes._

## Task 15c — AdventureBackpacks coexistence

_Appended when Task 15c completes._
