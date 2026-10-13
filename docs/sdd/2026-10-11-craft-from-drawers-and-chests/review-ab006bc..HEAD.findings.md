# Fresh-eyes review of `ab006bc..HEAD` — findings and triage (2026-10-13)

Requested by the owner before publishing, because the 0.1.12 and 0.1.13 changes were made inside the
live reported-bug loop and never had the per-task review rounds of Tasks 1–15. Performed by an
independent agent with no prior context, given the diff (`review-ab006bc..HEAD.diff`) plus the
invariants, and asked to *test* the author's claims rather than accept them. It built the product, ran
the tests, decompiled the relevant game methods and inspected the built DLL's call graph.

## Verdict: no Critical

It tried to break the three hard invariants the diff touches and could not. Notably it confirmed **from
IL** that `CountItems(name,-1,false)` sums every same-shared-name stack, which makes vanilla's own gate
(`max over q of CountItems(name,q,true)`) a subset of our held count — so R28's anti-free-craft chain
holds *by construction* at the gate and consume sites. Also clean: no `Container.m_inventory` or drawer
ZDO write, no `WithdrawItem_Request`, class-level `[HarmonyPatch]` intact on all three patch classes, the
deleted members referenced nowhere, targets public and present.

## Important

**I1 — the held-count comment was factually wrong, and the wrong reason was load-bearing.**
`CountHeldBySharedName` claimed station feeding never reads the held count. It does: `StationPatches`
plans a station's shortfall through the same planner. It is safe only because Adventure Backpacks' craft
scope is *closed* on those interaction paths, so AB contributes nothing exactly where a station could not
consume it. A future AB change or a new caller would silently count a backpack for a station and
under-feed it. **Fixed in 1.0.1** — the comment now records the dependency instead of asserting a
separation that does not exist.

**I2 — the backpack is not counted for the ctrl-click pull.** `InventoryGui.OnCraftPressed` is not one of
AB's scope-entering methods, so held excludes the backpack there; on `Player.UpdatePlacement` both mods
patch as *prefixes* with no ordering guarantee, so it is nondeterministic. Effect: the readout says
`24/1` and the craft button is enabled, yet ctrl-clicking to pull computes the whole requirement as
shortfall and can refuse, naming items the player is visibly carrying. Safe direction (no free craft),
user-visible, and contrary to the CHANGELOG's "at every place it is read".
**Queued for 1.0.2** — the fix needs AB's documented API (count the equipped backpack ourselves, gated on
`IsCraftingContextActive` so the two notions can never double-count).

**I3 — 0.1.12's station confirmation may be host-only.** It reads the station's ZDO synchronously after
`InvokeRPC`, which is trustworthy only on the peer that executes the call (the ZDO owner). On a non-owner
client the read-back is stale, so the confirm would fail, the loop would stop, and the four paths would
fall through to vanilla — reproducing the exact symptom 0.1.12 removed, with the pulled unit stranded in
the inventory. The reviewer could not verify `InvokeRPC`'s local-update semantics from the repo and
neither can the author from here; the owner validated on the host.
**To be settled by the owner's multiplayer pass** — a client, not the host, feeding a station. Fix if it
reproduces.

**I4 — deleting `HeldAmountTests` left R28 with no automated guard.** The method now lives in the
game-facing `Pull/`, which is not compiled into the test assembly, so the superset property has no test.
**Accepted and documented.** The suggested recovery (a pure constant for the `(-1, false)` argument tuple
plus a test) only detects an edit that stops using the constant, so it was judged low value against I2/I3.

## Minor (recorded; not all acted on)

- **M1** `LogReadout`'s `ToggledOff`/" OFF" branch is dead — collection drops switched-off storage first —
  and the doc claims otherwise; "source(s) in range" really means "eligible source(s)". The limitation is
  real, the code and comment disagree. Part of the queued diagnostic work.
- **M2** `Debug` defaulted to **true**, shipping the new diagnostic into every player's log, whose dedup
  key includes the whole-metre distance and so re-logs as the player walks. **Fixed in 1.0.1** (default
  false).
- **M3** `LogReadout` runs before the amount element's null checks, so it can describe a readout that was
  never shown.
- **M4** "crafting and building readout" is inaccurate — the only readout is `InventoryGui.SetupRequirement`
  (crafting), and the same file's class doc says there is deliberately no build readout.
- **M5** The 0.1.12 changelog's hint example drops the " max" token the code actually emits.
- **M6** `DrawerTake.MatchingAmount` is production-dead, kept alive by its test.
- **M7** `HeldCounts`' doc says an unresolvable requirement adds no entry; it now adds a `0` entry
  (behaviour identical).
- **M8** `StationPatches` still carries a 0.1.11-leftover comment contradicting the code 40 lines below it.
- **M9** The README repeated Item Drawers' *mislabeled* hotkey. **Fixed in 1.0.1** — the claim is dropped,
  and the verified finding (its real hotkey is LeftShift+E, not Left Alt+E) is recorded at the key default
  instead.
- **M10** The README's delivery rule understates: empty slots serve stackable items too.
- **M11** `FireplaceGetHoverTextPostfix` declares an unused `__instance`.

## Could not be verified from the repo (stated, not assumed)

Adventure Backpacks 2.3.4's own gating arithmetic (the author's quoted source is the basis); Harmony
prefix ordering between AB and this mod; `ZNetView.InvokeRPC` local-update semantics; the game's
localization tokens; and the in-game ordering behind the 0.1.12 report.
