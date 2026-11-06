# Valheim 1.0.21 update — compatibility analysis (assembly-level)

**Question:** does the Valheim 1.0.21 update break this mod, and what has to change for the build to
target it?

**Answer:** nothing in the mod's API surface changed. The build baseline moves from 1.0.20 to 1.0.21
(`Directory.Build.props`), references are regenerated from the installed assemblies, and **no product
code change is needed for compatibility**. Every one of the 26 game members this mod patches or
reflects is byte-identical between 1.0.20 and 1.0.21.

---

## 1. How the version was established

The version label is not readable from any file on disk (no literal in `assembly_valheim.dll`,
`app.info` or `globalgamemanagers`). It comes from the game's own log, which is the same ground truth
used for the 1.0.20 record:

```
[Message: BepInEx] Valheim Version: 1.0.21
[Info : Unity Log] ... Valheim version: 1.0.21 (network version 41)
```

`%APPDATA%\com.kesomannen.gale\valheim\profiles\Modheim\BepInEx\LogOutput.log`, session of
2026-11-07 (BepInExPack 5.4.2360, kg.ItemDrawers 1.4.0, CraftFromDrawersAndChests 1.0.9).
The network version moved 40 → 41 versus the 1.0.16-era log, so the wire protocol changed with it —
which is exactly why the compatibility question is asked at the assembly level rather than assumed
away.

The update is real on disk, not a label drift:

| Assembly | Installed (1.0.21) | `.references/Valheim/1.0.20/Managed/` |
|---|---|---|
| `assembly_valheim.dll` | 2,577,920 B, SHA256 `9F959C0396EC4240…` | 2,575,360 B, SHA256 `94998885B103D6E5…` |
| `assembly_utils.dll` | 242,688 B, SHA256 `843946B3DF81FB0A…` | 242,688 B, SHA256 `771D59CBF3EF7040…` |

`assembly_utils.dll` is the same size with different bytes — a body/literal-only recompile, as in
1.0.20.

## 2. Method

The dumper written for the 1.0.20 analysis is reused unchanged
(`tools/dump-api.ps1`, hardened: `$ErrorActionPreference='Stop'`, Cecil types resolved before any
loop, output buffered and written once):

```powershell
$t = 'docs\sdd\2026-10-11-craft-from-drawers-and-chests\tools\dump-api.ps1'
powershell -NoProfile -ExecutionPolicy Bypass -File $t `
  -Assembly 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll' `
  -OutFile "$env:TEMP\cfdac-1021\new_valheim.txt"
powershell -NoProfile -ExecutionPolicy Bypass -File $t `
  -Assembly '.references\Valheim\1.0.20\Managed\assembly_valheim.dll' `
  -OutFile "$env:TEMP\cfdac-1021\old_valheim.txt"
```

Then `Compare-Object $old $new -CaseSensitive` (`-CaseSensitive` matters: PowerShell string
comparison is case-insensitive by default), filtered for compiler-generated noise:

```powershell
$delta = Compare-Object $old $new -CaseSensitive |
  Where-Object { $_.InputObject -notmatch '<>c|<>c__DisplayClass|g__|b__\d' }
