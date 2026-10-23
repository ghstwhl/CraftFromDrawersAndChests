# GitHub release — 1.0.9

Tag: `v1.0.9` (annotated, object `8f8ef2f`) · Target: `main` · Peeled to commit `8e829f2`
Tag commit = package commit: this release has **no docs-only commits** on top of it (this record is the
first commit after the tag).
Asset: `Release/CraftFromDrawersAndChests-1.0.9.zip` (67,749 bytes,
`sha256:bee951988835f3a9bf17481184a86c1c0a4738eb6da647bd1567e7dc77173b0c`) — also copied to
`Release/CraftFromDrawersAndChests-release.zip`, byte-identical.
Title: **Craft From Drawers And Chests 1.0.9**

## What this version is

Everything since **1.0.5**, the last published release. Four changes, two of them fixes for reports from play
and one a 1.0.6 regression fixed in 1.0.7:

| Version | Change |
|---|---|
| 1.0.6 | The fill-all key feeds a station from the input **already in your inventory** (reported against a blast furnace holding flametal). |
| 1.0.7 | **Fixes a 1.0.6 regression:** the smelter's walk over its accepted ores stopped at the first ore storage did not hold, so tin, scrap iron, silver and scrap copper were never pulled from chests *or* drawers. |
| 1.0.8 | A HUD line reports what a feed added: `Added 10 Scrap iron, 3 Tin`. |
| 1.0.9 | That line lists **every** ore type however many the smelter converts (reported: four types must all appear). |

Plus nothing else: the shield generator work shipped in **1.0.5**, so this package contains only the four
rows above on top of it.

**A caveat worth recording:** 1.0.6, 1.0.7 and 1.0.8 were pushed but never published, and 1.0.6 shipped a
regression (only the first conversion of a smelter could be fed from storage, fixed in 1.0.7). No player ever
received 1.0.6 from a release; this 1.0.9 package is the first artifact containing any of it.

## Readiness at publication

- `dotnet test` **277/277**; `tools/Build.ps1` Debug and Release both `BUILD OK - CraftFromDrawersAndChests 1.0.9`
  (MSB3274 expected). The packaged plugin DLL is hash-identical to
  `CraftFromDrawersAndChests\bin\Release\CraftFromDrawersAndChests.dll`.
- Packaged `manifest.json` reads `1.0.9`; the DLL's ASCII scan contains `OrderFed`/`FeedSummary`/`ShowFedOres`
  and the `1.0.9` attribute string, and no stale `1.0.8`. (String *literals* such as `"Added "` live in the
  `#US` heap as UTF-16 at arbitrary byte offsets — a scan must try both alignments. Enum members are
  compile-time constants, so `TopLeft` legitimately does not appear in the DLL at all.)
- **Confirmed in game by the owner on 2026-10-24**, Valheim **1.0.20** (`Modheim` profile, log at 08:35):
  `Loading [CraftFromDrawersAndChests 1.0.9]`, `Harmony patched 24 game method(s)` (the list includes both
  `ShieldGenerator.*` targets), and **no** `[Error` and no feed diagnostics in the session. The owner then
  instructed this release.
- Checks the 1.0.6–1.0.9 sections of `author-verify.md` describe (multi-ore from a drawer, four ore types on
  one line, `Pull fuel` off, non-English names) are not individually recorded in the log; they remain the
  owner's pass over this build.

## Published

- **GitHub release published 2026-10-24** at the owner's instruction:
  <https://github.com/ghstwhl/CraftFromDrawersAndChests/releases/tag/v1.0.9> — verified from the public API:
  `draft = false`, `prerelease = false`, `tag_name = v1.0.9`, `target_commitish = main`, one asset
  `CraftFromDrawersAndChests-1.0.9.zip`, **67,749 bytes**, `state = uploaded`, `digest =
  sha256:bee95198…` equal to the local zip, and `/releases/latest` resolves to `v1.0.9`.
- Release notes: `release-1.0.9-body.md` (the GitHub body, passed with `--notes-file`).

## Still to do

- **Thunderstore**: state unknown — the web tools are unavailable here, so this record cannot check it. The
  1.0.3 notes recorded Thunderstore still serving **1.0.1**; 1.0.4 through 1.0.9 were never uploaded as far as
  this record knows. A Thunderstore version is immutable: upload `CraftFromDrawersAndChests-1.0.9.zip` once the
  version number is certain.
- Run the remaining 1.0.6–1.0.9 rows of `author-verify.md` against the released build. Any failure goes to
  `/investigate`, not to a patch from a guess.
- Tell players the server and every client must move to **1.0.9** together.
