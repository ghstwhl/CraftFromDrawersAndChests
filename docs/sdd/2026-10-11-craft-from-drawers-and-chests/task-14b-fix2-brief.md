# Task 14b — fix round 2

Round 1 correctly closed the reported false refusal and the per-press over-pull, and the predicate
extraction and pure-reader work are sound. But it changed the **source** half of the shortfall rule to
a stricter identity, and that reintroduces a free-craft path. One Critical, one Important with the same
root cause.

## Critical — the held count must be a *superset* of what vanilla will consume

The review established vanilla's consume identity from the assembly (sanctioned probe):

- `Player.ConsumeResources(Requirement[], Int32, Int32, Int32)` calls
  `Inventory.RemoveItem(String, Int32, Int32, Boolean)` — keyed by a **string (the shared name)**, not
  by an `ItemData`, so quality is not the consume identity and a same-name stack at a different quality
  **is** removable.
- `Inventory.CountItems(String, Int32, Boolean)` — vanilla's requirement test passes quality `-1`
  (= any), so vanilla's own held count ignores quality too.

The pre-fix `CountHeld` counted by `item.m_dropPrefab.name`, summing every stack regardless of quality
or world level. That made the mod's held count a **superset** of vanilla's — and that superset is
precisely what made the safety chain true:

> vanilla returned true ⇒ our shortfall is 0 ⇒ nothing is requested ⇒ the consume prefix cannot decline
> on a path vanilla had already cleared.

Round 1 replaced that with `DrawerTake.IsSameItem` / `MatchingAmount` (name **+ quality + world level**),
which is a **subset**. Consequence chain:

1. A held stack whose `(quality, worldLevel)` differs from the requirement's prefab item data is counted
   `0`, so the shortfall inflates to the **whole** requirement.
2. The gate cannot catch that state: both postfixes early-return when vanilla returned `true`
   (`CraftPatches.cs:84`, `:119`) — and by vanilla's own count the player *has* enough.
3. So the only guard left is the consume prefix, which re-plans with the same inflated rule and returns
   `false` — **after** `DoCrafting`/`TryPlacePiece` has already produced the item. A craft or placement
   for free.

It also re-enters the previous round's Important #2 from the other side: the gate can now decline a
craft vanilla would have completed, and a press can drain more storage than the player actually lacked.

The three-part identity is correct for the **destination** half — `Inventory.AddItem`'s merge predicate,
where quality and world level genuinely matter. It is wrong for the **source** half.

### R28 — split the two identities

- **Source (held counts): count by shared name, ignoring quality and world level** — a strict superset
  of what vanilla will consume. Either restore the prefab-keyed notion or add a name-only count; do not
  use `IsSameItem` here.
- **Keep `IsSameItem`/`MatchingAmount` for the destination/free-space half**, where `AddItem`'s merge
  predicate needs it. Do not delete that work — it is right where it is.
- Over-counting held can only make us pull *less* from storage, and any shortfall is then paid from the
  player's own inventory by vanilla's own consume — the safe, recoverable direction. Under-counting is
  what produces a free item.

**Then prove the invariant rather than asserting it.** The report's "in practice identical for materials"
was not proof and is what the review rejected. Pin the property in a test: for any inventory state, the
mod's held count for a requirement is **≥** vanilla's, so a shortfall can never be inflated past what
vanilla would consume. A pure property test over generated inventories (qualities 0/1/2+, world levels
0/1, mixed stacks) is the right shape and belongs in `Logic/` with the rest.

**Also fold in Minor 5**, because R28 makes it moot: the readout (`PullKeyPatches.cs:263`) and
`HasAnyOfEach` (`CraftPatches.cs:337`) already use the prefab-keyed, quality-ignoring `CountHeld`, so
once the source half is name-based there is **one** held notion again. Say so in your report and make
sure no third rule survives.

## Minor to fix

- **Stale doc.** `PullEngine.cs:145-152` still advertises "Pass null to request the full requirement (the
  'pull the whole recipe' case)" — no production caller passes null any more (`PlanFor` is the only
  path), so it documents a case the design deliberately deleted.
- **The reader's real degradation path is untested.** `PullKeyPatches.PlacementStatusField == null` is
  the game-facing half that degrades silently if a field is renamed; `PlacementStatusOf(null)` cannot
  occur for a real enum-typed field, so that test pins a contract rather than the failure. Pin the real
  one.
- **Redundant assertion.** In each new guard test (`BuildPullRulesTests.cs:53`, `:64`) the prevent key
  alone already refuses, so only the first assertion pins the pull-key requirement.

## Deferred — do not fix

The lost no-op feedback (`PullKeyPatches.cs:109-110`, `:208-209` return silently where CFC prints its
pulled message). The review settled this as **acceptable and documented**: CFC swallows the press on
both sites too, and CFC's message is unconditional inside its requirement loop (`1261-1262`) — but
misleading, since it prints "pulled" when nothing moved. Do not add a message this round.

## Scope and verification

Everything that bound Task 14b still binds: `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; `Logic/**`
free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`; **never write a `Container`'s inventory or a
drawer's ZDO `"Amount"`**; storage keyed by **prefab name**; `-1` (`ChestTake.Deferred`) never a count,
never summed, never retried; **items must never end up on the ground and a craft or placement must
never cost nothing**; new files registered in the legacy csproj's explicit `<Compile>` items; no scratch
or inspection scripts in the tree; no Nexus artefacts; `MSB3274` expected and untouched; no
station-feeding; no console command; **do not push to any remote**.

**R25:** use the sanctioned probe (`tools\cecil.ps1`, now with `-Fields`); do not write your own.
**R26:** report parameter types at call sites — a name-only scan cannot distinguish overloads.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 186 or more)
3. Commit, then append a **Fix round 2** section to `task-14b-report.md` covering: which count feeds the
   source half now and why it is a superset; the property test that pins it and what it generates; that
   only one held notion survives; the Minors; build and test lines; and confirmation that the
   free-craft chain (vanilla-true ⇒ shortfall 0 ⇒ no refusal) is now true by construction.

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report
path. Do not narrate your work in the reply.
