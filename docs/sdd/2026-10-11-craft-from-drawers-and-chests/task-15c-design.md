# Task 15c — fill-to-max: settled design (controller, 2026-10-12)

Written **before** implementing, because reading the oracle and the code changed the design twice.
`task-15-scope.md`'s plan for this task is wrong; the correction is below.

## What the task is

The owner ruled for full CraftFromContainers parity on the fill key: the fetched fuel/ore/cooking
items must end up **in the station**, not pre-loaded into the player's inventory. Today the fill
request is right (it asks for the station's remaining capacity — Task 15b's `PullPlan.fillUpTo` and
`DestinationCapacity.AcceptableAmount`) but the items land in the inventory, so the station reaches
its maximum only over the following interactions. Task 15b's report recorded this as D1.

## Three findings, each from a primary source

**1. CFC feeds the station through the station's own replicated add path, and never writes an
inventory.** `Smelter_OnAddOre_Patch` (`.references/Mods/CraftFromContainers/BepInExPlugin.cs:459`):

```csharp
int amount = Mathf.Min(m_maxOre - GetQueueSize(), c.GetInventory().CountItems(name));
if (amount > 0) amount -= leaveOne.Value ? 1 : 0;          // leave-one: containers only
c.GetInventory().RemoveItem(name, amount);                 // take from the SOURCE
for (int i = 0; i < amount; i++)
    ___m_nview.InvokeRPC("RPC_AddOre", new object[] { prefab, cheated });
```

`Smelter_OnAddFuel_Patch` (:575) and `Fireplace_Interact_Patch` (:272) are the same shape with
`RPC_AddFuel`. So CFC's "the station's inventory as the destination" is not what happens at all —
there is no station inventory to write for a Fireplace (a fuel float) or a Smelter (a queue plus a
fuel float). **The scope doc's four `Inventory`-taking engine variants are therefore not the work.**

**2. CFC's take bypasses MultiUserChest.** It removes items by calling `Inventory.RemoveItem` on the
container's own inventory. **This mod forbids that** — Ruling 5, the rule that keeps a chest from
desyncing against the player MultiUserChest has managing it — and `MultiUserChestBridge` is the only
sanctioned removal path.

**3. That path needs a destination `Inventory`.** `ContainerHandler.RemoveItemFromChest(chest, item,
destination, to, sender, amount)` delivers into `destination` and reports the delta (see
`MultiUserChestBridge.RemoveFromChest`). A removal with no destination is not expressible, so
"chest → fireplace fuel" in one step cannot exist in this design.

## The design

Take into the player's inventory through the sanctioned path, then feed the station from there:

```
while the station is not full and the last pass made progress:
    request <- min(station's remaining capacity, what the sources hold, what the inventory can accept)
    pull that much into the player's inventory        (existing PullEngine, unchanged)
    feed the station from the inventory               (NEW - the station's own add path)
```

Batching matters: it is what keeps a large fill from needing room for the whole amount at once, so
"fill a smelter with 100 ore" succeeds with an inventory that has a few free slots. The existing
shortfall/acceptance machinery already computes the third term, so no new capacity rule is needed.

Feeding goes through the station's own entry points, which are **private** and must therefore be
reached by reflection, exactly as `StationPatches` already reaches `Smelter.GetFuel`,
`Smelter.GetQueueSize` and `CookingStation.GetFuel`:

| Member | Visibility | Use |
|---|---|---|
| `Smelter.OnAddOre(Switch, Humanoid, ItemData)` | private | feed ore |
| `Smelter.OnAddFuel(Switch, Humanoid, ItemData)` | private | feed fuel |
| `CookingStation.OnAddFuelSwitch(Switch, Humanoid, ItemData)` | private | feed fuel |
| `CookingStation.GetFreeSlot()` | private | remaining capacity |
| `Fireplace.Interact(Humanoid, Boolean, Boolean)` | public | feed fuel (interface entry point) |
| `CookingStation.Interact(Humanoid, Boolean, Boolean)` | public | interface entry point |

Audited with the sanctioned probe against the shipping (unpublicized) `assembly_valheim.dll`. No
station member may be called directly unless it is public here — the publicized reference binds
private members at compile time and only fails at runtime.

## The one deliberate parity gap

CFC's fill works even with a completely full player inventory, because the items never touch it. This
design needs room for at least one item at a time, so a fill with a **completely** full inventory
takes nothing — the same limit this mod's single-item station feeding already has. To be recorded in
the changelog and in `author-verify.md` rather than discovered in play.

## Rejected

- **Direct source → station take.** Impossible: finding 3 above.
- **Writing the station's or the chest's inventory.** Forbidden by Ruling 5.
- **The scope doc's `Inventory`-taking engine variants.** Built on a model of the game that does not
  exist — finding 1.
- **Feeding via the station `RPC`s as CFC does.** Possible, but it pairs with CFC's forbidden take. If
  it proves simpler than the reflected add methods for a given station, it may be used *for the feed
  half only* — the take half stays on `MultiUserChestBridge`.
