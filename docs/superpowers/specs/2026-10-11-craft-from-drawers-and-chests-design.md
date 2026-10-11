# Craft From Drawers And Chests — Design

Date: 2026-10-11
Status: draft for approval

## Intent

A Valheim mod that lets crafting, building and station-feeding draw materials from
storage **within range of the player**, where "storage" means both vanilla chests and
KG Item Drawers drawers, treated as equals.

It replaces **two** mods on the author's server: CraftFromContainers (which has the
ctrl-click ingredient pull but cannot see drawers) and AzuCraftyBoxes (which sees
drawers but is not the mod wanted). Two mods that both pull from storage cannot safely
coexist, so one mod has to do both jobs.

The felt experience: you keep storage in drawers on the walls around your base, dump your
inventory onto the floor to auto-file it, and then never walk to a chest while crafting.

## Goals

- **G1 Behavioural parity.** Retain every CraftFromContainers 4.0.7 feature that is
  compatible with this design (see the inventory below).
- **G2 Storage parity.** A drawer and a chest are equally valid sources; no feature works
  for one and not the other.
- **G3 Distance is the only spatial rule.** One range setting. Storage within range of the
  player is eligible. No per-station groups, no per-container assignment.
- **G4 Per-storage switch, shared by everyone.** Any individual chest or drawer can be
  excluded from pulling. Toggled with a hotkey while hovering it; the current state is
  visible on hover. Whoever flips it, it is off for everyone.
- **G5 Server-authoritative configuration.** Settings live on the server, are editable
  through the BepInEx ConfigurationManager, and are pushed down to clients.
- **G6 Mod-set enforcement.** Every player must have the mod, at a compatible version.
- **G7 No hand-rolled inventory mutation.** Item movement goes through the owning mod's
  supported path (see Mutation paths).

## Non-goals

- Not a replacement for MultiUserChest or Item Drawers; both stay installed.
- Not a per-station / per-group configuration system. Explicitly rejected: the author's
  drawers ring the base and only distance matters.
- Not per-player pull permissions. Explicitly rejected in favour of a world-shared switch.
- Not support for `mkz.itemdrawers` (a different drawer mod that stores drawers as real
  `Container`s). The author runs KG Item Drawers.
- Not a standalone mod: it assumes the server's mod set.

## Anti-goals (what would make this a failure)

- **Item duplication or loss.** Two players pulling at the same moment must never both get
  the item, and an item must never exist twice or vanish.
- **Desync with MultiUserChest.** Writing chest inventories directly, the way
  CraftFromContainers does today, would desync against MultiUserChest's protocol.
- **Pulling from storage the player did not authorise** — including storage they switched
  off, and storage out of range.
- **Silent partial failure.** A craft that consumes some materials and then fails must not
  eat anything.

## Constraints (verified on the author's machine, 2026-10-11)

| Thing | Version / fact |
|---|---|
| BepInEx | 5.4.2350 (`tools/New-ValheimRefs.ps1` default; core assemblies copied to refs) |
| Jötunn | 2.30.2 (`net462`; in the Gale profile and in the NuGet cache as `jotunnlib`) |
| Valheim | publicized refs built by `tools/New-ValheimRefs.ps1` into `.references` |
| CraftFromContainers | 4.0.7 — the behavioural baseline |
| AzuCraftyBoxes | 1.8.27 — reference for the storage abstraction and the toggle |
| Item Drawers (`kg_ItemDrawers`) | 1.4.0 |
| MultiUserChest | Jötunn-based; version mismatch = no connection at all |
| Target framework | `net462` (all of the above are `net462`) |
| Deployment | Server and every client, same build |
| Licence: CraftFromContainers | public domain (aedenthorn/ValheimMods) |
| Licence: AzuCraftyBoxes | MIT-0 |
| Licence: MultiUserChest | MIT |

