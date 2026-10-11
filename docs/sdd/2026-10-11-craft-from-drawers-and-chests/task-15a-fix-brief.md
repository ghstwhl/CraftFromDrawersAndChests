# Task 15a — fix round 1

No Critical. One Important, verified by evidence, plus bundled doc corrections. The design and
item-safety work in this task are sound: capacity is proven before anything moves, the refactor is
delegation-only, the `-1` sentinel is untouched, and the fuel behaviour the brief doubted is now
probe-verified.

## Important — six direct calls to methods that are PRIVATE in the shipping assembly

`StationPatches.cs:148, :191, :235, :292, :302, :305` call these directly:

- `Smelter::GetQueueSize()`
- `Smelter::GetFuel()`
- `CookingStation::GetFuel()`
- `CookingStation::HaveDoneItem()`
- `CookingStation::GetFreeSlot()`
- `CookingStation::IsFireLit()`

**I settled their real visibility with the sanctioned probe, against both assemblies. Do not re-derive
it, and do not infer it from CFC:**

| Assembly | Result |
|---|---|
| `.references\Valheim\1.0.16\Managed\assembly_valheim.dll` — **what the game loads** | all six are **`private`** |
| `.references\Valheim\1.0.16\assembly_valheim_publicized.dll` — what we compile against | all six are `public` |

The publicized reference makes them bind at compile time; the shipping assembly does not expose them.
So build and every test stay green while the call fails or throws at runtime, where the prefix is
JIT-compiled — which can break the station's own interaction, not merely skip the feature.

**R29 — call only members that are public in the UNPUBLICIZED assembly directly; everything else goes
through reflection.** This is the project's established convention (`VanillaChestSource.cs:36` and
`PullKeyPatches.cs:64` already use `AccessTools` for exactly this reason) and it must now be applied to
these six.

### The fix

Route the six through `AccessTools.Method(...)` (or cached `GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)`), **cached in static fields** so this costs nothing per frame — these guards run inside per-frame station interactions.

**Fail closed.** If the `MethodInfo` is null or invocation fails, the station must simply take nothing:
log once, do not pull, do not throw. A missing member must degrade to "this station cannot be fed",
never to an exception thrown out of a prefix on a vanilla method.

`Turret::GetAmmo()` and `Turret::GetAmmoType()` are `public` in **both** assemblies — verified above — so
those direct calls are correct as they stand. Do not reflect on them.

Also correct `REFERENCES.md` §2, which claims that publicizing makes internal/private members usable
without reflection. The evidence above says the opposite, and that doc will mislead the next task.

## Bundled corrections from the same review

- **`StationPatches.cs:16-18`** — the class doc claims "every one of its station patches collects its
  sources with `GetNearbyContainers(__instance.transform.position)`". CFC's fireplace/smelter/cooking
  patches do; its **turret** patch measures from the player. The code's divergence from CFC is
  deliberate (the brief asked for the station's transform) and defensible, but the comment currently
  states the opposite of the truth. Fix the comment to state the divergence.
- **`StationPatches.cs:292`** — the `HaveDoneItem()` early-out is an unannounced CFC divergence inside a
  patch whose doc presents it as a CFC mirror. Conservative, but label it in code.
- **Turret ammo-type key.** `StationPatches.cs:364` uses `GetAmmo() > 0 ? GetAmmoType() : m_ammoType`,
  while CFC uses `GetAmmoType()` unconditionally for the prefab lookup and passes `m_ammoType` only to
  the inventory lookup — so the report's justification conflates two different keys. Verify from the
  unpublicized assembly whether `GetAmmoType()` can differ from `m_ammoType` when `GetAmmo() == 0`; if it
  can, mirror CFC. Decide on evidence; the failure mode is currently safe (log + no pull) so do not
  guess.
- **`StationFeedRules.IsDisallowedType`** trims entries and drops empties, where CFC does an untrimmed
  `Split(',').Contains(...)`. The mod's version is friendlier, so keep it — but state in the doc that it
  is a deliberate divergence from CFC, so the test pinning it is understood as documenting a choice
  rather than asserting the oracle.

## Deferred — do not fix

The duplicate `InputItems` overloads; `PullOne`'s log-only `prefab` parameter; the `RemainingCapacity`
restatement test.

## Scope and verification

Everything that bound Task 15a still binds: `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; `Logic/**`
free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`; **never write a `Container`'s inventory or a drawer's
ZDO `"Amount"`**; storage keyed by **prefab name**; `-1` (`ChestTake.Deferred`) never a count, never
summed, never retried; **items must never end up on the ground and nothing may be pulled that cannot be
delivered**; new files registered in the legacy csproj's explicit `<Compile>` items; no scratch or
inspection scripts in the tree; no Nexus artefacts; `MSB3274` expected and untouched; `fillAllModKey`
and the console command remain **Task 15b**; **do not push to any remote** (note: something in this
repository owner pushes commits in batches — commit locally and never run `git push`).

**R25:** assembly inspection goes through `tools\cecil.ps1` — never an ad-hoc probe. Its `-Definitions`
now reports member visibility precisely so this class of defect is checkable before review.
**R26:** report parameter types at call sites.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 205 or more)
3. Commit, then append a **Fix round 1** section to `task-15a-report.md`: each of the six now going
   through reflection with its cache and fail-closed behaviour; the `-Definitions` visibility check you
   ran to confirm; the `REFERENCES.md` correction; each bundled doc correction; your turret ammo-type
   conclusion and evidence; build and test lines.

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report
path. Do not narrate your work in the reply.
