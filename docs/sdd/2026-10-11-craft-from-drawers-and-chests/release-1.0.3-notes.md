# GitHub release — 1.0.3

Tag: `v1.0.3`  ·  Target: `main`
Package code commit: `5cb4d5f` (`release: 1.0.3 - depend on BepInExPack_Valheim 5.4.2351`) — the tag is
taken from the tip of `main`, so it may carry a docs-only commit on top, as `v1.0.1` and `v1.0.2` did
Asset: `Release/CraftFromDrawersAndChests-1.0.3.zip` (64,012 bytes)
Title: **Craft From Drawers And Chests 1.0.3**

## What this version is

Packaging only — no behaviour change. The plugin binary is the same size as `1.0.2`'s (84,480 bytes)
because nothing in it changed: both pack versions ship the same assemblies (BepInEx 5.4.23.5, HarmonyX
2.9.0, MonoMod 22.1.29.1), verified by reading the manager's cached `5.4.2351` pack against
`.references\BepInEx\5.4.2350\` and against what the running profiles load. The version exists so that the
tag, the asset and the manifest all tell the same story, rather than rewriting the tagged `1.0.2`.

## Readiness at publication

- `dotnet test` 262/262; `tools/Build.ps1 -Configuration Release` BUILD OK 1.0.3; the packaged
  `manifest.json` carries `denikson-BepInExPack_Valheim-5.4.2351`.
- Carries the 1.0.2 fix, validated in game by the owner, single player, 2026-10-14: repeating the ctrl-click
  pull fetches the materials again each time.
- Still unconfirmed in play, on the checklist: a pull with the base nearly empty, and a pull with the
  inventory nearly full.

## Published

- **GitHub release published 2026-10-14** at the owner's instruction:
  <https://github.com/ghstwhl/CraftFromDrawersAndChests/releases/tag/v1.0.3> — verified from the public API:
  `draft = false`, `prerelease = false`, one asset `CraftFromDrawersAndChests-1.0.3.zip`, **64,012 bytes**,
  equal to the local build. The tag and `main` are both at `e2f384d`.
- Commits for this release: `5cb4d5f` (the packaging change) then `e2f384d` (these notes).

## Still to do

- **Thunderstore still serves 1.0.1.** Upload `CraftFromDrawersAndChests-1.0.3.zip` as 1.0.3. A Thunderstore
  version is immutable, so it has to be right the first time.
- Tell players on 1.0.1 that the server and every client must move to 1.0.3 together: the version check
  refuses a mismatch.
- The two unconfirmed behaviour checks from 1.0.2 stay on the checklist in `author-verify.md`.
