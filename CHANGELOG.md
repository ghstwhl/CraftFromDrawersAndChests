# Changelog

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2026-10-12

### Fixed

- **The mod did nothing at all in game.** `PluginConfig` registered its own config file with Jötunn's
  `SynchronizationManager.RegisterCustomConfig`, which always throws for a plugin's default mod config
  ("already synchronized"). The exception aborted `Awake` *before* `Harmony.PatchAll()`, so **no patches
  were applied** — no chest pulling, no drawer pulling and no per-storage switch — and BepInEx still
  reported the plugin as loaded, so nothing in the log marked it as a failure.
- Config initialisation is now guarded: a failure there can no longer prevent patching, and it logs an
  error and disables the mod explicitly rather than leaving it silently inert.
- Declared `[SynchronizationMode(AdminOnlyStrictness.Always)]` so the server-authoritative settings are
  enforced as intended.

### Notes

- The server and every client must run the **same patch version**
  (`NetworkCompatibility(EveryoneMustHaveMod, VersionStrictness.Patch)`), so update both sides together
  or the join is refused by design.

## [0.1.0] - 2026-10-11

### Added

- Initial release: mod identity and Thunderstore packaging.
