# Task 13 — fix round 2

One narrow correction to round 1 (commit `8187394`). Round 1's R2 said a mid-loop `Deferred` should
return the delivered count rather than the sentinel. **That was wrong, and it opens an over-delivery
(duplication) path.** Invert it.

## The defect

`VanillaChestSource.Take` now loops across stacks, which is right. But when a removal inside that loop
is routed, round 1 returns `delivered > 0 ? delivered : ChestTake.Deferred`. Returning the partial
count makes `PullRunner` (correctly, per R3) retry for the remainder — and that remainder is **already
in flight**, so it is requested a second time.

Worked example: a 50-item line against a chest holding 2 × 30.
1. `Take(…, 50)` removes 30 locally, then asks for the remaining 20 and that removal **routes**.
2. Round 1 returns `30`; the in-flight 20 is not reported.
3. `PullRunner` sees `30 < 50` — a positive short — and retries for the remainder (20).
4. The retry removes 20 locally.
5. The line is satisfied, but 30 + 20 (routed) + 20 (retry) = **70 items left the chest for a 50-item
   line**.

## The correction

**Propagate the sentinel whenever a call in the loop defers, regardless of what was already
delivered.** The sentinel's meaning is precisely *"the entire un-returned remainder of this request is
in flight"* — the loop asks for `amount - delivered`, so a routed answer covers exactly that
remainder, and `delivered + routed == requested`. The caller already treats the sentinel as the line
being satisfied, which is then exactly right, and it does not retry — so nothing is requested twice.

So the loop becomes, in effect:

- removal returns the sentinel → **return the sentinel immediately**, discarding the accumulated count
  (the count is not needed: `PullRunner` needs only the verdict, and the delivered items are already in
  the player's inventory);
- removal returns `0` → break, return the accumulated count (source exhausted — a genuine shortfall);
- otherwise accumulate and continue.

Because the caller reacts to the sentinel the same way whether or not anything was delivered first,
round 1's "`return delivered > 0 ? delivered : Deferred`" distinction buys nothing and is what causes
the double-request.

Update the contract text accordingly — `VanillaChestSource.Take`'s remarks and the
`IStorageSource.Take` notes you added in round 1 currently describe the partial-count rule and would
now be wrong. The caller-side rule from round 1 stands unchanged: the sentinel satisfies the line, is
never counted, never summed and never retried.

## Also

- Check whether anything else in the round-1 change assumed a routed mid-loop removal yields a partial
  count rather than the sentinel, and correct it.
- `PullRunner`'s behaviour does not change, so its tests should not need editing; the sentinel's
  meaning changes on the source side only. If a test's *name or comment* now misdescribes the source's
  contract, fix the wording — do not weaken an assertion to make a change pass.

## Scope and verification

Everything from round 1 still binds: `net462`/BepInEx 5.4.2350/Jötunn 2.30.2; never write a
`Container`'s inventory; chest removal only through `MultiUserChestBridge.RemoveFromChest`; `Logic/**`
stays pure and game-type free; storage stays prefab-keyed; pulls stay all-or-nothing; register new
files in the csproj; no ctrl-click key handling; no station-feeding; **do not push to any remote**.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK`.
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 117 or more).
3. Commit, then append a **Fix round 2** section to `task-13-fix-report.md` covering: the corrected
   rule; why propagating the sentinel cannot over-deliver; what you checked for other round-1
   assumptions of the old rule; build and test result lines; anything still racy.

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report
path.