Consequence of the licences: porting code from any of the three is permitted. The design
still prefers calling MultiUserChest's API over copying its logic, because its protocol is
the thing that must be respected.

Publishing: Thunderstore, team `NorCal_Nerds`, package `CraftFromDrawersAndChests`; possibly
listed on Hexium. Not Nexus, so there is no `nexusID` and no Nexus-flavoured README.

## Decided: the storage model

One abstraction, two implementations, plus a range query.

- **Chests** — vanilla `Valheim.Container` components. Keep CraftFromContainers' existing
  approach: track them on `Container.Awake` / `Container.OnDestroyed`, then filter the
  tracked set by distance, chest-type settings and access checks. One deliberate departure
  from CraftFromContainers: it also skipped chests that another player had in use. **This mod
  does not.** An open chest is still a valid source, and it stays valid even when the player
  browsing it is someone else.
- **Drawers** — *not* `Container`s. Drawers are `ZNetView` objects whose ZDO carries the
  item type and count. Discovery is a reflection call into Item Drawers, deliberately
  avoiding a compile-time dependency:
  - Preferred contract: `API.ItemDrawers_API`, which ships inside the Item Drawers assembly
    and offers `AllDrawers()` and `AllDrawersInRange(pos, range)`, plus a `Drawer` wrapper
    (`Prefab` / `Amount` / `Quality`, and `Remove` / `Withdraw` / `Add`).
  - Fallback: `API.ClientSideV2.AllDrawers()` → `List<ZNetView>`, with our own distance
    filter. `ClientSideV2` exposes only `AllDrawers()`; the older `API.ClientSide` is gone
    in 1.4.0.
  - Per-drawer state read from the ZDO: `"Prefab"` (string), `"Amount"` (int), `"Quality"` (int).
  - If neither type resolves, the mod runs chest-only. Drawers are a **soft** dependency.
- **Range** — a single float setting, applied identically to both kinds. The *origin* differs
  by feature: the player for crafting, building and the ctrl-click pull, and the station for
  the fuel / ore / cooking features, matching CraftFromContainers. This is the one place the
  "distance from the player" goal bends, and it bends for parity.

Because the drawer API hands back every drawer in the world, the range filter matters for
performance as much as for behaviour.

## Decided: the two mutation paths

- **Chests → `MultiUserChest.ContainerHandler.RemoveItemFromChest(...)`.** A public
  extension method that routes the removal through MultiUserChest's request/response
  protocol to whichever player is currently managing that chest. Removing items any other
  way defeats the protocol. `ContainerHandler` also offers `AddItemToChest` and
  `MoveItemInChest` if needed. Two cases, and the difference matters:
  - **The chest is open by another player** (it has a live manager): send the request and let
    that player apply or refuse it. Do **not** claim ownership — taking a chest over from the
    player using it is exactly the desync the anti-goals forbid.
  - **The chest is closed** (no manager): take ownership first, then remove. There is nobody
    to route the request to.
  A refusal falls under the all-or-nothing rule: nothing is consumed.
- **Drawers → RPCs on the drawer's `ZNetView`.** Item Drawers exposes exactly three:
  - `ForceRemove` (int) — remove items, after `ClaimOwnership()`. **This is the one to use**,
    for pulling and for consuming alike; the mod then adds the items to the player itself.
  - `WithdrawItem_Request` (int) — do **not** use. It spawns a world `ItemDrop` at the player
    and only moves it into the inventory if it fits, so material can land on the ground; on an
    empty or invalid drawer it also clears the drawer's `Prefab` / `Amount` / `Quality`.
  - `AddItem_Request` (prefab, amount, quality) — put items in.

Hard rule for this codebase: **the mod never writes to `Container.m_inventory` and never
writes a drawer's ZDO `"Amount"` directly.**

