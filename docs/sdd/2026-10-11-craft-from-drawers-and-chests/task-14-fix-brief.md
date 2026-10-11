# Task 14 — fix round 1

No Critical. Two Importants, one bundled Minor, and one parity item the review surfaced.

The reference mod's source **is available** (`BepInExPlugin.cs` under `.references`), and the reviewer
used it as the oracle. For every behavioural question below, CraftFromContainers' actual code is the
authority, not this brief's paraphrase — the brief that produced this task already got the flash wrong
(CFC has no world-object flash; its only flash is the requirement readout, which is what was built).

## Important #1 — `resourceString` is bound, documented and never read

`PluginConfig` binds `ConfigModel.ResourceString` (`"Resource string"`), copies it into
`FeatureFlags.ResourceString`, and **nothing consumes it** — it appears only in `ConfigModel.cs`,
`FeatureFlags.cs` and `PluginConfig.cs`. The plan's own self-review assigns `resourceString` to this
task, so no later task will pick it up.

CFC uses it in the very `SetupRequirement` postfix this task ports: after the colour logic it sets
`text.text = string.Format(resourceString.Value, invAmount, amount)`. Our postfix writes only
`text.color` and never touches `text.text`, so a retained, documented setting is inert.

**R11 — wire it up**: set `text.text` from `FeatureFlags.ResourceString` exactly as CFC does, guarding
null/empty so it falls back to the plain amount rather than throwing on a malformed format string.

## Important #2 — the prevent key is honoured in one place, CFC honours it in several

The reviewer confirmed the port is faithful: CFC's
`AllowByKey() { if (CheckKeyHeld(preventModKey)) return switchPrevent; return !switchPrevent; }`, and
`Logic/PullKeyRules.AllowByKey` is an exact port. But CFC applies `AllowByKey()` across the
**requirement checks and consumption** as well as pulls, which is *why* CFC needs its crafting-panel
refresh patch at all — holding the prevent key changes which recipes the panel shows. Ours applies it
only to the pull and the readout flash, so holding the key currently changes nothing about
availability, and that plan row was omitted as "inert".

**R12 — make the prevent key mean what it means in CFC**: while it is held (in its configured
`SwitchPrevent` sense), the storage-aware availability gates must stop substituting storage — the
player sees vanilla availability, and a craft they do trigger is paid for from their own inventory
with no pull. Then add the panel refresh if that is what makes the change visible, mirroring CFC's
`InventoryGui.Update` → `UpdateCraftingPanel`.

**The safety direction is not negotiable here.** Task 13 spent five review rounds establishing that a
"yes" from a gate must mean the pull can actually be delivered. When storage is excluded, a "yes" may
only come from vanilla's own inventory check, and the pull must be skipped. There must be **no** state
in which the gate counted storage while the pull was suppressed — that would be a free craft and is
Critical. Verify that both directions of the key are safe and say in your report how.

This touches `CraftPatches.cs`, which has already been through five review rounds. Keep the change to
the minimum that achieves the behaviour, and say precisely what you altered.

## Bundled Minor — the key check does not normalise case

`PullKeyPatches.KeyHeld` passes the configured string straight into `Input.GetKey` inside a try/catch,
so a user who types `"Left Ctrl"` rather than `"left ctrl"` gets a silently dead key. CFC calls
`Input.GetKey(value.ToLower())`. Lower-case the configured value after the `IsKeyConfigured` guard.

## Deferred (do not fix)

The flash availability does not account for `LeaveOne` or for two requirements competing for the same
stack, so the readout can over-promise; and `PullToPlayer` plans internally while `FailureReason`
plans again on the refusal path (the brief pins the `bool` signature, so this is tolerated).

## Not this round — the controller is scheduling it separately

CFC also lets you pull a **building piece's** materials with the same key (`UpdatePlacement_Patch`,
which pulls `selectedPiece.m_resources` on attack while the key is held), and its own `pullItemsKey`
description covers "crafting **or building**". The Task 14 brief scoped the key to recipes, which is
what was built, but **no task in the plan implements the build pull** — so a retained CFC behaviour is
currently dropped. That is a plan gap, not a defect of this task. It is being scheduled; do not
implement it here.

## Scope and verification

Everything that bound Task 14 still binds: `net462`; BepInEx `5.4.2350`; Jötunn `2.30.2`; `Logic/**`
free of `UnityEngine`/`Valheim`/`BepInEx`/`Jötunn`; **never write a `Container`'s inventory or a
drawer's ZDO `"Amount"`** (chest removal only via `MultiUserChestBridge.RemoveFromChest`, drawer only
via `ForceRemove`); storage keyed by **prefab name**; `-1` (`ChestTake.Deferred`) never a count, never
summed, never retried; items must never end up on the ground; new files registered in the legacy
csproj's explicit `<Compile>` items; no scratch or inspection scripts in the tree; no Nexus artefacts;
no other mod's identity anywhere in this repository; `MSB3274` expected and untouched; no
station-feeding; no console command; **do not push to any remote**.

1. `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`
2. `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` → 0 failing (expect 152 or more; add tests for anything pure)
3. Commit, then append a **Fix round 1** section to `task-14-report.md` covering: how `resourceString`
   is consumed and what happens with a malformed one; exactly how the prevent key now affects
   availability, which call sites you touched, and why neither direction can produce a free craft;
   the case-normalisation fix; build and test lines; and any CFC behaviour you found that this project
   still does not have.

Then reply with ONLY (under 12 lines): **Status** · commit · build/test one-liner · concerns · report
path. Do not narrate your work in the reply.
