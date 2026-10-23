# References

Everything this repository compiles or runs against, plus how to regenerate the local
reference-assembly folder. None of the binary artifacts listed here are distributed with
this repository.

- **Mod version at time of writing:** 1.0.5
- **Target framework:** .NET Framework 4.6.2 (`net462`)
- **Game:** Valheim 1.0.20 (the version whose assemblies the build compiles against)
- **Last updated:** 2026-10-23

---

## 1. `.references` — the local reference-assembly folder

The product project (`CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj`) is a
legacy-style MSBuild project that references the game and framework assemblies by
`HintPath`, rooted at the MSBuild property `$(ReferencesRoot)`. That property defaults to
the **gitignored** folder `<repo>\.references` (see `Directory.Build.props`).

`.references` is **generated**, **machine-local**, and **never committed or shipped**: it
contains publicized copies of copyrighted Valheim assemblies and BepInEx's own binaries.
It is produced by [`tools/New-ValheimRefs.ps1`](tools/README.md) — run that script once on a
fresh checkout before building. `Directory.Build.props` only falls back to `.references`
when it already contains the assemblies, so an explicit `-ReferencesRoot` still wins.

### Layout it produces

```
.references\Valheim\<GameVersion>\
    assembly_valheim_publicized.dll
    assembly_utils_publicized.dll
    assembly_guiutils_publicized.dll
    assembly_postprocessing_publicized.dll
    Managed\                    (UnityEngine.dll, UnityEngine.*Module.dll, Unity.TextMeshPro.dll, ...)
.references\BepInEx\<BepInExVersion>\BepInEx\core\
    BepInEx.dll  BepInEx.Harmony.dll  0Harmony.dll
.references\Mods\
    MultiUserChest.dll
```

### How to regenerate it

```powershell
# From the repo root. Uses the cached publicizer and the installed Gale profile:
powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 `
    -PublicizerPath .\tools\.cache\publicizer\AssemblyPublicizer\AssemblyPublicizer.exe `
    -BepInExCorePath "C:\Users\<you>\AppData\Roaming\com.kesomannen.gale\valheim\profiles\<profile>\BepInEx\core" `
    -MultiUserChestDll "C:\...\profiles\<profile>\BepInEx\plugins\MSchmoecker-MultiUserChest\MultiUserChest.dll"