Second hard rule: **pulls are all-or-nothing.** Removing an item from a chest is a *deferred*
request/response in MultiUserChest — it can be refused, and per
`InventoryHandler.RPC_RequestItemRemoveResponse` the item can even be dropped on the ground.
So the mod must secure the whole requirement before anything is consumed, and if any part of
it cannot be secured, consume nothing. This is what keeps the "no silent partial failure"
anti-goal true, and it constrains how directly CraftFromContainers' synchronous "pull, then
let vanilla consume" architecture can be ported.

## Decided: the per-storage switch

- A single boolean on the storage's own ZDO. "Off" means excluded from every pull, for
  everyone.
- Flipped by a configurable hotkey while the player hovers the storage, calling
  `ClaimOwnership()` first.
- Hover text gains a line showing the current state.
- Works unchanged for chests and drawers, since both are `ZNetView`s with ZDOs.
- This is a **world** property, not a player property. It travels with the save.
- **Scope:** the switch governs *this mod's* pulling. It does not disable Item Drawers' own
  native behaviour — auto-collecting matching items off the floor and pushing them to nearby
  players — which is a separate feature the author relies on.

## Decided: configuration, sync and enforcement

- Config entries are registered with Jötunn so the server's values are authoritative:
  `SynchronizationManager.Instance.RegisterCustomConfig(configFile)`. Jötunn handles the
  push to clients and locks the entries for non-admins, so the BepInEx ConfigurationManager
  remains the editing surface on the server.
- `[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]`
  from `Jotunn.Utils` on the plugin class. This is the same enforcement MultiUserChest uses
  ("version mismatch = no connection").
- Declare the mods it replaces as incompatible so the failure is loud rather than a desync:
  `[BepInIncompatibility]` for CraftFromContainers and AzuCraftyBoxes (GUIDs to confirm).
- Dependencies: hard on Jötunn; soft on Item Drawers; MultiUserChest is present in every
  supported environment — see "left to the builder" for hard-vs-soft.

## Feature inventory to retain

Baseline: CraftFromContainers 4.0.7 `BepInExPlugin.cs`. Its configuration surface:

| CFC setting / behaviour | Disposition |
|---|---|
| `modEnabled` | Keep |
| `maxRange` | Keep — this becomes the single storage range (G3) |
| `pullItemsKey` (ctrl-click "pull ingredients to me") | Keep — a headline feature |
| `pullByDistance` | Keep |
| `leaveOne` | Keep |
| `resourceString`, `pulledMessage` | Keep |
| `flashColor` / `unFlashColor` (container flash on use) | Keep |
| `showGhostConnections`, `ghostConnectionStartOffset`, `ghostConnectionRemovalDelay` | **Drop.** They draw container ↔ crafting-station links; this design removes station grouping, so they have no subject left. |
| `ignoreShipContainers`, `ignoreWagonContainers`, `ignoreWoodChests`, `ignorePrivateChests`, `ignoreBlackMetalChests`, `ignoreReinforcedChests` | Keep (chest-only by nature) |
| `pullFuel` (Fireplace), `pullOre` (Smelter), `pullCooking` (CookingStation) | Keep |
| `fuelDisallowTypes`, `oreDisallowTypes` | Keep |
| `preventModKey`, `fillAllModKey`, `switchPrevent` | Keep |
| `isDebug` | Keep |
| `nexusID` | **Drop** — the mod is published to Thunderstore (and possibly Hexium), not Nexus. |
| Odin's QoL compatibility (stack-size / weight modifiers) | **Drop** — Odin's QoL is not in the mod set. |
| Turret ammo patch | Keep |
| Terminal command patch | Keep |
| Crafting-panel refresh when the pull key is held | Keep |
| Recipe/building "do I have the materials" checks counting storage | Keep — must count drawers too |
| Consuming requirements from storage when crafting/building | Keep — must consume from drawers too |

New behaviour with no CFC ancestor: the per-storage switch (G4).

## Behavioural baseline

CraftFromContainers 4.0.7's `BepInExPlugin.cs` (now public domain) **is** the behavioural
specification. The job is to port that behaviour and change only three things:

