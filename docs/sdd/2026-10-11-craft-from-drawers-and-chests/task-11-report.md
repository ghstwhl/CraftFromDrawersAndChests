# Task 11 Report — MultiUserChest removal bridge

Date: 2026-10-11
Branch: `feature/craft-from-drawers-and-chests`
Commit: `faf050b` — feat: route chest removal through MultiUserChest (3 files, +243 −11)

Base: `6ee6868`.

## Context: a prior agent left unverified, unbuilt, uncommitted work

The tree arrived with three modified/new files and no build, test, commit or report:

- `CraftFromDrawersAndChests/MultiUserChestBridge.cs` — new (150 lines).
- `CraftFromDrawersAndChests/Storage/VanillaChestSource.cs` — `Take` implemented (+71/−11).
- `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` — reference recovery target added.

I verified all three against the brief and the six "requirements the brief cannot know". The bridge
and the source's `Take` were already correct. **The csproj was broken: the project did not compile at
all.** I fixed it (details below), then built, tested, committed.

## Verdict per requirement

| # | Requirement | Where | Status |
|---|---|---|---|
| 1 | Storage keyed by **prefab name** (`m_dropPrefab.name`), mirroring `Count` | `VanillaChestSource.FindLargestStack` matches `string.Equals(item.m_dropPrefab.name, itemName, Ordinal)`; `m_shared.m_name` is never used as a key | ✅ |
| 2 | Returned count must be **definite and synchronous**; report a concern instead of an optimistic count if it cannot be | Bridge returns the **destination's own item-count delta** measured across the call. Local branch: applied inline → delta is definite. Routed branch: no synchronous count exists → returns 0 and logs a warning (see Concern 1) | ✅ with concern |
| 3 | Removal must **deliver into `destination`**; report the count that moved | `ContainerHandler.RemoveItemFromChest` credits `destination` inline on the local branch; the delta is exactly the number that landed there | ✅ |
| 4 | **Ownership**: unowned → claim first; owned by another player → do **not** claim | `if (!view.HasOwner()) view.ClaimOwnership();` — and nothing else. A chest another player holds open is left owned by them | ✅ |
| 5 | **Never read or write `chest.GetInventory()`** to perform the removal | The bridge never touches `chest.GetInventory()`; it passes the `Container` to MultiUserChest. `VanillaChestSource.Take` only *reads* `GetAllItems()` to locate the stack | ✅ |
| 6 | Register the new file in the legacy csproj | `<Compile Include="MultiUserChestBridge.cs" />` present | ✅ |

## The csproj bug (uncommitted work did not compile)

First build: `error CS0246: The type or namespace name 'MultiUserChest' could not be found`.

The prior agent's `AddMultiUserChestReference` target correctly keeps `MSB3274` unsuppressed and
re-injects the already-resolved DLL before `CoreCompile`. But it appended to the wrong item:

```xml
<Target Name="AddMultiUserChestReference" DependsOnTargets="ResolveAssemblyReferences" BeforeTargets="CoreCompile" ...>
  <ItemGroup>
    <ReferencePath Include="$(ReferencesRoot)\Mods\MultiUserChest.dll"><Private>false</Private></ReferencePath>
  </ItemGroup>
</Target>
```

Evidence (both from the installed MSBuild, VS 18 `Current\Bin`):

- `Roslyn\Microsoft.CSharp.Core.targets:153` — the `Csc` task's reference input is
  `References="@(ReferencePathWithRefAssemblies)"`. It does **not** read `@(ReferencePath)`.
- `Microsoft.Common.CurrentVersion.targets:2544` — `ReferencePathWithRefAssemblies` is *snapshotted*
  from `@(ReferencePath)` inside `FindReferenceAssembliesForReferences`, which is a dependency of
  `ResolveReferences`. That target has already run by the time `AddMultiUserChestReference` fires
  (confirmed with `/v:diag`: the target ran, `1 calls`, yet `MultiUserChest.dll` was absent from the
  `csc.exe` command line).

So appending to `@(ReferencePath)` after the snapshot never reaches the compiler. **Fix:** append the
DLL to `@(ReferencePathWithRefAssemblies)` as well (keeping the `@(ReferencePath)` entry for anything
that inspects the resolved set), with the comment corrected to explain why. `MSB3274` still prints on
every build, as required, and `MultiUserChest.dll` is **not** copied to `bin\Debug` (checked: absent),
so the reference stays compile-time only.

