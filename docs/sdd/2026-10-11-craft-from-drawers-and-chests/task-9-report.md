# Task 9 Report — Drawer reflection API and drawer source

Date: 2026-10-11
Branch: `feature/craft-from-drawers-and-chests`
Commit: `7d8b14f` — feat: item drawers reflection api and storage source (8 files, +811)

## What I implemented

Two rulings overrode the brief; both are honoured.

**Ruling A — resolution moved into the pure layer.**
- `CraftFromDrawersAndChests/Logic/DrawerContract.cs` (pure; `System` + `System.Reflection` only):
  - `enum DrawerContractKind { None, ItemDrawersApi, ClientSideV2 }`
  - `readonly struct DrawerContract` carrying `Kind`, the cached `AllDrawersQuery` `MethodInfo`,
    and (for `ItemDrawersApi`) the cached `RangeQuery` `MethodInfo`, plus `IsAvailable`.
  - `static DrawerContract Resolve(Func<string, Type> typeLookup)` — prefers `API.ItemDrawers_API`
    (requires `AllDrawersInRange`), falls back to `API.ClientSideV2.AllDrawers()`, else `None`.
    A null lookup, a throwing lookup, a missing type and a partial rename all yield `None` — never
    an exception.
- `CraftFromDrawersAndChests/Storage/DrawerApi.cs` is the thin game-facing wrapper: it supplies
  `Type.GetType` as the lookup and turns the queried objects into `DrawerHandle`s.

**Ruling B — keying.** `IStorageSource.Count(string itemName)` documents `itemName` as the **prefab
name** (`"Wood"`), not `m_shared.m_name` (`"$item_wood"`), and `DrawerSource.Count` compares it to
the ZDO `"Prefab"` directly.

Files created:
- `Logic/DrawerContract.cs` — pure contract resolution.
- `Logic/DrawerStock.cs` — pure prefab-keyed "how many of this item does this drawer contribute"
  decision. Added because Review Focus 4 (a drawer reporting a prefab with a zero amount) is pinned
  to a Task 9 test, and a live ZDO cannot be built in the `Tests` project; this makes that decision
  provable as a unit test.
- `Storage/IStorageSource.cs` — the one storage abstraction.
- `Storage/DrawerApi.cs` — reflection front door; also defines `DrawerHandle`.
- `Storage/DrawerSource.cs` — adapts a drawer `ZNetView` to `IStorageSource`.
- `Tests/Logic/DrawerContractTests.cs` (9 tests), `Tests/Logic/DrawerStockTests.cs` (7 tests).

csproj: five `<Compile Include>` items added (the product project is legacy-style with explicit
items, so unregistered files would not build).

### Decisions taken (with evidence)

I decompiled the installed `kg_ItemDrawers.dll` 1.4.0 with Mono.Cecil (child process, no file lock)
rather than guess the API. Primary findings:

- `API.ItemDrawers_API` (public static): static **property** `AllDrawers` → `List<Drawer>`, and
  static `AllDrawersInRange(Vector3, float)` → `List<Drawer>`.
- Nested `Drawer`: public fields `Prefab`/`Amount`/`Quality`; its `ZNetView` lives in a **private**
  instance field, so `DrawerApi` extracts it by field *type* (`typeof(ZNetView)`), not by name.
- `Drawer.Remove(amount)` compiles to exactly `znv.ClaimOwnership(); znv.InvokeRPC("ForceRemove", amount);`
  — confirming the brief's prescribed path and the RPC name.
- `API.ClientSideV2.AllDrawers()` returns `List<ZNetView>`.
- `DrawerComponent` reads its ZDO as `GetString("Prefab", "")`, `GetInt("Amount", 0)`,
  `GetInt("Quality", 1)` — so `DrawerSource` uses the same keys/defaults.
- `GetStableHashCode` is a public `[Extension]` in the global-namespace `StringExtensionMethods`
  (`assembly_utils`), so `ConfigModel.ZdoKey.GetStableHashCode()` is the correct hash for the
  `ToggledOff` ZDO read — reusing the single existing key constant (no second key string invented).

Behaviour required by the brief/constraints and implemented:
- `DrawerSource.Take` = `ClaimOwnership()` + `InvokeRPC("ForceRemove", take)`; it never calls
  `WithdrawItem_Request` and never writes the ZDO `"Amount"`.