```

Member counts: `assembly_valheim.dll` **26430 → 26440** lines, `assembly_utils.dll`
**2173 → 2174**.

The filter is necessary because the C# compiler renumbered every lambda and display class in
`InventoryGui`, `SaveCollection`, `SaveSystem`, `PlatformInitializer` and `SaveWithBackups`
(`b__41_*` → `b__42_*`, `<>c__DisplayClass43_0` → `44_0`, `83_0` → `84_0`, `14_0` → `18_0`). Those
are not API.

## 3. The delta — 22 real lines (17 added / 5 removed)

### Signature changes (all in members this mod does not touch)

| Member | 1.0.20 | 1.0.21 |
|---|---|---|
| `CookingStation.SetSlot` | `(Int32,String,Single,Status,Boolean)` | `(Int32,String,Single,Status,Boolean,Int64)` |
| `PlayerProfile.GetStat` | `(PlayerStatType)` | `(PlayerStatType,DifficultyRequirement)` (new overload) |
| `Ship.UpdateSail` | `(Single)` | `(Single,Boolean)` |
| `Ship.UpdateSailSize` | `(Single)` | `(Single,Boolean)` |
| `SaveSystem.InvalidateCache` | private static | public static |
| `StatusEffect.SetDuration` | compiler-controlled | real public method |
| `FileHelpers.ValidPath` (`assembly_utils`) | — | added |

### Additions

`WearNTear.m_supportWearNTear` (private `List<WearNTear>` field), `AchievementsGui.OnDisable`,
`AchievementsGui.ResetSelected`, `InventoryGui.OnReleasedOutside`,
`PlatformInitializer.RetroactivePathFix`, `SaveCollection.EnsureLocalLoaded` /
`GetAllFilesInSource` / `ReloadLocal` / `SourceByIndexAndEntryCount`,
`SaveSystem.ForceRefreshLocalCache`, `SaveWithBackups.ClearSaveFiles`.

Nothing here is a crafting, inventory, container or station member.

## 4. Touchpoints — 26 of 26 identical

Every member this mod binds (`[HarmonyPatch]` targets plus the reflected members), matched as
`^[MF]\|<Type>::<Member>\|` in both dumps and compared line-for-line including parameter types,
return type and attribute flags:

| # | Member | 1.0.20 vs 1.0.21 |
|---|---|---|
| 1 | `Player.HaveRequirementItems` | identical |
| 2 | `Player.HaveRequirements(Recipe,…)` | identical |
| 3 | `Player.HaveRequirements(Piece,RequirementMode)` | identical |
| 4 | `Player.ConsumeResources` | identical |
| 5 | `Player.UpdatePlacement` | identical |
| 6 | `Player.Update` | identical |
| 7 | `Player.m_placementStatus` (field, `AccessTools.Field`) | identical |
| 8 | `InventoryGui.OnCraftPressed` | identical |
| 9 | `InventoryGui.SetupRequirement` | identical |
| 10 | `InventoryGui.Update` | identical |
| 11 | `InventoryGui.UpdateCraftingPanel` (`AccessTools.Method`) | identical |
| 12 | `Fireplace.Interact` | identical |
| 13 | `Fireplace.GetHoverText` | identical |
| 14 | `Smelter.OnAddOre` | identical |
| 15 | `Smelter.OnAddFuel` | identical |
| 16 | `Smelter.OnHoverAddFuel` | identical |
| 17 | `Smelter.OnHoverAddOre` | identical |
| 18 | `ShieldGenerator.OnAddFuel` | identical |
| 19 | `ShieldGenerator.OnHoverAddFuel` | identical |
| 20 | `CookingStation.OnAddFuelSwitch` | identical |
| 21 | `CookingStation.OnInteract` | identical |
| 22 | `Turret.UseItem` | identical |
| 23 | `Terminal.InputText` | identical |
| 24 | `Container.Awake` | identical |
| 25 | `Container.GetHoverText` | identical |
| 26 | `Container.OnDestroyed` | identical |
| 27 | `Container.CheckAccess` (`AccessTools.Method`) | identical |

(The table has 27 rows, not 26: `Player.HaveRequirements` contributes one row per overload, and the
comparison keys `Type::Member` plus every signature, so both were checked.)

`CookingStation.SetSlot` **did** change signature, and it is called from inside the patched
`CookingStation.OnInteract`. That is harmless here and would not be for a transpiler: every patch in
this mod is a `[HarmonyPrefix]`/`[HarmonyPostfix]`, so a changed callee signature cannot alter what
the patch sees. There are **zero transpilers** in the codebase.

The one runtime-resolved target, the Item Drawers `DrawerComponent.GetHoverText`, lives in
`kg_ItemDrawers.dll` (not a game assembly) and is already `[HarmonyPrepare]`-guarded.

## 5. Verdict

- **Compatibility:** no code change required. The build moves to the 1.0.21 reference assemblies and
  the resulting DLL binds the same members it did on 1.0.20.
- **Evidence limits:** this is an API-level proof. Behaviour that depends on asset data (recipes,
  item prefabs, station conversions) is outside an API dump; the in-game checklist in
  `author-verify.md` remains the functional gate.
- **Not yet observed in game:** this build has not been run. The log line to confirm is
  `Harmony patched 24 game method(s)` with `Valheim Version: 1.0.21`.

## 6. Re-running the analysis

1. Regenerate references from the install (the folder is gitignored):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 -GameVersion 1.0.21 `
  -PublicizerPath .\tools\.cache\publicizer\AssemblyPublicizer\AssemblyPublicizer.exe `
  -BepInExCorePath <folder with BepInEx.dll> `
  -MultiUserChestDll <path to MultiUserChest.dll>
```

2. Dump both assemblies with `tools/dump-api.ps1` and diff with `Compare-Object … -CaseSensitive`,
   filtering `<>c|<>c__DisplayClass|g__|b__\d`.
3. Mark the members above and confirm **0 differences**.

A publicized-assembly sanity check is also part of the ritual: dumping
`.references\Valheim\1.0.21\assembly_valheim_publicized.dll` and matching the same 27 rows against
`^[MF]\|<Type>::<Member>\|` finds all of them (access modifiers are expected to read `public` there
and must not be compared).