## How I established that the removal count is synchronous and definite

The brief warns that `ContainerHandler.RemoveItemFromChest` "returns a *request*; the removal is
applied elsewhere". That is only half true, and the bridge's design turns on the other half. I read
the real artefact — `.references\Mods\MultiUserChest.dll`, **assembly version 0.6.2.0** — not a web
summary: I dumped IL with Mono.Cecil *in a child process* (so no file handle leaks into the build) and
cross-checked against the matching `master` source (`github.com/MSchmoecker/No-Chest-Block`).

`ContainerHandler.RemoveItemFromChest` branches on `container.m_nview.IsOwner()` (IL offset 226):

```
220  ldarg.0
221  ldfld    ZNetView Container::m_nview
226  callvirt bool ZNetView::IsOwner()
231  brfalse.s IL_00ff                 ; not owner -> routed path
233  ldarg.0
234  callvirt Inventory Container::GetInventory()
239  ldloc.0
240  call     RequestChestRemoveResponse ContainerRPCHandler::RequestItemRemove(Inventory, RequestChestRemove)
246  ldarg.2
247  ldloc.1
248  call     void InventoryHandler::RPC_RequestItemRemoveResponse(Inventory, RequestChestRemoveResponse)
253  ldnull
254  ret                               ; owner path: applied inline, returns NULL
255  ldarg.0
256  ldfld    ZNetView Container::m_nview
261  ldstr    MUC_RequestItemRemove
266  ldloc.0
267  call     void GamePatches::InvokeRPC(ZNetView, string, IPackage)
272  ldloc.0
273  ret                               ; routed path: returns the request
```

- **Owner path is synchronous.** `ContainerRPCHandler.RequestItemRemove(chestInventory, request)`
  performs the removal on the chest (`inventory.RemoveItem(from, removedAmount)`) and returns a
  response carrying `removedAmount`; the very next call,
  `InventoryHandler.RPC_RequestItemRemoveResponse(destinationInventory, response)`, credits the
  **destination** in the same stack frame. The method then returns `null`. Nothing is deferred.
- **Routed path is not.** It calls `GamePatches.InvokeRPC(...)` and returns the request; the owning
  client applies it and routes a response back to *us*, whose handler credits our destination later.

Because the owner path is inline, the destination's matching-item delta measured across the call is a
definite, synchronous count. `ClaimOwnership()` makes the owner path reachable for an unowned chest:
its IL is `if (!IsOwner()) m_zdo.SetOwner(ZDOMan.GetSessionID())` — a local ZDO write with **no RPC**,
so `IsOwner()` is true immediately on return. That is how an unowned chest is serviced locally.

### Why the `(-1, -1)` sentinel is safe and does deliver

The bridge passes `to = new Vector2i(-1, -1)` ("choose a destination slot"):

- `InventoryBlock.CanBlockSlot` refuses negative slots, so `BlockSlot`/`ReleaseSlot` no-op for
  `(-1, -1)` and `IsSlotBlocked((-1, -1))` is always `false` — the guard in `RemoveItemFromChest`
  cannot silently turn the request into a no-op.
- `PossibleDragAmount` with `to < 0`: if the destination has an empty slot, `dragAmount` is
  `min(amount, item.m_stack)`; otherwise it is clamped to
  `FindFreeStackSpace(m_name, worldLevel)`. I confirmed the game assembly exposes
  `Inventory::FindFreeStackSpace(String, Single)` (Valheim `1.0.16`), so the path resolves.
- `RPC_RequestItemRemoveResponse` then takes its `responseItem != null` branch (because
  `pos.x < 0`), i.e. `tmp.AddItem(...)` followed by `inventory.MoveItemToThis(tmp, responseItem)`,
  which credits the destination. Overflow would be dropped on the ground, but the `FindFreeStackSpace`
  clamp above makes that unreachable for a `(-1, -1)` request.

## Build / test

- `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
  → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)` (41,472 bytes). `MSB3274` prints, expected.
