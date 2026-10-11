# Changelog

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.2] - 2026-10-12

### Fixed

- **The mod did nothing at all in game, for a second reason.** `Plugin.Awake` called Harmony's
  no-argument `Harmony.PatchAll()`, which does not patch the calling assembly — it *infers* one with
  `new StackTrace().GetFrame(1).GetMethod().ReflectedType.Assembly`. When that inference does not
  resolve to this mod's assembly, Harmony patches the wrong assembly instead: it finds no
  `[HarmonyPatch]` classes there, applies **zero patches**, throws nothing and logs nothing. The
  assembly is now passed explicitly (`Harmony.PatchAll(typeof(Plugin).Assembly)`), so the inference is
  gone. This is why 0.1.1 could fix the config fault and still do nothing in game.

### Added

- A permanent start-up line reporting how many game methods this mod's Harmony instance patched (and
  what the old inference would have chosen), with an error when the count is zero. 0.1.1 loaded
  cleanly, threw nothing and patched nothing, and nothing in the log could tell that apart from a mod
  that works and simply finds no storage nearby — which cost two test cycles to discover.
- A one-shot breadcrumb when the container-tracking patch first runs, naming the first container and
  whether it was tracked, and a one-shot breadcrumb in the console hook naming the first console line
  it sees.

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
