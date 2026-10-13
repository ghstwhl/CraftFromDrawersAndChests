# Task 15d — AdventureBackpacks coexistence: findings and the 0.1.13 fix

Owner's report (2026-10-13), verbatim:

> If I go to craft something at the cauldron that takes 1 boar meat, and I have all the boar meat (25)
> in my backpack, the crafting menu shows "0/1" instead of "25/1" amd I can not craft it.
>
> Similarly, crafting at the forge fails to report the inventory in my backpack as available. But if I
> hold down the left-alt key, the inventory is inversed and it shows backpack items as available but NOT
> items from iten drawers.

## What the "left alt" half is: not a defect

`Prevent mod key` defaults to `left alt` (a CraftFromContainers 4.0.7 port) and the owner's live config
has it at that default (`NorCal_Nerds.CraftFromDrawersAndChests.cfg`). While it is held,
`PullKeyRules.AllowByKey` refuses, so `SetupRequirementPostfix` returns early and **counts no storage at
all** — and vanilla's own text is a bare integer, which AB's own `InventoryGuiSetupRequirementPatch`
rewrites from `player.GetInventory().CountItems(...)`. So with Alt held the panel shows AB's number:
backpack in, drawers out. The observed "inversion" is the prevent gate working as designed, plus the
proof that AB's contribution is present in the vanilla path we were overwriting.

## What the real defect was

`PullEngine.CountHeld` and `HeldCounts` both scanned `Player.m_inventory` themselves. AB keeps the
backpack in a **separate `Inventory`** and contributes it only through a postfix on
`Inventory.CountItems` — so our scan saw zero. The requirement gate therefore answered "the player holds
none", refused the craft, and the readout overwrote AB's larger number with our smaller one. Exactly the
defect `backpack-requirement.md` predicted, and the open question it left ("our held count bypasses
`Inventory.CountItems`") is now **verified rather than reasoned**: see the AB source evidence below.

## Evidence gathered (all primary)

1. **`Inventory.CountItems(String, Int32, Boolean)` IL** (`assembly_valheim.dll`, via `tools/cecil.ps1`):
   iterates `m_inventory`, matches `item.m_shared.m_name == name`; the `m_quality` compare only runs when
   the quality argument is **non-negative**, and the `m_worldLevel` compare only when the third argument
   is **true**; sums `m_stack`. So `CountItems(sharedName, -1, false)` is *identical* to the pure rule our
   `DrawerTake.HeldAmount` implemented — same-name, quality ignored, world level ignored. Declared
   `public` in the unpublicized assembly, so R29 is satisfied and no reflection is needed.
2. **AB 2.3.4 source** (fetched from `raw.githubusercontent.com/Vapok/AdventureBackpacks/main/AdventureBackpacks/**`;
   note the repo root has an `AdventureBackpacks/` subfolder):
   - `Patches/Inventory.cs:441` — `Inventory.CountItems(String,Int32,Boolean)` **postfix** adds the
     backpack, gated on `CraftingContext.IsActive` **and** `__instance == Player.m_localPlayer.GetInventory()`.
     It also subtracts equipped copies and honours `LeaveOneItemInBackpack`.
   - `Features/CraftingContext.cs` — `IsActive => _activeDepth > 0 && !IsSuppressed`; `Enter`/`Exit` are a
     counted scope and `Suppress`/`Unsuppress(modIdentifier)` exist **for other mods**.
   - `Enter()` call sites: prefixes on `Player.HaveRequirementItems`, `Player.HaveRequirements(Piece,mode)`,
     `Player.HaveRequirements(Recipe,bool,int,int)`, `Player.UpdatePlacement`, `InventoryGui.DoCrafting`,
     `InventoryGui.SetupRequirement` (plus HUD/tabs), each with a matching finalizer `Exit()`. **Prefixes
     run before postfixes**, so all four of our read sites are inside AB's scope: `HaveRequirementItems`
     postfix, `HaveRequirements(Piece,mode)` postfix, `SetupRequirement` postfix, and
     `ConsumeResources` (reached from `DoCrafting`/`UpdatePlacement`, both wrapped).
   - `API/ABAPI.cs` header: do **not** reflect into AB internals; for soft integrations use
     `Docs/ABAPI_Client.cs` (zero-dependency client) or the repackable `AdventureBackpacksAPI.dll`.
     `ABAPI` exposes `TryGetEquippedBackpackInventory`, `IsCraftingContextActive`,
     `IsCraftFromBackpackEnabled`, `IsLeaveOneItemInBackpackEnabled`, `SuppressNativeCrafting`, … — recorded
     as the route to take if this mod ever needs more than counting.

## Decision (R32)

**"Held" is the game's own `Inventory.CountItems(name, -1, false)`, in every place it is read.** It is
vanilla's notion *and* AB's notion **by construction**, so R28 (held ≥ consumed) stops being a
hand-rolled property and becomes a tautology, and the count includes the backpack exactly when AB will
also pay with it. No AB assembly reference, no reflection, no vendored client, no double counting: when
AB's scope is *not* open it contributes nothing — and it would not pay from the backpack either, so the
count stays honest in both branches.

Rejected alternatives (with reasons):
- Keeping the pure scan and adding a backpack scan ourselves: duplicates AB's rules (leave-one, equipped
  protection, `CanCraftFromBackpack` gating), i.e. a hand-rolled superset that can silently exceed what
  AB will pay — the free-craft direction.