- A drawer reporting a prefab with `Amount == 0` contributes nothing and throws nothing.
- No contract → `DrawerApi.IsAvailable` false, `AllInRange` returns empty, nothing throws.
- No Harmony patches, no chests, no MultiUserChest work (Tasks 10/11).

## TDD evidence

### RED

Command:
`dotnet test C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\Tests\CraftFromDrawersAndChests.Tests.csproj --filter 'DrawerContractTests|DrawerStockTests'`

Output (tail):
```
...Tests\Logic\DrawerContractTests.cs(65,39): error CS0103: The name 'DrawerContract' does not exist in the current context
...Tests\Logic\DrawerContractTests.cs(67,51): error CS0103: The name 'DrawerContractKind' does not exist in the current context
...Tests\Logic\DrawerContractTests.cs(76,13): error CS0246: The type or namespace name 'DrawerContract' could not be found ...
... (repeats for every DrawerContract / DrawerContractKind use in DrawerContractTests.cs)
```

Why expected: the tests were written first and reference `Logic.DrawerContract`, `DrawerContractKind`
and `Logic.DrawerStock`, none of which existed yet. The `Tests` project compiles `Logic/**` directly,
so the pure-layer types are missing at compile time and the run cannot start.

### GREEN

Command: `dotnet test C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\Tests\CraftFromDrawersAndChests.Tests.csproj`

Output (tail):
```
CraftFromDrawersAndChests.Tests -> ...\Tests\bin\Debug\net462\CraftFromDrawersAndChests.Tests.dll
Test run for ...\CraftFromDrawersAndChests.Tests.dll (.NETFramework,Version=v4.6.2)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    57, Skipped:     0, Total:    57, Duration: 177 ms - CraftFromDrawersAndChests.Tests.dll (net462)
```

57 = 41 (Tasks 1–5) + 16 new (9 `DrawerContractTests` + 7 `DrawerStockTests`). Every new test asserts
a concrete value; none asserts nothing.

## Build

Command: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`

Final lines:
```
  CraftFromDrawersAndChests -> ...\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll
    Built ...\CraftFromDrawersAndChests.dll (30,720 bytes)

BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```

`MSB3274` (MultiUserChest is net472 vs the project's net462) prints as expected and is not mine to fix.

Two first-build errors were hit and fixed (both genuine mistakes, not workarounds):
1. `DrawerSource.cs`: `Vector3`/`GameObject` undefined — added `using UnityEngine;`.
2. `DrawerApi.cs`: `Logger` ambiguous between `BepInEx.Logging.Logger` and `UnityEngine.Logger` —
   fully qualified the BepInEx call.

## Files changed

| File | Change |
|---|---|
| `CraftFromDrawersAndChests/Logic/DrawerContract.cs` | new (pure) |
| `CraftFromDrawersAndChests/Logic/DrawerStock.cs` | new (pure) |
| `CraftFromDrawersAndChests/Storage/IStorageSource.cs` | new |
| `CraftFromDrawersAndChests/Storage/DrawerApi.cs` | new |
| `CraftFromDrawersAndChests/Storage/DrawerSource.cs` | new |
| `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` | +5 `<Compile>` items |
| `Tests/Logic/DrawerContractTests.cs` | new (9 tests) |
| `Tests/Logic/DrawerStockTests.cs` | new (7 tests) |

## Self-review findings

- **`Logic/**` purity:** grep for `UnityEngine|Valheim|BepInEx|Jotunn|MonoBehaviour|ZNetView|Inventory`
  over `Logic/**` returns only **doc-comment** matches; no code reference. `DrawerContract.cs` and
  `DrawerStock.cs` use only `System` and `System.Reflection`. The `Tests` project (which links
  `Logic/**` and references no game assembly) compiles and runs, which is the operative proof.
- **Ruling A shape:** enum + result struct with `Kind`/`AllDrawersQuery`/`RangeQuery` + `static Resolve(Func<string, Type>)`;
  tests live in `Tests/Logic/` and use a stub resolver, so no game assembly is needed.
- **Ruling B:** the prefab-name keying is documented on the `Count` parameter and compared to the ZDO
  `"Prefab"` directly.
- **No test asserts nothing:** all 16 new tests assert concrete `Is.EqualTo`/`Is.False`/`Is.Null` values.
- **Single source of truth for literals:** ZDO keys and the `ForceRemove` RPC name are `public const`
  on `DrawerApi`; `DrawerSource` reuses them. The toggle key is not re-declared — it comes from
  `ConfigModel.ZdoKey`.
- **Names:** `DrawerContract`/`DrawerContractKind`/`DrawerHandle`/`DrawerApi`/`DrawerSource`/`IStorageSource`
  match the brief and the plan's Task 10/11/12 consumers (`DrawerApi.AllInRange`, `DrawerApi.IsDrawer`).
- **No stray artefacts:** all Cecil inspection scripts were written to `%TEMP%`, outside the repo;
  `git status` was clean after the commit.

## Concerns

1. **The `Drawer`'s `ZNetView` is a private field.** `DrawerApi` locates it by field type
   (`typeof(ZNetView)`) rather than by name, so a rename is survivable, but if KJ restructures `Drawer`
   to drop that field the drawer path silently yields no handles (it does not throw). Verified against
   1.4.0 only.
2. **`Logic/DrawerStock.cs` is an addition beyond the ruling's literal wording.** Justification:
   Review Focus 4 says its case is pinned by "Task 9's test", and a ZDO cannot be built in the
   `Tests` project; extracting the pure decision is the only way to pin it with a real test. The
   ruling's "Cover at least" list is still fully covered by `DrawerContractTests`.
3. **`DrawerSource.Take` does not move items into `destination`.** `ForceRemove` only removes; the
   design has the mod add the items to the player afterwards. The parameter is therefore unused for
   drawers and is documented as such. If Task 13 expects `Take` itself to add, that is a Task 13
   integration decision, not a change here.
4. **`DrawerApi.IsDrawer` matches `GameObject.GetComponent("DrawerComponent")` by name.** It needs no
   compile-time type and is safe on a destroyed object, but it is string-based. It is only used by
   Task 12's hover path and is not covered by this task's tests.

## AUTHOR-VERIFY

Cannot be checked without the game (no Valheim runtime, no installed Item Drawers in the test process):

1. **Deploy with Item Drawers installed** (server and client) and confirm `DrawerApi.IsAvailable` is
   `true` in a debug log at first use. On a machine *without* Item Drawers, confirm it is `false`, the
   mod still loads, and no exception is logged — the soft-dependency fallback (Review Focus 5).
2. **Drawer discovery:** place drawers inside and outside the configured range and log each
   `DrawerHandle` (prefab, amount, distance). Expect the in-range drawers only; confirm the
   `ItemDrawers_API` path is taken (native range query) by logging `DrawerContract.Kind`.
3. **Empty drawer (Review Focus 4):** pull an item so a drawer's ZDO reports a prefab with
   `Amount == 0`; confirm it contributes nothing and no exception is thrown.
4. **Removal:** with a drawer holding `Wood`, call `DrawerSource.Take("Wood", 2, playerInventory)`.
   Confirm the drawer loses 2 and the items are added to the player by the caller — and that no item
   is dropped on the ground (i.e. `ForceRemove` was used, not `WithdrawItem_Request`). Then confirm
   taking from a drawer whose prefab does not match removes nothing.

---

# Task 9 — Fix round 1 (Rulings 20 and 21)

Date: 2026-10-11
Commit: `d5dd529` — fix: drawer pulls deliver into the destination and bind by parameter type (7 files, +232 / −19)

## What I changed

### Finding 1 (Critical) — `DrawerSource.Take` now delivers into `destination`

`Take` previously only removed (`ForceRemove`) and discarded `destination`, so every drawer pull
destroyed the items — the design's top anti-goal — and the XML doc at `:92-95` wrongly claimed the
caller adds them afterwards. It now removes **and delivers**, the same way the chest path will
through `MultiUserChestBridge.RemoveFromChest(chest, item, destination, ...)`:

1. `take = DrawerTake.AmountToRemove(amount, Count(itemName), destination != null)` — clamped to the
   drawer's stock, and 0 when there is no destination.
2. Resolve the prefab: `ObjectDB.instance.GetItemPrefab(itemName)`; if it is missing, log a warning
   and take nothing.
3. Capacity check: `destination.CanAddItem(prefab, take)`; if it will not fit, log a warning and take
   nothing.
4. `ClaimOwnership()` + `InvokeRPC("ForceRemove", take)` (the only supported removal path — never
   `WithdrawItem_Request`), then `destination.AddItem(prefab, take)`.
5. Return `take` — the number actually delivered.

Removal is ordered **after** both delivery preconditions, so the number removed never exceeds the
number delivered, and nothing is silently dropped: a null destination, an unresolvable prefab or a
full destination all remove nothing and return 0. `destination` is now documented as the delivery
target on `DrawerSource.Take` and `IStorageSource.Take` (the false "unused" wording is gone).

API shapes verified against `.references\Valheim\1.0.16\assembly_valheim_publicized.dll` with
Mono.Cecil (child process): `Inventory.AddItem(GameObject, int)` clones the prefab's `ItemData`, sets
`m_stack = Min(amount, maxStackSize)` and adds stack-aware; `Inventory.CanAddItem(GameObject, int)`
delegates to `CanAddItem(ItemData, int)`; `ObjectDB.instance.GetItemPrefab(string)`; `ObjectDB`'s
singleton is the static field `m_instance`.

New pure decision `Logic/DrawerTake.cs` (`AmountToRemove`) + its `<Compile>` entry (Ruling 9). It is
the only part of the delivery that can be pinned without the game.

### Finding 2 (Minor, bundled — Ruling 21) — `DrawerContract.FindStaticMethod` matches parameter types

It matched by name + parameter count only, so a future same-arity overload could bind the wrong
`MethodInfo` and silently degrade the whole drawer path to "no drawers" at `Invoke` time. It now
matches parameter types by position as well: `AllDrawersInRange` requires `(Vector3, float)`. Types
are compared by simple name (`"Vector3"`, `"Single"`) because `Logic/` may not reference UnityEngine.

## Tests that cover the amended code

- `Tests/Logic/DrawerTakeTests.cs` (new, 4 tests): clamping to stock, and the "no destination removes
  nothing" rule that stops a pull destroying items.
- `Tests/Logic/DrawerContractTests.cs`: the range-query stub now takes a `Vector3` (a local stub
  struct — the test project references no UnityEngine), plus a same-arity / `object`-position decoy
  stub and two tests proving it is rejected.

**Coverage limit (stated explicitly):** the materialisation step (`ObjectDB.GetItemPrefab` →
`Inventory.CanAddItem` / `Inventory.AddItem`) is game-facing and is **not** covered by these tests —
it needs the Valheim runtime and a live `Inventory`. The pure decision that feeds it
(`DrawerTake.AmountToRemove`) is covered.

## TDD evidence

RED (Fix 2) — `dotnet test C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\Tests\CraftFromDrawersAndChests.Tests.csproj --filter "FullyQualifiedName~DrawerContractTests"`
```
  Failed ApiWithMismatchedRangeParameterTypes_FallsBackToClientSideV2 [91 ms]
  Error Message:  Expected: ClientSideV2  But was:  ItemDrawersApi
  Failed ApiWithMismatchedRangeParameterTypes_FallsBackToNoneWhenNoFallback [1 ms]
  Error Message:  Expected: None  But was:  ItemDrawersApi
Failed!  - Failed:     2, Passed:     9, Skipped:     0, Total:    11, Duration: 177 ms
```

RED (Fix 1) — `dotnet test ...\Tests\CraftFromDrawersAndChests.Tests.csproj --filter "FullyQualifiedName~DrawerTakeTests"`
```
Tests\Logic\DrawerTakeTests.cs(22,25): error CS0103: The name 'DrawerTake' does not exist in the current context
... (8 errors, one per call site)
```

GREEN — `dotnet test C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\Tests\CraftFromDrawersAndChests.Tests.csproj`
```
Passed!  - Failed:     0, Passed:    63, Skipped:     0, Total:    63, Duration: 246 ms - CraftFromDrawersAndChests.Tests.dll (net462)
```
63 = 57 previous + 6 new (4 `DrawerTakeTests` + 2 `DrawerContractTests`).

## Build

`powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
```
  CraftFromDrawersAndChests -> ...\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll
    Built ...\CraftFromDrawersAndChests.dll (32,256 bytes)
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```
`MSB3274` (MultiUserChest is `net472` against this project's `net462`) is the only warning, as expected.

## Files changed

| File | Change |
|---|---|
| `CraftFromDrawersAndChests/Logic/DrawerTake.cs` | new (pure delivery decision) |
| `CraftFromDrawersAndChests/Logic/DrawerContract.cs` | parameter-type matching |
| `CraftFromDrawersAndChests/Storage/DrawerSource.cs` | `Take` delivers; docs corrected |
| `CraftFromDrawersAndChests/Storage/IStorageSource.cs` | `destination` documented as the delivery target |
| `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` | +1 `<Compile>` item |
| `Tests/Logic/DrawerTakeTests.cs` | new (4 tests) |
| `Tests/Logic/DrawerContractTests.cs` | +2 tests, stub updated |

## Still for the game

The `AUTHOR-VERIFY` list above still applies; item 4 is now the direct check of this fix — confirm
`Take("Wood", 2, playerInventory)` removes 2 from the drawer **and** lands 2 in the inventory, with
nothing on the ground.
5. **Toggle read:** with the drawer's ZDO `CFDAC_NoPull` boolean set, confirm `DrawerSource.ToggledOff`
   is `true` (and that flipping it in-game is Task 12's job, not this task's).

---

# Task 9 — Fix round 2 (Critical: silent item loss; also Ruling 22)

Date: 2026-10-11
Commit: `618fd0c` — fix: deliver drawer pulls in stack-sized chunks so none are destroyed (3 files, +167 / −36)

## The finding I corrected

Round 1's `Take` removed `take` with one `ForceRemove` and then called
`destination.AddItem(prefab, take)` **once**, reporting `take` as delivered. One
`Inventory.AddItem(GameObject, int)` places **at most one stack**, so for `take > maxStackSize` the
drawer lost all of `take` while the player received one stack and `Take` reported `take`. Silent
destruction of items — the design's top anti-goal. Round 1's `CanAddItem(prefab, take)` gate was true
(the capacity is measured across all free stacks and empty slots) but `AddItem` still only delivered
one stack, so the gate did not make the delivery all-or-nothing.

### Verify-first (IL, child process, Mono.Cecil)

`assembly_valheim_publicized.dll` (1.0.16):

```
### AddItem(GameObject, Int32)
  ... prefab.GetComponent<ItemDrop>().m_itemData.Clone();
  m_stack = Mathf.Min(amount, m_shared.m_maxStackSize);   // clamps to ONE stack
  ... return this.AddItem(ItemData);                      // adds a single ItemData
### CanAddItem(ItemData, Int32)
  num  = FindFreeStackSpace(name, worldLevel);
  num += (width*height - m_inventory.Count) * m_maxStackSize;
  return num >= stack;                                    // true for stack >> maxStackSize
```

`AddItem(ItemData)` (maxStack > 1) merges one item at a time into matching free stacks and puts the
remainder in an empty slot — so **for `n <= maxStackSize`, `CanAddItem(prefab, n) == true` implies
`AddItem(prefab, n)` places exactly `n`**. That is the guarantee the fix relies on. (maxStack == 1:
capacity reduces to the empty-slot count, so the same implication holds.)

## What I changed

**`Logic/DrawerTake.cs` (pure, unchanged boundaries).** Added `NextChunk(int target, int deliveredSoFar,
int maxStackSize)` — the chunk-size and running-total decision: `min(target − deliveredSoFar,
maxStackSize)`, or 0 when the target is met or no chunk is deliverable. `AmountToRemove` is unchanged.
`Logic/**` still uses only `System` (no `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`).

**`Storage/DrawerSource.cs` (`Take`).** Resolve the prefab and its `m_maxStackSize` (via
`prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize`), then drive the chunk loop:

```
int delivered = 0;
while ((chunk = DrawerTake.NextChunk(want, delivered, maxStack)) > 0)
{
    if (!destination.CanAddItem(prefab, chunk)) break;   // gate THIS chunk's delivery FIRST
    _handle.View.ClaimOwnership();
    _handle.View.InvokeRPC(DrawerApi.ForceRemoveRpcName, chunk);   // remove exactly `chunk`
    if (!destination.AddItem(prefab, chunk)) break;       // deliver exactly `chunk` (unreachable)
    delivered += chunk;
}
return delivered;
```

### The invariant, and how the code enforces it

**`removed == delivered` holds at every step and at the end — the drawer never gives up more than the
player received.** Three things enforce it:

1. Each chunk is `<= maxStackSize`, so it is exactly what one `AddItem` can place — no excess is
   dropped on the floor of the API.
2. `destination.CanAddItem(prefab, chunk)` is evaluated **before** that chunk's `ForceRemove`, and
   `chunk <= maxStackSize` makes the check sufficient (per the IL guarantee above): the delivery is
   known to succeed *before* anything leaves the drawer. No removal precedes its delivery.
3. `delivered` is incremented only after `AddItem` returns true and is what `Take` returns, so the
   report never exceeds what was delivered. A null destination, a missing prefab/item drop or no room
   for even the first chunk removes nothing and returns 0.

The `if (!destination.AddItem(...)) break;` line is now genuinely unreachable (the gate proves it),
but is kept and logged rather than swallowed. Ruling 22's hazard — a removal followed by a reported
failed `AddItem` — is gone: the removal is now gated on, and paired with, the delivery of the same
chunk, and a partial result reports the actual `delivered` count (never 0 after removing).

## Covering tests and the exact command

`Tests/Logic/DrawerTakeTests.cs` gained 5 tests (now 9); the case that failed — requested amount
exceeding a single stack — is covered directly.

Command:
```
dotnet test C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\Tests\CraftFromDrawersAndChests.Tests.csproj
```
Output (tail):
```
Passed!  - Failed:     0, Passed:    68, Skipped:     0, Total:    68, Duration: 153 ms - CraftFromDrawersAndChests.Tests.dll (net462)
```
68 = 63 previous + 5 new. The decisive new tests:
- `NextChunk_RequestExceedingOneStack_YieldsOneStackAtATime` — `NextChunk(70,0,20)==20`,
  `(70,20,20)==20`, `(70,60,20)==10`, `(70,70,20)==0`.
- `NextChunk_DrivingTheDeliveryLoop_ReachesTheWholeRequestInStacks` — the loop yields `[20,20,20,10]`
  and sums to 70, i.e. the full request is delivered in stack-sized pieces.
- `AmountToRemove_ThenChunkedStillDeliversEverythingTaken` — request 70 from a drawer holding 50 with
  20-per-stack: take 50, deliver `50` as `20+20+10` (`removed == delivered`).

RED first: `DrawerTake.NextChunk` did not exist — `error CS0117: 'DrawerTake' does not contain a
definition for 'NextChunk'` (12 errors, one per call site) — before the method was added.

## Build

`powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
```
  CraftFromDrawersAndChests -> ...\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll
    Built ...\CraftFromDrawersAndChests.dll (32,768 bytes)
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```
`MSB3274` (MultiUserChest is `net472` vs this project's `net462`) is the only warning, as expected.

## Files changed

| File | Change |
|---|---|
| `CraftFromDrawersAndChests/Logic/DrawerTake.cs` | +`NextChunk` pure chunk-size/running-total decision |
| `CraftFromDrawersAndChests/Storage/DrawerSource.cs` | `Take` delivers in stack-sized chunks, per-chunk capacity gate |
| `Tests/Logic/DrawerTakeTests.cs` | +5 tests (incl. requested > one stack) |

## Coverage limit (stated explicitly)

The **pure** decisions (how much to take, how to chunk it) are covered by the tests above. The
game-facing materialisation — resolving the prefab's `m_maxStackSize`, `Inventory.CanAddItem` and
`Inventory.AddItem` — needs the Valheim runtime and a live `Inventory`, so it is **not** unit-tested
here; it was verified by reading the IL (above). In-game verification of the sequence
`CanAddItem -> ForceRemove -> AddItem` per chunk is still the `AUTHOR-VERIFY` item 4 above.

---

# Task 9 — Fix round 3 (invariant by construction: exact gate + refund)

Date: 2026-10-11
Commit: `d3760be` — fix: guarantee drawer-pull invariant with an exact delivery gate (4 files, +388 / −58)

A prior agent had begun this round but left it uncommitted and unreported. I re-derived every claim
from the IL, added a test, and committed it. Nothing below rests on the earlier agent's word.

## The defect round 2 left standing

Round 2 gated each chunk with `destination.CanAddItem(prefab, chunk)`, then removed the chunk with
`ForceRemove` and delivered it with `destination.AddItem(prefab, chunk)`. The gate did not imply the
delivery — the two APIs match on different predicates.

Verified in IL against `.references/Valheim/1.0.16/assembly_valheim_publicized.dll` (Mono.Cecil in a
child process, so the handle dies with it):

- `Inventory.AddItem(ItemData)` (maxStack > 1) loops calling
  `FindFreeStackItem(name, quality, worldLevel)`, which matches **name AND `m_quality` AND
  `m_worldLevel`** and requires `m_stack < m_maxStackSize`, placing one item per call across every
  matching stack; for whatever is left it sets `item.m_stack = remaining` and puts the remainder into
  **one** empty slot (`FindEmptySlot(TopFirst(item))`), returning true; with no empty slot it returns
  false. The `maxStack == 1` branch uses an empty slot only.
- `Inventory.CanAddItem(ItemData, int)` sums `FindFreeStackSpace(name, worldLevel)` — matches
  **name AND `m_worldLevel` but NOT quality** — plus `(m_width*m_height − m_inventory.Count) *
  m_maxStackSize`, and returns `capacity >= stack`. It is the looser predicate.
- `Inventory.AddItem(GameObject, int)` clones the prefab's default `ItemData` and sets
  `m_worldLevel = (byte)Game.m_worldLevel`; `CanAddItem(GameObject, int)` uses the prefab's default
  world level — the `GameObject` pair disagrees on quality *and* world level.

So a same-name partial stack of a different quality, with no empty slot, made
`CanAddItem(prefab, chunk)` true while `AddItem(prefab, chunk)` returned false — and by then the
chunk had already left the drawer and was destroyed. Note: the brief's suggested
`CanAddItem(ItemData)` / `AddItem(ItemData)` "consistent pair" is **not** consistent on quality, so
that route was not taken.

## What I changed

**`Logic/DrawerTake.cs` (pure; `System` + `System.Collections.Generic` only).**
- `InventoryStack` — a game-free view of one destination stack (`Name`, `Quality`, `WorldLevel`,
  `Stack`, `MaxStack`).
- `MatchingFreeStackSpace(name, quality, worldLevel, stacks)` — mirrors the predicate
  `FindFreeStackItem` merges on: a stack counts only when name, **quality** and world level all match
  and it is not full; free space is summed across every matching stack (matching `AddItem`'s
  one-item-at-a-time merge).
- `CanDeliver(chunk, maxStack, matchingFreeStackSpace, emptySlots)` — `chunk` must be in `1..maxStack`
  and fit `matchingFreeStackSpace + emptySlots * maxStack`. Exact for `chunk <= maxStack` (one
  `AddItem` merged across matching stacks plus at most one empty slot).
- `AmountToRemove` and `NextChunk` unchanged.

**`Storage/DrawerSource.cs` (`Take`).** For each stack-sized chunk: recompute the destination's
matching free space and empty slots; `CanDeliver` gate **before** `ClaimOwnership` +
`InvokeRPC("ForceRemove", chunk)`; materialise the chunk via `blueprint.Clone()` at the drawer's
stored quality and `(byte)Game.m_worldLevel`; `destination.AddItem(ItemData)`. If `AddItem` still
returns false, `Refund(itemName, chunk, quality)` calls `ClaimOwnership()` +
`InvokeRPC("AddItem_Request", prefab, amount, quality)`. Returns `delivered`.

**`Storage/DrawerApi.cs`.** Added `const string AddItemRequestRpcName = "AddItem_Request"`.

## Why the invariant now holds by construction rather than by argument

For every chunk, `removed == delivered`:

1. The chunk is at most one stack, so it is exactly what one `AddItem(ItemData)` can place.
2. `CanDeliver` is the **same predicate** `AddItem(ItemData)` matches on (name + quality + world
   level free stack space, plus an empty slot for the remainder), so a chunk whose delivery would be
   refused is **never removed**: the gate is evaluated before the `ForceRemove`, and the IL shows the
   gate cannot be true while the delivery returns false (`CanAddItem`, which was true here, is not
   consulted). No removal precedes a delivery that may fail, and a failed `AddItem` therefore cannot
   leave a partially-mutated inventory behind.
3. `delivered` is incremented only after `AddItem` returns true and is what `Take` returns; the
   refund restores the drawer's `Amount` through its own add RPC if the (unreachable) branch is ever
   hit. A null destination, an unresolvable prefab/item drop, or no room for the first chunk removes
   nothing and returns 0.

Verified the drawer side too (Mono.Cecil, `kg_ItemDrawers.dll` 1.4.0): `DrawerComponent.Awake`
registers `"ForceRemove"` → `RPC_ForceRemove(long,int)` and `"AddItem_Request"` →
`RPC_AddItem(long,string prefab,int amount,int quality)`; `RPC_ForceRemove` only does
`CurrentAmount -= Min(amount, CurrentAmount)` (and `CurrentAmount` is the ZDO `"Amount"`) and never
clears prefab/quality; `RPC_AddItem` dumps the existing contents **only** when the incoming prefab or
quality differs from what the drawer holds, so a refund with the drawer's own prefab+quality merely
restores `Amount` (no world drop). `RPC_WithdrawItem_Request` is never called (it would spawn a world
drop at the player via `AddItem_Player` and clear an empty drawer's fields).

## Covering tests (pure decisions) — command and output

Command:
```
dotnet test C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\Tests\CraftFromDrawersAndChests.Tests.csproj
```
Output:
```
Passed!  - Failed:     0, Passed:    74, Skipped:     0, Total:    74, Duration: 129 ms - CraftFromDrawersAndChests.Tests.dll (net462)
```
74 = 68 (round 2) + 6 new/extended in `Tests/Logic/DrawerTakeTests.cs`. The decisive ones:
- `TheDefectCase_SameNameDifferentQualityWithNoEmptySlot_IsRefused` — the exact round-2 hole: a
  quality-2 partial stack of `$item_wood` gives zero matching free space, so the chunk is refused
  (and a single empty slot makes the very same chunk deliverable).
- `MatchingFreeStackSpace_SumsAcrossEveryMatchingStack` — the merge uses the sum over matching
  stacks (15 + 3 = 18), not just the largest.
- `CanDeliver_AgreesWithAStackByStackAddItemSimulation` — now also covers two matching partial stacks
  (and a different-quality stack beside them); a per-item simulation of
  `FindFreeStackItem`-then-one-empty-slot agrees with `CanDeliver` for every chunk `1..maxStack` and
  0–2 empty slots.
- `CanDeliver_FitsMatchingFreeSpaceOrAnEmptySlotButNeverMoreThanOneStack`,
  `MatchingFreeStackSpace_CountsOnlyNameQualityAndWorldLevelMatches`,
  `AmountToRemove_ThenChunkedStillDeliversEverythingTaken`, `NextChunk_*` — clamp/chunk/refuse
  behaviour.

## Build

```
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug
...
BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```
`MSB3274` (MultiUserChest is `net472` vs this project's `net462`) is the only warning, as expected.

## Still only verifiable in game (stated explicitly)

The **pure** decisions (how much to take, how to chunk it, and whether a chunk's delivery is
guaranteed) are unit-tested above. The **game-facing materialisation is not unit-tested**: resolving
the prefab through `ObjectDB`, reading `Inventory.m_inventory`/`m_width`/`m_height`, calling
`Inventory.AddItem(ItemData)`, and the `ForceRemove` / `AddItem_Request` RPCs all need the Valheim
runtime plus a live drawer and `Inventory`. They were verified by reading the IL (above). Still for
the game:

1. Drawer holds 2× of a quality-1 item; the player already has a partial quality-2 stack of the same
   item and a full inventory: `Take("Wood", 2, inv)` must return **0** and remove **nothing**.
2. Same but with one free slot: the take must land exactly 2 in the player's inventory and remove
   exactly 2 from the drawer.
3. A request larger than one stack (`Take("Wood", 70, inv)` with a 20-stack item): the drawer must
   lose exactly what the player receives, in stack-sized pieces, with nothing on the ground.
4. The refund branch is unreachable while the gate is exact; if it is ever exercised, confirm
   `ClaimOwnership()` + `InvokeRPC("AddItem_Request", prefab, amount, quality)` restores the drawer's
   `Amount` (no world drop). That depends on `IsOwner()` being true by the time the handler runs — a
   multiplayer-timing question.