- `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
  → `Passed! Failed: 0, Passed: 83, Skipped: 0, Total: 83` — every Task 1–10 test, unchanged.
- `Logic/**` untouched; no Harmony patch, toggle-write or pull-execution code added.

## Concerns

1. **The routed branch returns 0, which is definite but not accurate.** When the chest is owned by
   another player we deliberately do not claim it (Requirement 4), so MultiUserChest routes the
   request and no synchronous count exists. Returning the requested amount would be the optimistic
   claim Requirement 2 forbids, so the bridge returns 0 and logs a warning. The item *does* still
   leave the chest exactly once and *does* arrive in our destination — just asynchronously. The
   two-player case therefore satisfies "the item moves exactly once", but a future pull caller must
   **not** read a 0 from a chest it does not own as "the chest was empty" and retry, or it could
   double-take. There is no pull-execution code in scope, so this is latent; it is recorded here
   because it is exactly the class of misuse the all-or-nothing rule is meant to prevent.
2. **`Take` services one stack per call.** MultiUserChest's protocol is a one-slot operation, and the
   brief says `Take` locates *the* `ItemDrop.ItemData` and returns the bridge's result, so `FindLargestStack`
   picks the single largest matching stack and the return never exceeds that stack — even when
   `amount` is larger and the chest holds several smaller stacks. `Take` returns the true (smaller)
   amount, so a caller that needs the full amount must call it again; a caller that assumes "returned
   the requested amount or the chest was empty" would be wrong. This matches the brief and is
   documented on `Take`/`FindLargestStack`, but it is a real limitation of the interface.

## AUTHOR-VERIFY (brief Step 4 — requires the game, cannot be run here)

Two clients, second player holds the chest open:

1. Player A places a chest with e.g. 50x Wood; player B opens it and **leaves it open** (B owns it).
2. On A, trigger the debug removal of 1x Wood (a manual call to
   `MultiUserChestBridge.RemoveFromChest(chest, item, A_inventory, 1)`; the pull path is not wired
   yet, so this must be invoked from a temporary debug hook — do **not** add it to the shipped mod).
   Expected: A's bridge logs `Chest is managed by another player; routed a request for 1x 'Wood'` and
   returns 0. Observe on **both** clients that the chest shows 49x Wood and A's inventory gained 1x,
   each exactly once, with no chest-content divergence between the two clients and no "chest moved"
   flicker on B's screen (A never claimed ownership).
3. Repeat with the chest **closed** (no owner). Expected: A claims ownership via
   `ClaimOwnership()`, the call returns 1 synchronously, and A's inventory gains exactly 1x. The
   chest is now owned by A.
4. Repeat step 3 with a destination inventory that is full of a different item and has no matching
   free space. Expected: the bridge removes nothing and returns 0 (not a positive count), and no Wood
   is dropped on the ground.

If step 2 shows the count on A only after a delay, or the chest contents diverge between clients, the
routed-branch handling (Concern 1) is the first suspect.

---

# Task 11 Fix Report — routed-removal `-1` sentinel + availability predicate

Date: 2026-10-11
Commit: `4198333` — fix: distinguish routed chest removal with a -1 sentinel
Base: `faf050b` (the Task 11 implementation reviewed above)
Addresses the review's Finding 1 (Important) and Finding 2 (Minor, same code).

## What was wrong

Ground truth (fetched MultiUserChest `master`, `MultiUserChest/ContainerHandler.cs`, and it matches
the 0.6.2 DLL shipped at `.references\Mods\MultiUserChest.dll`): `RemoveItemFromChest` has three
exits, and the old bridge collapsed two of them.

| Exit | `RemoveItemFromChest` returns | Meaning |
|---|---|---|
| Applied inline (we own the chest) | `null` | removal + delivery done synchronously; definite count |
| Declined (`dragAmount <= 0` after `PossibleDragAmount`, or the slot is blocked) | non-null request, `dragAmount == 0` | nothing removed; definite zero |
| Routed (another player owns the chest) | non-null request, `dragAmount > 0` | RPC already sent; outcome unknown |

- **Finding 1 (Important):** the old bridge returned `0` for *both* "nothing removed" and "routed".
  A pull executor would read the routed `0` as "chest empty" and retry — but the item *does* still
  move and arrives later, so the retry duplicates it.
- **Finding 2 (Minor):** the warning branch `request == null && delivered == 0` was dead: the
  declined case returns a *non-null* request with `dragAmount == 0`, so the genuine
  "chest empty / destination full" case logged nothing.

## Changes

1. **`Logic/ChestTake.cs` (new, pure).** The decisions, unit-testable without the game:
   - `const int Deferred = -1`
   - `CanServeSynchronously(bool hasOwner, bool isOwner)` → `!hasOwner || isOwner`
   - `WasRouted(bool requestIsNull, int requestDragAmount)` → `!requestIsNull && requestDragAmount > 0`
   - `Result(bool routed, int delivered)` → `routed ? Deferred : max(delivered, 0)`
   - `DeliveredNothing(bool routed, int delivered)` → `!routed && delivered <= 0`
2. **`MultiUserChestBridge.cs`.** `RemoveFromChest` computes `routed` from the request and returns
   `ChestTake.Result(routed, delivered)`: the delivered count (`>= 0`) normally, `-1` when routed. The
   warning is now based on `delivered` (Finding 2), so "chest empty / destination full" logs. Added
   `public static bool CanRemoveSynchronously(Container)`.
3. **`Storage/VanillaChestSource.cs`.** Added read-only `bool CanTakeSynchronously { get; }` delegating
   to the bridge; `Take` propagates the bridge's value verbatim, including `-1` (documented).
4. **`Storage/IStorageSource.cs`.** `Take`'s contract now states `>= 0` items taken, or
   `ChestTake.Deferred` (`-1`) when routed.
5. **csproj** registers `Logic\ChestTake.cs`.
6. **`Tests/Logic/ChestTakeTests.cs`** (new, 11 cases) pins all five decisions.

## The contract, exactly as now documented

- `IStorageSource.Take` / `VanillaChestSource.Take` / `MultiUserChestBridge.RemoveFromChest` all
  return **`>= 0` = the number actually removed** (may be fewer than requested, or `0`), or
  **`-1` = `ChestTake.Deferred` = the removal was routed to another player and the count is not
  knowable synchronously**.
- **`-1` is not a failure and must not be retried.** The items are already leaving the chest and will
  arrive later, so a retry duplicates them. This is the sentinel the all-or-nothing pull executor
  relies on.
- A definite local zero (chest held none, destination full, invalid input) stays `0`, distinct from
  `-1`; `Result` clamps a miscounted negative delta to `0`, so a stray negative can never masquerade
  as the sentinel.

## How a caller uses the availability predicate

An executor that needs a definite outcome must call `VanillaChestSource.CanTakeSynchronously`
(or `MultiUserChestBridge.CanRemoveSynchronously(container)`) **before** `Take`, and skip any source
for which it is `false`. It is `true` for an unowned chest (we claim it, so the removal is local) and
for a chest we already own, and `false` for a chest another player owns. Checking afterwards is too
late: a routed request has already been sent by the time `Take` returns.

## Build / test

- `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
  → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)` (41,984 bytes). `MSB3274` prints, expected.
  First attempt was RED: `CS0103: The name 'ChestTake' does not exist in the current context` in
  `MultiUserChestBridge.cs` — the bridge needed `using CraftFromDrawersAndChests.Logic;`. Added, then
  green.
- `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj`
  → `Test summary: total: 94, failed: 0, succeeded: 94, skipped: 0` — every Task 1–10 test plus the
  11 new `ChestTakeTests`.

## Testability note

The parts that could be made pure **are** pure and covered: the availability predicate, the
routed / inline / declined classification, the delivered-count-or-sentinel return, and the warning
condition — all in `Logic/ChestTake.cs`, 11 NUnit cases. The game-facing remainder
(`ZNetView.HasOwner()`/`IsOwner()`, `ContainerHandler.RemoveItemFromChest`'s request, the destination
delta) cannot be exercised without the game, so it is not covered here; the Task 11 AUTHOR-VERIFY
two-client steps above still apply.

## Concerns

- None new. The earlier Concern 1 ("a pull caller must not read `0` from another player's chest as
  empty and retry") is now *structurally* prevented rather than merely documented: the routed case
  returns `-1`, and `CanTakeSynchronously` lets a caller decline before anything is sent.
- The Task 11 AUTHOR-VERIFY steps still require a live two-client session and were not run here. In
  step 2 the expected bridge return is now `-1` (was `0`).