1. the storage layer (chests + drawers behind one abstraction),
2. the mutation paths (MultiUserChest for chests, RPCs for drawers),
3. the configuration/sync layer (Jötunn, server-authoritative).

Recorded architecture note: CFC's model for crafting is *pull the required items into the
player's inventory, then let vanilla consume them*. Keep that unless a reason emerges —
it is what makes the feature work uniformly for crafting, building and stations.

## Build and toolchain

`tools/` was lifted from another of the author's mods. It has already been re-pointed at
this one — every reference to the old mod's identity is gone — but it still has to be made
to actually build this mod:

- `Build.ps1`: nuget restore → validation → MSBuild → Release package + zip. Its validation
  checks `ModInfo.Version` against `manifest.json` and the packaged `manifest.json`;
  those identities must be updated. Same for the package folder name and zip names.
- `New-ValheimRefs.ps1`: unchanged in principle, plus new references:
  - **Jötunn** from the `jotunnlib` NuGet package (`net462`).
  - **MultiUserChest** — `MultiUserChest.dll`, taken from the installed mod set.
  - Item Drawers is **not** referenced at compile time; it is reached by reflection, the
    same choice AzuCraftyBoxes makes.
- The repo is otherwise empty: the solution, project, `Directory.Build.props` and source
  layout still need creating.

## Calls I made (check these while reading)

- Chest removal goes through MultiUserChest rather than a local `RemoveItem`, even though
  copying CFC's version would be simpler.
- The pull-into-inventory operation uses the drawer's `WithdrawItem_Request` RPC rather than
  `ForceRemove` + manually adding to the player.
- The per-storage switch is stored on the object's ZDO, so it is shared, not local.
- The toggle hotkey and hover line are modelled on AzuCraftyBoxes because that is the
  interaction the author already knows.
- Drawers are a soft dependency and chests are the guaranteed path.
- `mkz.itemdrawers` support is dropped.

## Resolved during builder check

- **Pulls are all-or-nothing** — second hard rule under Mutation paths. This is what makes
  the deferred chest protocol safe to build on.
- **Drawers are emptied with `ForceRemove`**, never `WithdrawItem_Request`.
- **One range value, feature-dependent origin** — player for crafting / building / ctrl-click,
  station for fuel / ore / cooking.
- **Ghost connections are dropped**, because their subject no longer exists.
- **The storage switch does not touch Item Drawers' own automation.**
- **Drawer reflection prefers `API.ItemDrawers_API`**, falling back to `API.ClientSideV2`.
- **In-use chests stay eligible.** CraftFromContainers' `IsInUse()` exclusion is dropped: an
  open chest is pulled from through MultiUserChest's manager-routed request, and a closed
  chest is owned first.

## Left to the builder

- Hard vs soft dependency on MultiUserChest. Recommendation: make it **hard**
  (`BepInDependency`), because with it absent the only working implementation is exactly
  the direct mutation this design forbids.
- Mod identity: name, GUID, Thunderstore team (`NorCal_Nerds`, the team already used by the
  author's other Valheim mods).
- The precise Jötunn calls for registering a synced config and for reacting to a config
  change arriving from the server (`OnConfigurationSynchronized`).
- Which of CraftFromContainers' Harmony patch points survive contact with the current game
  version — the source is the reference, but it targets an older Valheim.
- Where the toggled-off state is read when building the eligible-storage list, and whether
  the list is cached per-frame as CFC does (`lastPosition` / `cachedContainerList`) or as
  AzuCraftyBoxes does (a time-windowed cache).
- Testability: which parts of this can be covered by unit tests outside the game
  (requirement arithmetic, range filtering, the eligibility predicate) versus only by hand
  in-game.
- Verify in MultiUserChest's source that a removal request aimed at a chest with a live
  manager is actually applied by that manager, and that one aimed at an unowned chest is not
  silently dropped. The in-use decision rests on this.
