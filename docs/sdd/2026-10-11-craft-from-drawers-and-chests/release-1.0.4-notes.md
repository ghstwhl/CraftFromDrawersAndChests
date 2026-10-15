# GitHub release — 1.0.4

Tag: `v1.0.4`  ·  Target: `main`
Package code commit: `ce8d041` (`release: 1.0.4 - built against Valheim 1.0.20 (not published)`)
Tag commit: `4ce6fa6` — taken from the tip of `main`, so it carries **four docs-only commits** on top of the
package commit (`e8e8fda`, `10ec917`, `c34e3b7`, `4ce6fa6`), the same shape as `v1.0.1`–`v1.0.3`.
Asset: `Release/CraftFromDrawersAndChests-1.0.4.zip` (64,425 bytes) — also copied to
`Release/CraftFromDrawersAndChests-release.zip`, which is byte-identical to it.
Title: **Craft From Drawers And Chests 1.0.4**

## What this version is

Compatibility with **Valheim 1.0.20**, with **no behavioural change**. The game update was an anti-cheat and
save-migration release: it renamed `ItemDrop.ResetVariantIfInvalid` → `EnsureValid`, retyped
`CharacterDrop.s_pseudoCounter` from `Dictionary<string,Tuple<float,int>>` to `Dictionary<int,int>`, added an
internal `CheatUtils` (`IsCheated`/`SetCheated`/…, over the pre-existing `ZDOVars.s_cheated` flag) and new
`Version/{Item,Player,World,PlayerData}` migration stamps. Of the **24** members this mod patches or reflects,
**none** changed — no signature, no access modifier, anywhere in the assembly — and all 22 patches are
Prefix/Postfix with **zero transpilers**, so body-level churn cannot break them. Nothing needed fixing, so
nothing in the mod was touched.

The plugin **is** different from 1.0.3's: it was recompiled against the 1.0.20 assemblies and stamped `1.0.4`
(all three of `1.0.2`, `1.0.3`, `1.0.4` have mutually distinct plugin binaries at 84,480 bytes). That matters
because this mod enforces a matching version between the server and every client —
**everyone has to move to 1.0.4 together**, or the connection is refused.

## Readiness at publication

- `dotnet test` **262/262**; `tools/Build.ps1 -Configuration Release` → `BUILD OK - CraftFromDrawersAndChests 1.0.4 (Release)`;
  the packaged `manifest.json` reads `1.0.4` and its `[BepInPlugin]` blob reads
  `NorCal_Nerds.CraftFromDrawersAndChests.CraftFromDrawersAndChests.1.0.4` with no stale `1.0.3`.
- **Confirmed in game on Valheim 1.0.20**, single player (host), 2026-10-16, from the session log in the
  `Modheim` profile (whose plugin DLL is hash-identical to the packaged one): the game itself reports
  `Valheim Version: 1.0.20`; the plugin loaded, all 22 Harmony patches applied, container tracking was live
  (`piece_chest_wood(Clone)` → tracked), and the session logged **0 errors** (12 warnings). Dependencies
  present: BepInExPack 5.4.2351, Jötunn 2.30.2, MultiUserChest 0.6.2, Adventure Backpacks 2.3.4.
- That session exercised the **load** path only — no feature appears in the log. The five functional checks
  on 1.0.20 are therefore **still open** (`author-verify.md`, "Valheim 1.0.20 update"): first load of a
  pre-update world under the new clean-slate migration, no cheat flag left behind, unchanged costs,
  client-side station feeding, and MultiUserChest as a non-host client.

## Published

- **GitHub release published 2026-10-16** at the owner's instruction:
  <https://github.com/ghstwhl/CraftFromDrawersAndChests/releases/tag/v1.0.4> — verified from the API:
  `draft = false`, `prerelease = false`, tag `v1.0.4`, one asset `CraftFromDrawersAndChests-1.0.4.zip`,
  **64,425 bytes**, `state = uploaded`, equal to the local build. The tag and `main` are both at `4ce6fa6`.
- Release notes: `release-1.0.4-body.md` (the GitHub body, passed with `--notes-file`).

## Still to do

- **Thunderstore**: state unknown — the web tools are unavailable in this environment, so this record could
  not check it. The 1.0.3 notes recorded Thunderstore still serving **1.0.1**, and 1.0.4 has not been uploaded
  as far as this record knows. If it is behind, upload `CraftFromDrawersAndChests-1.0.4.zip` as 1.0.4; a
  Thunderstore version is immutable, so it has to be right the first time.
- Run the five open checks above against the released build. Any failure goes to `/investigate`, not to a
  patch from a guess.
- Tell players that the server and every client must move to 1.0.4 together.
