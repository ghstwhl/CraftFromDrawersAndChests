# Task 16 — Cut the 1.0.0 release

## What was done (commit `08adb4d`, 2026-10-13)

- `ModInfo.Version` and `manifest.json`'s `version_number` bumped together to `1.0.0` — `Build.ps1`
  fails when they disagree.
- `CHANGELOG.md`: a `1.0.0` section describing the release.
- `dotnet test` before the bump: **254 passing, 0 failing**. (The plan's Task 16 Step 1 says "expected
  41" — stale: the suite has grown since the plan was written.)
- `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Release` →
  `BUILD OK - CraftFromDrawersAndChests 1.0.0 (Release)`;
  `Release\NorCal_Nerds-CraftFromDrawersAndChests\plugins\CraftFromDrawersAndChests.dll` (83,968 bytes)
  and `Release\CraftFromDrawersAndChests-1.0.0.zip` (61,784 bytes).

## Deliberate scope: 1.0.0 is the build the author validated

No code changed for this release, and that is **proven rather than asserted**: every method's IL was
compared between `0.1.13`'s shipped DLL (extracted from its zip) and `1.0.0`'s —
**340 methods, none added, none removed, exactly one whose IL differs**: `Plugin::LogPatchReport`, which
embeds the version string in its log line.

A raw byte diff is *useless* for this and worth remembering: it reports 14,607 differing bytes across 121
regions, because the shorter version string shifts every later metadata heap offset and the COFF
timestamp changes on every build. Compare IL, not bytes.

## Held for the first bug-fix release (1.0.1)

1. **The readout diagnostic still cannot see switched-off storage.** `Eligibility.IsEligible` rejects a
   toggled-off source as its first clause, so it is dropped before `LogReadout` runs. That is why the
   Dvergr-lantern investigation could not be answered from the log even though the answer was a flag on
   one drawer. The diagnostic should name nearby storage that was excluded and why.
2. **The toggle default key, and feedback for a flip** — see below.

## Finding: the shipped toggle default collides with Item Drawers (verified)

Unity's `KeyCode` constants, read from `UnityEngine.CoreModule.dll`:
`LeftShift = 304`, `LeftAlt = 308`, `W = 119`, `E = 101`.

- Item Drawers' `DrawerComponent.Interact` tests `Input.GetKey(304)` — that is **LeftShift** — while its
  own hover text advertises `Left Alt + $KEY_Use` for "withdraw single item". So the hover text is
  mislabeled, and its real hotkey is **LeftShift + E**, which is this mod's shipped `Toggle storage key`
  default (`left shift + e`). On a drawer the two fire on the same press.
- The owner asked for `w` as the new default. **`w` is forward movement (119)**, and the toggle is a
  guard-free postfix on `Player.Update` (`TogglePatches.PlayerUpdatePostfix`): it flips whatever storage
  is under the crosshair on any key-down. `w` would therefore silently disable storage while walking —
  the exact failure that cost this session's investigation, where the owner's toggle was on `s`, also
  walk-backwards. No guard rescues it: an "ignore while moving" rule would disable the key entirely.
- Recommended instead: a single **non-movement** key, and — more important than the key itself — **a HUD
  message when a storage is flipped**, so a toggle can never again be silent. That silent state change,
  not the key, is the root cause of the incident.

## Process note

The `0.1.12` and `0.1.13` changes (the station-feed verification fix and the held-count change to the
game's own `CountItems`) were made inside the live reported-bug loop and have **not** had the per-task
review round that Tasks 1–15 had. Their artifact checks are in place (IL/cecil verification, the three
call sites confirmed on the built DLL), but a fresh-eyes review of that diff is outstanding and offered.

## Nothing is published

Per the plan's Step 4, packaging and publishing are the author's, after their in-game pass over
`author-verify.md`. The outstanding checklist item is the two-player backpack check, which the owner
explicitly deferred to a bug-fix release if it fails.

## Publication (2026-10-13, owner-authorised)

The feature branch was merged into `main` with the `finishing-a-development-branch` skill's Option 1:
a fast-forward to `52429fa`, the suite re-run green on the merged result (255), and the branch deleted
(`-D`, because its *remote* counterpart was stale — git confirmed it was merged to HEAD).

**`main` had to be force-pushed, and the reason is worth recording.** `origin/main` was not this
project's history at all: it was an orphan commit created in GitHub's web UI (author
`Chris Knight <github@ghostwheel.kiwi>`, committer `GitHub <noreply@github.com>`, GPG-signed)
containing a 674-line `LICENSE` and a two-line `README.md`. Our history descends from the local
`main` baseline `2f9f3c3`, which was never pushed, so the two histories are unrelated and a plain
push was rejected as non-fast-forward. Before asking, the loss was measured rather than assumed:
that `LICENSE` is **byte-identical** to our `LICENSE.md` (675 lines each, compared in full), and its
README is a stub the real one supersedes — so replacing it loses nothing but a duplicate path.

`git push --force-with-lease origin main` (with `GIT_TERMINAL_PROMPT=0` so it could not hang on a
credential prompt) was authorised by the owner and succeeded. Verified from the server, not the local
ref: `git ls-remote origin refs/heads/main` reports `a71179c`, and `main --not --remotes` is 0. The
stale `origin/feature/craft-from-drawers-and-chests` branch remains on GitHub; deleting it is the
owner's call.

Left to the owner: create the GitHub release (tag `v1.0.1`, target `main`, asset
`Release/CraftFromDrawersAndChests-1.0.1.zip`) using the draft at `release-1.0.1-notes.md`, and publish
to Thunderstore, whose changelog tab comes from `CHANGELOG.md`.

### The tag was applied; the release upload could not be

`git tag -a v1.0.1` was created at `a71179c` with the tagger identity and the message "Craft From
Drawers And Chests 1.0.1 - first public release", and pushed. Verified from the server, not the local
ref: `git ls-remote --tags origin` reports `refs/tags/v1.0.1` = `5506f54` (the tag object) dereferencing
to `a71179c` = `main`.

**Creating the release itself was not possible from here, and that is an environment fact worth
recording rather than a decision:** `gh` is not installed, and neither `GITHUB_TOKEN` nor `GH_TOKEN`
exists in the environment. The release and its asset need the GitHub API with credentials, and a token
must not be routed through the assistant, so the two were prepared for a one-step owner action instead
of being attempted:

- the release **body**, clean of the draft file's metadata header, at `release-1.0.1-body.md`, for the
  web UI (Releases → Draft a new release → pick the existing `v1.0.1` tag → paste → attach the zip) or
  for `gh` (`gh release create v1.0.1 --draft --verify-tag --title "…" --notes-file <body> <zip>`,
  where `--verify-tag` makes it use the tag that is already pushed rather than creating one).

Only `CraftFromDrawersAndChests-1.0.1.zip` is a release asset. The other fifteen zips in `Release/` are
local build artefacts from the 0.1.x test builds and are gitignored, so they never enter the repository.

### Published to Thunderstore (owner, 2026-10-13)

<https://thunderstore.io/c/valheim/p/NorCal_Nerds/CraftFromDrawersAndChests/>

The path matches the identity the repo already declares — `ModInfo.ThunderstoreTeam` = `NorCal_Nerds`
and `ModInfo.ThunderstorePackage` = `CraftFromDrawersAndChests` — so the published listing is the package
`Build.ps1` produces, not a hand-made one. That URL is now in the README's intro and Installation section
(as the recommended route, with the manual steps kept) and in the GitHub release body.

`manifest.json`'s `website_url` deliberately still points at the GitHub repository: Thunderstore treats
that field as the project's home page and renders it as a "Website" link on the very page the URL would
then point back to. If the Thunderstore page should be linked from the repository side, the place for it
is the GitHub repository's own *Website* setting, which is a repo setting and not a file.

Hexium remains a listing rather than a second package format, per the plan's constraint.

### GitHub release published (owner, 2026-10-13) — verified against the public API

`https://api.github.com/repos/ghstwhl/CraftFromDrawersAndChests/releases` reports exactly one release:
`v1.0.1`, `draft=false`, published `2026-10-12T20:20:53Z`, with one asset,
`CraftFromDrawersAndChests-1.0.1.zip`, **62,407 bytes — byte-for-byte the local build's size**, so the
uploaded package is the one the code commit produces rather than a stale or hand-made archive. A public
listing also proves it is published rather than still a draft, which the unauthenticated API could not
have shown for a draft.

That completes the release: Thunderstore listing, GitHub release and the `v1.0.1` tag at `a71179c`. The
release points at the tag, which predates the two README-only commits made afterwards (`eeda7ac`, the
Thunderstore link; `127bed4`, the issue-tracker section), so those ride the next release — nothing to do.
