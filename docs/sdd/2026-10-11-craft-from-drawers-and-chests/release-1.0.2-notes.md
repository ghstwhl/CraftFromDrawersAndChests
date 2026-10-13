# GitHub release — 1.0.2

Tag: `v1.0.2`  ·  Target: `main`
Package code commit: `8af7cb6` (`fix: repeat pulls fetch the full cost again (1.0.2)`) — the tag is taken
from the tip of `main`, so it carries a docs-only commit on top, exactly as `v1.0.1` carried `a71179c`
Asset: `Release/CraftFromDrawersAndChests-1.0.2.zip`
Title: **Craft From Drawers And Chests 1.0.2**

## Readiness at publication

- **Validated in game by the owner on 1.0.2, single player (2026-10-14):** repeating the ctrl-click pull
  fetches the materials again each time — the reported defect. The other two 1.0.2 checks (a base that can
  only partly cover the cost; a nearly full inventory) were not reported on that pass and remain on the
  checklist in `author-verify.md`.
- Build and tests verified: `dotnet test` 262/262, `tools/Build.ps1 -Configuration Release` BUILD OK, and
  the three pull call sites confirmed on the built DLL by IL (`FullCost` + fill for the fetch; `Shortfall`
  for the station feed and the station fill key).

## Published

- **GitHub release published 2026-10-14** at the owner's instruction:
  <https://github.com/ghstwhl/CraftFromDrawersAndChests/releases/tag/v1.0.2> — verified from the public API:
  `draft = false`, `prerelease = false`, one asset `CraftFromDrawersAndChests-1.0.2.zip`, **63,698 bytes**,
  equal to the local build.
- Commits on `main` for this release: `8af7cb6` (the 1.0.2 code and documentation) then `589db30` (these
  notes). The tag comes from the tip of `main`, as `v1.0.1`'s did.

## Still to do

- **Thunderstore still serves 1.0.1.** Upload the same zip as 1.0.2. A Thunderstore version is immutable,
  so any later wording change rides a new version rather than an edit.
- Tell players on 1.0.1 that the server and every client must move to 1.0.2 together: the version check
  refuses a mismatch.
