# 1.0.11 — release record

**Published:** 2026-11-07 at the owner's instruction.
<https://github.com/ghstwhl/CraftFromDrawersAndChests/releases/tag/v1.0.11> — `draft=false`,
`prerelease=false`, resolved as `/releases/latest`.

## Artifact

| | |
|---|---|
| Asset | `CraftFromDrawersAndChests-1.0.11.zip` |
| Size | 70,857 bytes |
| SHA256 | `6efaaac3aee0c5507312f0befe63f3e2a81c10324984b58dcdf466fbdc2397a1` |
| Digest check | the release asset's `digest` equals the local zip's SHA256, so the published artifact **is** the tested build |
| Package contents | `manifest.json` reads `1.0.11`; DLL is the `-Configuration Release` build |

## Commits

| Commit | Subject |
|---|---|
| `d2c85d1` | `build: compile against Valheim 1.0.21` |
| `63ddaa4` | `feat: report storage that is switched off or locked as nearby but blocked` |
| `906acb5` | `release: 1.0.11 - Valheim 1.0.21 baseline, blocked-storage hint` |

Annotated tag `v1.0.11` → `906acb5` (tip of `main` at tag time). This notes commit rides on top,
the same shape as v1.0.4–v1.0.9.

## What 1.0.11 contains

1. **Valheim 1.0.21 baseline** — `.references/Valheim/1.0.21` regenerated; `ValheimGameVersion` and
   both script fallbacks flipped. Compatibility is an assembly diff, not an impression: 17 added /
   5 removed members (station and save-system internals), and **26 of 26** members this mod patches
   or reflects identical. Evidence: `game-1.0.21-compat-analysis.md`.
2. **Nearby-but-blocked hint** — a requirement whose shortfall sits in storage that is switched off
   or locked is tinted, marked and named in a one-off message. Pulls are untouched; the blocked list
   has its own cache slot and is never a pull source.
3. **Debug readout fix** — an unanswered requirement now lists switched-off storages with their
   contents and an `OFF` mark, which is the case that produced the original `SealBlubber` report.

## Verification

- `dotnet test` → **308 passed, 0 failed** (25 tests new for the hint and the block reason).
- `tools/Build.ps1 -Configuration Release` → `BUILD OK - CraftFromDrawersAndChests 1.0.11 (Release)`
  against `References: … (Valheim 1.0.21)`.
- Owner: 1.0.10 (the 1.0.21 baseline) confirmed in game on Valheim 1.0.21; 1.0.11 verified locally
  before publication.
- Release JSON re-read after publishing: title/tag/draft/prerelease as intended, asset digest equal
  to the local SHA256, `latest` = `v1.0.11`.

## Not done

- **Thunderstore was not uploaded** — same state as 1.0.4–1.0.9; the owner publishes there
  separately. Thunderstore's current version is unknown from here.
- 1.0.10 was never published; its content is folded into this release (the changelog keeps both
  entries).