```

See [`tools/README.md`](tools/README.md) for every parameter. In short:

- `-ValheimPath` — auto-detected from Steam if omitted.
- `-BepInExCorePath` — folder with `BepInEx.dll`; defaults to `<ValheimPath>\BepInEx\core`.
  Point it at a mod-manager profile when BepInEx is not installed into the game folder.
- `-PublicizerPath` — a prebuilt `AssemblyPublicizer.exe` (the cached copy is under
  `tools\.cache`); otherwise `-BuildPublicizer` clones and builds it.
- `-MultiUserChestDll` — `MultiUserChest.dll` to stage at `.references\Mods\MultiUserChest.dll`.
  Defaults to a recursive search of the `<BepInExCorePath>` profile's `plugins` folder.

---

## 2. Game assemblies (the runtime API surface)

| Source | Location / version | Used for |
|---|---|---|
| Valheim game assemblies | `<Steam>\steamapps\common\Valheim\Valheim_Data\Managed\` — 1.0.20 | All gameplay API facts (`Container`, `ZNetView`, `ZDO`/`ZDOMan`, `Inventory`, `ZRoutedRpc`, `ZPackage`, `Piece`, `Fireplace`, `Smelter`, `CookingStation`, `ShieldGenerator`, …) |
| Unity engine modules | same `Managed\` folder (`UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.PhysicsModule.dll`, `UnityEngine.TextRenderingModule.dll`, `UnityEngine.AnimationModule.dll`, `UnityEngine.ParticleSystemModule.dll`, `UnityEngine.AssetBundleModule.dll`, `UnityEngine.UI.dll`, `Unity.TextMeshPro.dll`) | Unity types the mod touches directly; the csproj references these modules explicitly rather than the whole folder |

The four `assembly_*.dll` files are **publicized** at reference-build time (see §3). Publicizing
changes only the **compile-time** reference: it lets the build bind an `internal`/`private` member so
the code compiles. It does **not** make that member usable at runtime — the shipped game loads the
**unpublicized** assembly, which still does not expose it, so a non-public member still fails (or
throws out of a Harmony prefix on a vanilla method) when the JIT compiles the calling code. The mod
therefore reaches a member by reflection (`AccessTools`) unless it is `public` in the unpublicized
assembly — as `Patches/VanillaChestSource.cs`, `Patches/PullKeyPatches.cs` and
`Patches/StationPatches.cs` do. They are copyrighted game binaries and are **not** redistributed.

## 3. Modding framework, libraries and tooling

| Source | Version | Where it comes from | Used for |
|---|---|---|---|
| **BepInEx 5** | pack `5.4.2351` (pack `5.4.2350` also works) | `denikson-BepInExPack_Valheim`; copied into `.references\BepInEx\5.4.2350\BepInEx\core\` | Plugin base, `ConfigFile`/`ConfigEntry`, logging |
| **HarmonyX** | `2.9.0` (`0Harmony.dll`) | shipped with the BepInEx pack | Runtime patches |
| **Jötunn** | 2.30.2 | NuGet package `JotunnLib` 2.30.2 (`packages.config`); <https://www.nuget.org/packages/JotunnLib> | Mod framework: `SynchronizationManager` (server-synced config), `NetworkCompatibility`, config helpers |
| **MultiUserChest** | 0.6.2 | the installed mod set (`MSchmoecker-MultiUserChest`), staged into `.references\Mods\` | The only supported chest read/removal path (`ContainerHandler.RemoveItemFromChest`), so two players can use one chest at once |
| **KG Item Drawers** (`kg_ItemDrawers`) | 1.4.0 | installed mod set — **not** referenced at compile time | Reached by reflection at runtime; a **soft** dependency (the mod runs chest-only without it) |
| **Microsoft.NETFramework.ReferenceAssemblies** (+ `.net462`) | 1.0.3 | NuGet (`packages.config`) | Compiling against .NET Framework 4.6.2 without a machine-wide targeting pack |
| **AssemblyPublicizer** (CabbageCrow) — <https://github.com/CabbageCrow/AssemblyPublicizer> | cached at `tools\.cache\publicizer\AssemblyPublicizer\AssemblyPublicizer.exe` | Publicizing the four game assemblies; driven by `tools/New-ValheimRefs.ps1` |
| **Mono.Cecil** | ships with the Jotunn NuGet package and the publicizer | Reading IL/metadata when verifying API facts |
| **nuget.exe** | latest (auto-downloaded to `tools\.cache`) | `packages.config` restore in `tools/Build.ps1` |

**On the BepInEx version numbers.** `5.4.2350` and `5.4.2351` are Thunderstore *packaging* versions of
`denikson-BepInExPack_Valheim`, not BepInEx's own version. Both ship **BepInEx 5.4.23.5**, HarmonyX
**2.9.0** (`0Harmony.dll`) and MonoMod **22.1.29.1** — the assemblies the mod actually binds to — verified
by reading the assembly file versions of the manager's cached pack
(`…\gale\cache\denikson-BepInExPack_Valheim\5.4.2351\BepInEx\core\`) against the copies in
`.references\BepInEx\5.4.2350\` and against what the running profiles load. The reference folder keeps its
original name because its contents did not change; only the dependency string in `manifest.json` moved to
the current pack.
| **MSBuild** (Visual Studio Build Tools) | local toolchain | Compiling the legacy (non-SDK) csproj |

## 4. Referenced third-party mods

| Item | Relationship |
|---|---|
| **CraftFromContainers** (aedenthorn) — 4.0.7 | The behavioural baseline this mod ports; public domain. Declared a `[BepInIncompatibility]` and must be removed before installing. |
| **AzuCraftyBoxes** (Azumatt) — 1.8.27 | Reference for the storage abstraction and the toggle; MIT-0. Declared a `[BepInIncompatibility]` and must be removed before installing. |
| **MultiUserChest** (MSchmoecker) | A hard dependency; see §3. |

## 5. Licence notes

- **Valheim** and its assemblies are © Iron Gate Studio AB / Coffee Stain Publishing. They
  are used as compile-time references only and are **not** redistributed.
- **BepInEx**, **HarmonyX**, **Jötunn**, **MultiUserChest**, **Item Drawers**, **Mono.Cecil**
  and **AssemblyPublicizer** each remain under their own upstream licences.
- `tools/New-ValheimRefs.ps1` records that CraftFromDrawersAndChests is **not affiliated**
  with AssemblyPublicizer, and that only assemblies you are legally entitled to work with
  should be publicized.
