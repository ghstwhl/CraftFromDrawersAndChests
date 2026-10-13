# GitHub release draft — 1.0.2

Tag: `v1.0.2`  ·  Target: `main`
Package code commit: `8af7cb6` (`fix: repeat pulls fetch the full cost again (1.0.2)`) — the tag is taken
from the tip of `main`, so it may carry a docs-only commit on top, exactly as `v1.0.1` carried `a71179c`
Asset: `Release/CraftFromDrawersAndChests-1.0.2.zip`
Title: **Craft From Drawers And Chests 1.0.2**

## Ready to publish

- **Validated in game by the owner on 1.0.2, single player (2026-10-14):** repeating the ctrl-click pull
  fetches the materials again each time — the reported defect. The other two 1.0.2 checks (a base that can
  only partly cover the cost; a nearly full inventory) were not reported on that pass and remain on the
  checklist in `author-verify.md`.
- Build and tests verified: `dotnet test` 262/262, `tools/Build.ps1 -Configuration Release` BUILD OK, and
  the three pull call sites confirmed on the built DLL by IL (`FullCost` + fill for the fetch; `Shortfall`
  for the station feed and the station fill key).

## Publishing checklist

1. Confirm the zip attached here is `CraftFromDrawersAndChests-1.0.2.zip` and is the freshly built one.
2. Publish the release (the tag is created from `main` by `gh release create --target main`).
3. Upload the same zip to Thunderstore as 1.0.2. Thunderstore versions are immutable, so any later
   wording change to the README rides a new version rather than an edit.
4. Tell players already running 1.0.1 that both sides — server and every client — must move to 1.0.2
   together, because the version check refuses a mismatch.
