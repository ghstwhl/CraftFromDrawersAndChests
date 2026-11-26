# tools

Helper scripts for building CraftFromDrawersAndChests.

## `Build.ps1` — build, validate, and package (start here)

One command for the whole pipeline: **NuGet restore → validation → MSBuild →
Release packaging (+ zip)**. Runs from any working directory and exits non-zero on failure
(CI/agent friendly).

```powershell
# Release build + package + zip (default)
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1

# Debug build (compiles only, no packaging)
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug

# Skip steps you know aren't needed
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -SkipRestore -SkipValidation
```

### What it does

1. **Restore** — `nuget restore` for `packages.config` (auto-downloads `nuget.exe` to
   `tools\.cache` if it isn't on `PATH`).
2. **Validate** — fails the build when:
   - `ModInfo.Version` isn't `MAJOR.MINOR.PATCH`, or
   - it doesn't match `manifest.json`, or
   - any `Translations\**\*.json` file is invalid JSON, or
   - the reference assemblies aren't present at `-ReferencesRoot` (default `<repo>\.references`).
3. **Build** — MSBuild; produces the single mod DLL directly - no assembly merging step.
4. **Package** (Release) — assembles the Thunderstore-style folder and two identical zips:
   `CraftFromDrawersAndChests-release.zip` (the stable "latest" name) and `CraftFromDrawersAndChests-<version>.zip`
   (a copy named after the version being built).

### Parameters

| Parameter | Default | Purpose |
|---|---|---|
| `-Configuration` | `Release` | `Release` (packages) or `Debug` (builds only). |
| `-ReferencesRoot` | `<repo>\.references` | Folder with the publicized reference assemblies. |
| `-SkipRestore` | off | Skip the NuGet restore step. |
| `-SkipValidation` | off | Skip the validation checks. |
| `-NoPackage` | off | Build the release folder but don't create the zips. |

### Outputs

```
CraftFromDrawersAndChests\bin\<Configuration>\CraftFromDrawersAndChests.dll    the mod DLL
Release\NorCal_Nerds-CraftFromDrawersAndChests\                                package (plugins\ + docs), named from ModInfo.cs
Release\CraftFromDrawersAndChests-release.zip                                  Release only
Release\CraftFromDrawersAndChests-<version>.zip                                Release only (copy of the above)
```

Deploy the package's `plugins` contents to `BepInEx\plugins\` on **both** the server and the
client (same build on each side).

### Exit codes

`0` = success, non-zero = failure. The last line is always `BUILD OK ...` or `BUILD FAILED: ...`.

## Documentation

There is no generated-documentation or template build step in this repository. All docs
are plain, hand-maintained Markdown: the repository `README.md`, this `tools/README.md`,
and the design and plan notes under `docs/superpowers/`.

## `New-ValheimRefs.ps1`

Creates the reference-assembly layout that `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj`
compiles against (the `$(ReferencesRoot)` folder), using
[CabbageCrow/AssemblyPublicizer](https://github.com/CabbageCrow/AssemblyPublicizer)
to publicize the Valheim game assemblies.

It produces exactly what the csproj expects:

```
<OutputPath>\Valheim\<GameVersion>\
    assembly_valheim_publicized.dll
    assembly_utils_publicized.dll
    assembly_guiutils_publicized.dll
    assembly_postprocessing_publicized.dll
    Managed\                (UnityEngine*.dll, Unity.TextMeshPro.dll, ...)
<OutputPath>\BepInEx\<BepInExVersion>\BepInEx\core\
    BepInEx.dll  BepInEx.Harmony.dll  0Harmony.dll
<OutputPath>\Mods\
    MultiUserChest.dll
```

### Prerequisites

- **Valheim** installed (Steam) or `-ValheimPath` provided.
- **BepInEx 5.4.2350** installed into the Valheim folder, or `-BepInExCorePath`
  pointing at a folder containing `BepInEx.dll`.
- **MultiUserChest** installed in the same mod set, or `-MultiUserChestDll`
  pointing at its `MultiUserChest.dll`. It is staged at `<OutputPath>\Mods\MultiUserChest.dll`,
  which `CraftFromDrawersAndChests.csproj` references; a mod-manager profile's copy under
  `<BepInExCorePath>\..\plugins` is found automatically.
- **AssemblyPublicizer.exe** — either pass `-PublicizerPath`, or use
  `-BuildPublicizer` (requires Visual Studio / MSBuild, which you need anyway).

### Usage

```powershell
# From the repo root. Builds the publicizer from source if needed.
powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 -BuildPublicizer

# Use a downloaded release binary (preferred) and a specific Valheim install:
powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 `
    -ValheimPath "D:\Steam\steamapps\common\Valheim" `
    -PublicizerPath "C:\tools\AssemblyPublicizer.exe"

# Write somewhere other than the repo's .references and match a different game version:
powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 `
    -OutputPath "C:\ValheimRefs" -GameVersion 0.217.46
```

Default output is `<repo>\.references` (gitignored).

### After running

Point the project at the generated folder by setting `ReferencesRoot`, e.g. add a
`Directory.Build.props` at the repo root:

```xml
<Project>
  <PropertyGroup>
    <ReferencesRoot>C:\ValheimRefs</ReferencesRoot>
  </PropertyGroup>
</Project>
```

Then build in Visual Studio, or:

```powershell
msbuild CraftFromDrawersAndChests.sln /p:ReferencesRoot="C:\ValheimRefs"
```

> **Note:** If you change `-GameVersion` / `-BepInExVersion`, keep them in sync with
> `VALHEIM_INSTALL` / `BEPINEX_PATH` (or `ReferencesRoot`) in the csproj.

## Forced-debug log-capture build (temporary, never released)

`PluginConfig.ForceDebugForDiagnostics` is compiled in only when the `FORCED_DEBUG` symbol is
defined, so a normal build can never contain it. The reason it exists: every config entry is
`IsAdminOnly`, so a non-admin player cannot turn `Debug` on at all, and Jötunn pushes the server's
value into the entry — a log-capture session needs debug on both clients regardless.

`Build.ps1` does not forward extra MSBuild properties, so build the variant directly:

```powershell
$mb = 'C:\Program Files\Microsoft Visual Studio\<ver>\Community\MSBuild\Current\Bin\MSBuild.exe'
& $mb 'CraftFromDrawersAndChests\CraftFromDrawersAndChests.csproj' `
    /p:Configuration=Release '/p:DefineConstants=TRACE%3BFORCED_DEBUG' `
    /p:DevPluginsFolder=C:\nonexistent-devplugins /nologo /v:minimal
```

The semicolon in the symbol list **must** be escaped as `%3B`, and setting
`$env:DefineConstants` does **not** work (the csproj's own `<DefineConstants>` wins) — both cost a
build to find out. Then copy `bin\Release\CraftFromDrawersAndChests.dll` into
`Release\NorCal_Nerds-CraftFromDrawersAndChests\plugins\`, zip that folder under a distinct name,
and re-run `Build.ps1 -Configuration Release` afterwards so the Release folder is not left holding a
forced-debug DLL.

Verify the **artifact**, not the source:

- the banner literal `FORCED-DEBUG TEST BUILD` is in the DLL — scan the UTF-16 `#US` heap at byte
  offset 1 as well as 0, or a literal at an odd offset reads as absent;
- `cecil.ps1 -Type PluginConfig -Method .cctor -IL` shows `ldc.i4.1` + `stsfld
  ForceDebugForDiagnostics` (a normal build has no initialiser there at all).

Keep the version unchanged: `[NetworkCompatibility(..., VersionStrictness.Patch)]` disconnects any
client whose build differs, so a log-capture build must carry the same version as the server.

### Legal

Reference assemblies are derived from the game and tooling binaries and are **not**
distributed with this repo. Only publicize assemblies you are entitled to work with.
CraftFromDrawersAndChests is not affiliated with AssemblyPublicizer.