- `ABAPIClient` + our own arithmetic: more code and a vendored file for a number vanilla already computes;
  the client's value is chiefly for capabilities we do not need yet (see above).
- Forcing AB's scope open around our own count (`CraftingContext.Enter()` by reflection) — unnecessary,
  since the scope is already open at all four sites, and it would be reflecting into a dependency whose
  API doc asks us not to.

## What changed (commit `b6f7255`, 0.1.13)

- `PullEngine.CountHeldBySharedName` replaces `CountHeld`; `PullEngine.SharedNameOf` added; `HeldCounts`
  uses it for the shortfall.
- `PullKeyPatches.SetupRequirementPostfix` — the readout counts `held + storage` from the same notion, so
  it no longer overwrites AB's number with a smaller one.
- `CraftPatches.HasAnyOfEach` — the "has any of this material" clause uses the same notion, so a build
  piece whose ingredient is in the backpack is offered in the build menu.
- **`DrawerTake.HeldAmount` and `Tests/Logic/HeldAmountTests.cs` deleted.** Its superset property is now
  the game's own method (IL verified above), and leaving a second, unused held notion beside the real one
  is the trap that produced the 14b regression. Tests went 260 → **254**, exactly the removed fixture.
- Station feeding deliberately untouched: a fireplace/smelter/cooking station takes its input from
  `player.GetInventory()` alone, so a backpack item is genuinely not available to it and must not count
  as held.
- Artifact verification on the built DLL: `HeldCounts`, `HasAnyOfEach` and `SetupRequirementPostfix` call
  `CountHeldBySharedName`; its IL is `CountItems(name, -1, false)`; `CountHeld`/`HeldAmount` report
  `(none)`. Build: `BUILD OK - CraftFromDrawersAndChests 0.1.13 (Release)`.

## Residual risks / still open

- **The IL contract is a game-version dependency.** If a future Valheim changes `CountItems`' parameter
  meaning, our superset could break silently, and the fixture that used to pin it is gone. The contract is
  now documented at the call site (`CountHeldBySharedName`) with the exact IL evidence, so a game update is
  caught by reading it — but there is no automated guard.
- **Second player's backpack.** A backpack is not an `IStorageSource` and nothing pulls from it; the
  two-player check in `author-verify.md` still has to be run by the owner.
- **`StationPatches.CountInventory`** counts the player's inventory by `m_dropPrefab` for station feeding.
  Unchanged and correct for its own reason (the station reads `GetInventory()`), but it is now the one
  remaining place that scans an inventory's items directly rather than going through the game's count.
