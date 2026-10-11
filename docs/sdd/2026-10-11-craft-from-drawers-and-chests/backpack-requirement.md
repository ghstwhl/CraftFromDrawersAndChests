# Requirement: AdventureBackpacks coexistence (controller findings, verified 2026-10-11)

User's requirement, verbatim:

> Crafting should be able to pull from a player's backpack (https://github.com/Vapok/AdventureBackpacks)
> for crafting with the hammer, but not for the ctrl-click action. We would not want to pull items into
> a player's inventory from their own backpack, because the items in the backpack are inherently part
> of the player's inventory.

## What verification found — the feature already exists, in AdventureBackpacks

Inspected `AdventureBackpacks.dll` **2.3.1** (installed at
`…\profiles\Modheim\BepInEx\plugins\Vapok-AdventureBackpacks\`), decompiled and read directly.
`[BepInPlugin("vapok.mods.adventurebackpacks", "Adventure Backpacks", "2.3.1")]`.

**The backpack is a real `Inventory`.** `BackpackComponent : CustomItemData` holds
`private Inventory _backpackInventory`, exposed as `GetInventory()`. Reachable two ways:
- the equipped item — `inventory.GetAllItems().FirstOrDefault(i => i.IsBackpack() && i.Data().Get<BackpackComponent>()?.GetInventory() == …)`
- `Player.m_localPlayer.GetBackpackContainerProxy(createIfMissing: false)` — a `Container` proxy whose
  `m_inventory` is swapped to the backpack's.

**AB already implements crafting-from-backpack, deducting in place.** `CraftFromBackpack` exposes
`CanCraftFromBackpack`, `DeductBackpackCraftingItem`, `ProtectEquippedItems`, `CanCraftOutputToBackpack`.
It patches:
- `Inventory.CountItems(String, Int32, Boolean)` — **postfix** adding backpack contents
- `Inventory.RemoveItem(String, Int32, Int32, Boolean)` — **prefix** deducting from the backpack
- `Inventory.RemoveItem(ItemData...)` ×3, `Inventory.CanAddItem`, `Inventory.AddItem`
- and, critically for the hammer, **`Player.UpdatePlacement` (line 3567), both `Player.HaveRequirements`
  overloads (3461 Piece / 3495 Recipe), `Player.HaveRequirementItems` (3531), `Hud.SetupPieceInfo` (1286)**

All of it is gated on `CraftingContext.IsActive` — a depth-counted scope `Enter()`/`Exit()`ed around those
calls (595, 1299, 2364, 2708, 2794, 2820, 2848, 3480, 3490), with `Suppress`/`Unsuppress` for other mods.
So the backpack's contents count during availability checks and are deducted from during consumption —
without any item entering `Player.m_inventory`.

## The likely defect: our held count bypasses `Inventory.CountItems`

AB's `CountItems` postfix early-returns unless `__instance == Player.m_localPlayer.GetInventory()`, so it
contributes to that one method and nothing else. Our held count does not call it:

- `PullEngine.HeldCounts` / `DrawerTake.HeldAmount` / `PullEngine.CountHeld` / `InventoryStacks` all scan
  `inventory.GetAllItems()` and match on `m_shared.m_name` / `m_dropPrefab.name` directly.

So our gate cannot see backpack contents, concludes the player lacks them, and then either plans a storage
pull for materials the player already holds or refuses the pull — while vanilla + AB would have paid from
the backpack. **This is reasoning from two verified facts, not itself verified: the task must confirm it
(both mods patch `HaveRequirementItems`/`HaveRequirements`/`UpdatePlacement`, so patch *ordering* and
`CraftingContext` depth also have to be checked) before changing anything.**

## Design constraints (from the user's requirement)

1. **The backpack must NOT become an `IStorageSource`.** No `StorageRegistry` entry, no pull source: the
   ctrl-click action must not move items out of it. That is the explicit requirement.
2. **The backpack MUST count as "held"** so the hammer/build gate and our shortfall see its contents, and
   our consume prefix does not plan a shortfall for materials the player already has.
3. **Nothing may be moved into `Player.m_inventory` from the backpack.** AB deducts in place; we must not
   duplicate items by pulling them out *and* leaving AB's deduction to also take them.
4. **Payment must still be real.** If our gate counts backpack contents but vanilla + AB cannot consume
   them on a given path, we have re-created the free-craft hazard that Task 14b spent two rounds closing.
   The R28 invariant — our held count is a **superset** of what will actually be consumed — must be
   re-established with the backpack included.

## Additional requirement (user, same day): never another player's backpack

> The mod should not be able to pull from other player's backpacks, only the backpack of the player
> pulling the items during a craft.

### Verified: today's exclusion is incidental, not designed

- Our chest collection requires `IsInsidePiece` (`Logic/Eligibility.cs`, the chest clause:
  `if (!c.IsInsidePiece || !c.PassesGuardStone) return false;`), plus `HasAccess`, `HasInventory`, range
  and the chest-type rules. AB's backpack proxy is a runtime `Container` parented to the player rather
  than a placed piece, so it is **not** eligible today.
- **But nothing names backpacks.** `ContainerTrackingPatches.ShouldTrack` is CraftFromContainers'
  verbatim predicate and accepts any container whose GameObject name starts with `piece_` **or
  `Container`**, or whose ZDO carries a non-zero `creator`. AB patches `Container.Awake`, so proxies do
  awake and do get evaluated. A name-prefix acceptance test is one naming convention away from
  admitting a proxy as a pull source — and it would admit *any* player's proxy, not just your own.
- AB reaches the backpack via `Player.GetBackpackContainerProxy(createIfMissing:)` and swaps
  `m_inventory` on that proxy (`BackpackComponent.SyncContainerInventory`). Whether a proxy for
  **another** player's backpack exists locally on this client still needs checking.

### What the task must do

1. **Explicitly exclude every player's backpack proxy as a pull source**, rather than relying on
   `IsInsidePiece` incidentally. Identify it robustly, fail closed when AdventureBackpacks is absent, and
   pin it with a test.
2. **Never enumerate or reach into another player's backpack.** No `Player.m_allPlayers` sweep that could
   surface someone else's inventory.
3. **The pulling player's own backpack may count as "held" during a craft** (the earlier requirement) —
   counting is not pulling, and AB deducts in place during its `CraftingContext`. It must still never
   become an `IStorageSource`.
4. Add an `author-verify.md` two-player check: with a second player wearing a backpack in range, neither
   the ctrl-click pull nor a station feed may take anything from it.

## Open questions the task must settle, with evidence

- Does our `HaveRequirementItems`/`HaveRequirements` postfix run **inside or outside** AB's
  `CraftingContext` scope, given both mods patch those methods? (Patch ordering decides whether AB's
  `CountItems` contribution is even visible to a `CountItems` call made from our postfix.)
- Does our `ConsumeResources` prefix returning `false` (the decline path) **skip AB's in-place backpack
  deduction**, and if so, can that produce an unpaid craft?
- Is the cleanest fix to base our held count on `Inventory.CountItems(name, -1, …)` — which would make it
  *vanilla's + AB's* notion by construction, rather than a hand-rolled superset — or to read the backpack
  inventory explicitly? Decide on evidence, and keep the R28 property (held ≥ consumed) provable.
- Should AdventureBackpacks become a **soft** `[BepInDependency]`? It must not become a hard one: the mod
  has to work without it.
