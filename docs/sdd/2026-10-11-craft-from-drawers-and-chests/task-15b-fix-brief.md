# Task 15b — fix round 1

No Critical. Two Importants from one root cause, plus Minors. Everything *around* the fill key is
verified correct against the oracle and the artifact: the binding, the polarity, the three gated call
sites (fireplace, smelter ore, smelter fuel — and deliberately **not** the cooking patches or the turret,
matching CFC), the hover hints, the console command and its `reset` caveat, the csproj registration
proven on the built DLL (36/36 bijection), and the R29/R30 visibility discipline.

## Important #1 — the fill key inverts on any shortfall

`StationPatches.cs:305-306` (also `:366-368`, `:423-424`) asks for `FillAmount(fillAll, remaining)` — the
station's **whole** remaining capacity. That amount becomes a single `Piece.Requirement.m_amount`
(`:641-668`) and goes through `PullEngine.PullToInventory`, whose planner is **all-or-nothing**:
`PullPlan.cs:192` discards the entire plan on a single shortfall, and `PullEngine.cs:424`
(`if (!plan.Feasible) return false;`) returns **before** `DestinationAllows`/`Execute` at `:427-429`, so
nothing moves. The caller then **ignores that result and returns `true`** (`:306-307`), so vanilla runs
with an empty hand.

Net effect: with the key held and storage short of `capacity − held`, the interaction fuels the fire
**0** where the unmodified interaction fuels it **1**. Holding the key is strictly worse than not holding
it — the opposite of what the setting says.

CFC never does this: it tops up per container —
`amount = pullAll ? Min(maxFuel − CeilToInt(fuel), item.m_stack) : 1` inside a
`foreach (Container c in nearbyContainers)` loop that continues until the station is full or the
containers run out (CFC `317` fireplace, `503` smelter ore, `624` smelter fuel; loop tails at
`336`/`531`/`641`).

### R31 — a fill is not a craft; do not plan it all-or-nothing

The all-or-nothing rule exists so a **craft** is never paid for out of storage and then not happen. A
**fill** is a different operation: "take as much as is available, up to the station's remaining capacity".

So: plan the fill so it requests up to the capacity but **accepts `min(available, requested)`** — it must
never refuse the whole fill merely because storage cannot cover the entire remaining capacity. Keep:

- the **`DestinationAllows` destination proof** for whatever is actually taken (nothing may be pulled
  that cannot be delivered);
- the existing `IStorageSource.Take` paths, `PullRunner`'s handling of the three answer shapes, and the
  `-1` (`ChestTake.Deferred`) rule;
- the station-capacity guard that stops a pull into a full station.

**At minimum, holding the key must never pull less than releasing it.** If storage can only supply part of
the capacity, take that part; if it can supply nothing, behave exactly as the unmodified interaction does
— and never leave the patch returning `true` after a declined pull while vanilla finds nothing in hand.

## Important #2 — no test can catch #1, so 228 green tests are not evidence for this feature

`Tests/Logic/StationFeedRulesTests.cs:112-150` pins `FillAmount`'s arithmetic (which does match CFC's
capacity half) but nothing exercises that amount against the planner — which is exactly where the
inversion lives. Add a test **at the level where the refusal happens**: a fill request that storage can
only partly cover must still take the available part, and a fill request storage cannot cover at all must
leave the caller behaving as the unmodified interaction does. Put the decision wherever it can be pure,
and test it there.

## Also required — make the tooltip honest, without pre-judging the parity work

Two places describe an effect the code cannot produce, because vanilla's own `Interact` still only
advances **one** item per interaction (`StationPatches.cs:250-252`), so a successful fill merely
pre-loads the player's inventory:

- the tooltip at `PluginConfig.cs:139` — "fills a station to its maximum instead of a single item";
- your own `AUTHOR-VERIFY` step 1 ("the fire fills toward its maximum").

Correct both. **The controller has now decided that true fill-to-max parity is wanted, and it is
scheduled as Task 15c** — so do **not** implement station delivery here. Do not leave a promise the code
cannot keep either: word the tooltip to describe what the key actually *requests* (the station's
remaining capacity, taken as far as storage allows) rather than claiming where the items end up, so it
stays true both before and after 15c. Note in your report that 15c will revisit the wording once delivery
into the station exists.

## Deferred — do not fix

Delivering the fetched items **into the station** rather than the player's inventory is Task 15c, now
scheduled by the controller's decision. Leave it to that task; do not attempt it here.

## Minors to fix

- **Null-guard consistency:** `TerminalPatch.StatusReport()` passes `PluginConfig.Flags` straight into
  `PullEngine.Collect` (`:118-121`) while `Reset` guards it (`:143-148`). `StorageRegistry.Collect`
  dereferences `flags.MaxRange`/`flags.EnableChests`, so a null snapshot would throw inside the prefix.
  Unreachable today, but make the two methods consistent.
- **Prevent-key divergence on the smelter fill paths:** CFC lets `pullAll` override `AllowByKey()`
  (CFC `465`/`582`, `!AllowByKey() && !pullAll`); the port keeps the prevent key authoritative
  everywhere. Documented and stricter, so keep it — but add it to the deferred-divergence list in your
  report so it lives somewhere durable.
- **The console command is not gated on `modEnabled`** (CFC `1503` is). Documented at
  `TerminalPatch.cs:44-48`; keep the behaviour, but list it as a deliberate divergence too.

## Deferred — do not fix

The fill-positive case depositing the fetched stack in the player's inventory rather than the station is
the same root cause as the parity question above — leave it to the controller's decision, documented.

## Scope and verification

Everything that bound Task 15b still binds: `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; `Logic/**` free
of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`; **never write a `Container`'s inventory or a drawer's ZDO
`"Amount"`** (chest removal only via `MultiUserChestBridge.RemoveFromChest`, drawer only via
`ForceRemove`); storage keyed by **prefab name**; `-1` never a count, never summed, never retried;
**items must never end up on the ground and nothing may be pulled that cannot be delivered**; new files
registered in the legacy csproj's explicit `<Compile>` items; no scratch or inspection scripts in the
tree; no Nexus artefacts; `MSB3274` expected and untouched; **do not push to any remote** — something in
the repository owner pushes commits in batches; commit locally and never push.

**R29/R30 still bind:** any new vanilla member or field you touch must be checked with
`tools\cecil.ps1` (`-Definitions` for methods, `-Fields` for fields) and be `public` in the
**unpublicized** assembly, or else reflected — and prefer removing the dependency when a public
equivalent exists. **R25:** the sanctioned probe only, never an ad-hoc one.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 228 or more)
3. Commit, then append a **Fix round 1** section to `task-15b-report.md`: how the fill is planned now and
   why it can no longer pull less than the unmodified interaction; the test that covers the refusal path
   and what it generates; the corrected tooltip and `AUTHOR-VERIFY` wording; the null-guard fix; the
   deferred-divergence list (station-delivery parity, prevent-key override, `modEnabled` gating); build
   and test lines.

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report
path. Do not narrate your work in the reply.