- The owner's 0.1.11 log shows the **0.1.11 station check firing on the happy path** (`the station did not
  take a fed 'Wood' …`) — that is the defect 0.1.12 fixed; 0.1.12's fix itself is still unverified in game.

## Follow-up reconnaissance (2026-10-13): the Item Drawers side of the lantern report

The owner's hypothesis is that the Dvergr-lantern case is an Item Drawers bug. Checked `kg_ItemDrawers.dll`
1.4.0 (installed) with Cecil, in child processes from `%TEMP%`:

- **`Prefab` has exactly one writer**: `DrawerComponent.set_CurrentPrefab`, called from only two places —
  `RPC_WithdrawItem_Request` (to clear the drawer with `""` when it empties) and `RPC_AddItem`, which
  stores **the prefab string passed in by the sender** (`ldarg.2`). So the stored string is whatever the
  client that stores the item computes, and *both halves of a name mismatch are observable*: the drawer's
  stored string is readable from the ZDO, and the requirement's is readable from the recipe.
- Protocol names confirmed present as literals: `ForceRemove`, `AddItem_Request`, `WithdrawItem_Request`,
  `RoutedRPC` — our `DrawerApi` constants match this build.
- **`Left Alt` is overloaded in this install.** Item Drawers' own hover text offers
  `Left Alt + $KEY_Use` to "withdraw single item" from a hovered drawer, while our `Prevent mod key`
  defaults to `left alt` (and the owner's per-storage `Toggle storage key` is `s`, which is also Valheim's
  walk-backwards key). Neither is a defect in itself, but three different meanings for one key, plus a
  toggle bound to a movement key, is a usability trap worth raising with the owner.
- My first scan reported zero hits for `AddItem_Request`, which was **my** argument-passing bug (`-Keys a,b`
  to a `-File` script arrives as one string), not evidence about the DLL. Recorded because "no hits" from a
  probe is not evidence until the probe itself is trusted.

## Validated in game by the owner (0.1.13, 2026-10-13)

- **Backpack materials now count.** A cauldron recipe (Turnip stew) wanting 1 Boar Meat reads `24/1`
  instead of `0/1` with 25 carried, and a forge recipe needing 7 Chain reads `52/7` with 53 carried —
  both were `0/…` before. Crafting works from the backpack.
- **0.1.12's station fix is validated.** The `left shift+E` hover hint is present on the smelter's scrap
  and coal intakes, and single-item adds no longer print the game's own "you don't have any …" message.
  (0.1.11 fed the item and *then* let vanilla print that message; 0.1.11's log shows the confirming check
  firing on the happy path, which is what made it findable.)

### The reported "off-by-one" is AdventureBackpacks' own rule, not a defect here

Both numbers are exactly one below the amount carried, which is the signature of AB's
**`Leave One Item In Backpack = true`** in the owner's install — its own config text, verbatim:

> When enabled, at least one item of each resource type will remain in the backpack and will not be
> consumed or counted during crafting and building.

AB's `Inventory.CountItems` postfix implements precisely that (`bpCount = Max(0, bpCount - 1)`), and
`DeductBackpackCraftingItem` limits consumption the same way, so 25 carried really does mean 24 usable and
AB will genuinely refuse to consume the last one. Because 0.1.13's held count **is** that method, the panel
reports the number the game and AB will act on — which is the point of the change.

Our own `Leave one` setting is unrelated, and the owner's own test already showed it (the number is the
same with it on and off): ours limits what a *storage* pull may take, never the backpack. **No code change
is warranted**; AB's setting is the switch, at the cost of AB emptying the backpack of a resource entirely.

**Documentation owed:** the README should state that the available amount includes the equipped backpack
and obeys AB's own rules, including that setting. Deferred to the next version bump so the shipped 0.1.13
zip is not left stale against the repo.

**Still open:** the Dvergr-lantern readout test (owner to run).

## Dvergr-lantern report — RESOLVED 2026-10-13: the storage was switched off

Owner's words: *"This was user-error on my part. Pull was toggled to off on this one specific item
drawer. This was the correct behavior for the mod."*

So the second of the two candidates the log pointed at was right, and the mod behaved correctly. The
report was **not** about non-stackable items, and no code change is warranted for it. Two findings from
the investigation are still worth acting on:

1. **The readout diagnostic cannot see switched-off storage.** `Eligibility.IsEligible` rejects a
   toggled-off source as its *first* clause, so the source is dropped before `LogReadout` ever sees it —
   which is why the log showed zero `OFF` markers and could not answer the question, even though the
   answer was a flag on the one drawer in question. The instrument should name nearby storage that was
   excluded and why (off / out of range), so this class of report is answered by the log next time. This
   is the reason the diagnosis took the owner's side of the loop instead of ours.
2. **The owner's `Toggle storage key` is `s`, which is also walk-backwards.** This incident is most
   likely an accidental flip: standing near a drawer with it under the crosshair and pressing the
   movement key silently switches that drawer's pull off, and nothing reports it. The shipped default is
   `left shift + e`; a movement key should never be bound to a per-storage toggle. Worth a README warning
   as well as a recommendation to the owner — and `Left Alt` is separately overloaded (Item Drawers binds
   it to "withdraw single item").

Documentation owed to the README (all verified): the readout's available amount includes the equipped
backpack under AdventureBackpacks' own rules — including `Leave One Item In Backpack`, which is why 25
carried can read as 24 — and a troubleshooting line pointing at `(pull: off)` and `Maximum range` when a
storage's contents are not counted.
