# Valheim 1.0.20 update — compatibility analysis (assembly-level)

**Date:** 2026-10-16
**Question:** other developers report the Valheim 1.0.20 update broke their mods. Does it break this one?
**Answer:** **No.** No member this mod patches or reflects changed. No code change was required.

Everything below is read from a primary artifact — the shipped assemblies, their hashes, their Cecil
metadata, the build, and the test run. No web source was used (the web tools in this environment are
non-functional; see *Version label*).

Scripts referenced here live beside this file: `tools/dump-api.ps1` (hardened API dumper) and
`tools/cecil.ps1` (the sanctioned member/IL/caller probe). Both must be run in a **child** `powershell`
process: `AssemblyDefinition.ReadAssembly` holds the file stream open, and a locked build output fails the
next build with MSB3026/MSB3027.

---

## 1. Trigger — the update is real

| Evidence | Value |
|---|---|
| `steamapps\appmanifest_892970.acf` | `buildid` **25909357**, `LastUpdated` 1792087004 |
| Last write time of `valheim_Data\Managed\assembly_valheim.dll` | **2026-10-16 06:56:42** |
| Same for `assembly_utils.dll`, `Assembly-CSharp.dll` | **2026-10-16 06:56:42** |
| Unchanged in that update | `valheim.exe` (2026-10-07 18:14:46), PlayFabParty/gui_framework/assembly_lux/… |

`AssemblyVersion` on all four `assembly_*.dll` files is the uninformative Unity `0.0.0.0`, so identity has
to be established by hash. Installed (`Steam\…\valheim_Data\Managed\`) versus the baseline this mod was
verified against (`.references\Valheim\1.0.16\Managed\`):

| Assembly | Installed SHA256 | Baseline SHA256 (refs 1.0.16) | |
|---|---|---|---|
| `assembly_valheim.dll` | `94998885B103D6E5F5480123EAEE0A093E82AF2DDEEEE37536C15BF555F24F3A` | `25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE` | **DIFFERENT** |
| `assembly_utils.dll` | `771D59CBF3EF704007267E8EAF89AAC231C172B052BD93F9C273A18381BD6D59` | `6BE2AFEB97B5B26FD3176E1B8584C07E57AD48DDCD6E8E5E79E32BEE943827C1` | **DIFFERENT** |
| `assembly_guiutils.dll` | `52846FCAE0C535D733711C13FDB8F214D303603B5DBF23D05B1ED6A8E475C9DD` | *(identical)* | SAME |
| `assembly_postprocessing.dll` | `67F6A99278A629261D07145826831364905CC4A8DAED610617C4B9BCE0A4FB48` | *(identical)* | SAME |

The two assemblies this mod actually binds changed; the two it does not are bit-identical.

## 2. Version label — 1.0.20 is the owner's reading, not a locally verified fact

The version string is **not** recoverable from local artifacts:

- not in the assemblies — neither as ASCII nor as UTF-16, and `AssemblyVersion` is `0.0.0.0`;
- not in `valheim_Data\app.info` (publisher `IronGate`, product `Valheim` only);
- not in `globalgamemanagers` or `boot.config`;
- not in the Steam ACF (it records a *build* id, not a marketing version).

The only local record of the game's own version is the **pre-update** log
(`%APPDATA%\com.kesomannen.gale\valheim\profiles\Modheim-interim\BepInEx\LogOutput.log`, written 2026-10-13):

```
[Message:   BepInEx] Valheim Version: 1.0.17
[Info   : Unity Log] 10/13/2026 08:04:40: Valheim version: 1.0.17 (network version 40)
```

Two consequences worth keeping:

1. The repo's `Directory.Build.props` said `1.0.16` while the game was already **1.0.17** on Oct 13 — the
   label had drifted one version *before* this update. Regenerating the refs must therefore be verified by
   **hash**, never by the folder name (see §6).
2. **1.0.20 is the owner's stated version.** It is consistent with everything measurable here (a second
   update landed 2026-10-16) but was not independently confirmed. Confirm it by reading the
   `Valheim version:` line in `LogOutput.log` after the next game launch.

## 3. The managed API delta

Full member dump of old versus new (`dump-api.ps1` + `Compare-Object -CaseSensitive`; the `-CaseSensitive`
switch is required, PowerShell string comparison is case-insensitive by default and would hide a case-only
rename):

- `assembly_valheim.dll`: **6 removed / 21 added** (26,415 → 26,430 member lines)
- `assembly_utils.dll`: **0 removed / 0 added** (2,173 lines; bytes differ, so it was recompiled with an
  identical API — a body or literal-only change)

**Removed:**

```
F|<PrivateImplementationDetails>::6181044DC9508299B5001304F0ADE90AB15A61DE4D8D6BFB28D15D08C3338DE1|<PrivateImplementationDetails>/__StaticArrayInitTypeSize=26455|Assembly, Static, InitOnly, HasFieldRVA
F|<PrivateImplementationDetails>::D268BA71708C74B15C2E0CD8B1FABD75EC4E3D2BC0FCFBE26D31E2EB996056B8|<PrivateImplementationDetails>/__StaticArrayInitTypeSize=18400|Assembly, Static, InitOnly, HasFieldRVA
F|CharacterDrop::s_pseudoCounter|System.Collections.Generic.Dictionary`2<System.String,System.Tuple`2<System.Single,System.Int32>>|Public, Static
M|ItemDrop::ResetVariantIfInvalid|()|System.Void|CompilerControlled, Public, HideBySig
TYPE|<PrivateImplementationDetails>/__StaticArrayInitTypeSize=18400|…
TYPE|<PrivateImplementationDetails>/__StaticArrayInitTypeSize=26455|…
```

**Added:**

```
F|<PrivateImplementationDetails>::B08D27E83FB3AAF60F95833482F46FED7539AD31C9758D2EB9BE9D9B5ED5C307|<PrivateImplementationDetails>/__StaticArrayInitTypeSize=18416|…
F|<PrivateImplementationDetails>::E75FB91DB791394B404D129E76DB2DB2C4A94563D20021BB19F6C96F041AD632|<PrivateImplementationDetails>/__StaticArrayInitTypeSize=26502|…
F|CharacterDrop/Drop::m_levelMultiplierNotAmount|System.Boolean|Public
F|CharacterDrop::s_pseudoCounter|System.Collections.Generic.Dictionary`2<System.Int32,System.Int32>|Public, Static
F|CheatUtils::c_CheatRevision|System.Int32|Private, Static, Literal, HasDefault
F|Player::m_itemUpgradeSeeds|System.Collections.Generic.Dictionary`2<System.Int32,System.Int32>|Private
F|Version/Item::NewCheats|Version/Item|Public, Static, Literal, HasDefault
F|Version/Player::CheatCleanSlate|Version/Player|Public, Static, Literal, HasDefault
F|Version/PlayerData::PostLaunch|Version/PlayerData|Public, Static, Literal, HasDefault
F|Version/World::CheatCleanSlate|Version/World|Public, Static, Literal, HasDefault
M|CheatUtils::HasCheatedProperties|(ItemDrop/ItemData)|System.Boolean|CompilerControlled, Assembly, Static, HideBySig
M|CheatUtils::IsCheated|(ZDO)|System.Boolean|CompilerControlled, Assembly, Static, HideBySig
M|CheatUtils::IsCheated|(ZDO,System.Boolean)|System.Boolean|CompilerControlled, Assembly, Static, HideBySig
M|CheatUtils::RemoveCheated|(ZDO)|System.Void|CompilerControlled, Assembly, Static, HideBySig
M|CheatUtils::SetCheated|(ZDO)|System.Void|CompilerControlled, Assembly, Static, HideBySig
M|CheatUtils::SetCheated|(ZDO,System.Boolean)|System.Void|CompilerControlled, Assembly, Static, HideBySig
M|ItemDrop::EnsureValid|()|System.Void|CompilerControlled, Public, HideBySig
M|Player::GetItemUpgradeChance|(System.Int32)|System.Single|CompilerControlled, Public, HideBySig
TYPE|<PrivateImplementationDetails>/__StaticArrayInitTypeSize=18416|…
TYPE|<PrivateImplementationDetails>/__StaticArrayInitTypeSize=26502|…
TYPE|CheatUtils|base=System.Object|… Abstract, Sealed, BeforeFieldInit
```

Reading of the delta:

- **The `<PrivateImplementationDetails>` entries are compiler noise** — array-initializer blobs that grew
  from 18,400 → 18,416 and 26,455 → 26,502 bytes. No type or member of the game is affected.
- **`ItemDrop.ResetVariantIfInvalid()` → `ItemDrop.EnsureValid()`** is a pure rename (both `public () : void`).
  The mod never calls it: `grep ResetVariantIfInvalid|EnsureValid` over `CraftFromDrawersAndChests/**/*.cs`
  returns nothing.
- **`CharacterDrop.s_pseudoCounter` was retyped** `Dictionary<string,Tuple<float,int>>` →
  `Dictionary<int,int>`, with a new sibling `CharacterDrop/Drop::m_levelMultiplierNotAmount`. Loot-drop
  bookkeeping; the mod does not touch `CharacterDrop` at all.
- **The update is an anti-cheat + save-migration release.** `CheatUtils` is a new *internal static* class;
  `Version/{Item,Player,World,PlayerData}` gained enum members (`CheatCleanSlate`, `NewCheats`,
  `PostLaunch`) — those are ZDO/save version stamps, i.e. new one-shot migrations. `Player.GetItemUpgradeChance`
  and `m_itemUpgradeSeeds` add a chance-based item upgrade path.
- **Nothing the mod binds is in either list.**

## 4. The 24 touchpoints — identical old and new

Every member this mod patches (via `[HarmonyPatch]`) or resolves by reflection, dumped from the
**unpublicized** installed assembly and from the baseline copy. `old == new` compares signature *and*
access modifier, because the same probe was run against both files.

| # | Member | Signature in 1.0.20 | Access | old vs new |
|---|---|---|---|---|
| 1 | `Container::Awake` | `() : Void` | private | same |
| 2 | `Container::GetHoverText` | `() : String` | public | same |
| 3 | `Container::OnDestroyed` | `() : Void` | private | same |
| 4 | `Container::CheckAccess` | `(Int64) : Boolean` | private | same |
| 5 | `CookingStation::OnAddFuelSwitch` | `(Switch, Humanoid, ItemDrop/ItemData) : Boolean` | private | same |
| 6 | `CookingStation::OnInteract` | `(Humanoid) : Boolean` | private | same |
| 7 | `Fireplace::GetHoverText` | `() : String` | public | same |
| 8 | `Fireplace::Interact` | `(Humanoid, Boolean, Boolean) : Boolean` | public | same |
| 9 | `InventoryGui::OnCraftPressed` | `() : Void` | private | same |
| 10 | `InventoryGui::SetupRequirement` | `(Transform, Piece/Requirement, Player, Boolean, Int32, Int32) : Boolean` | public static | same |
| 11 | `InventoryGui::Update` | `() : Void` | private | same |
| 12 | `InventoryGui::UpdateCraftingPanel` | `(Boolean) : Void` | private | same |
| 13 | `Player::ConsumeResources` | `(Piece/Requirement[], Int32, Int32, Int32) : Void` | public | same |
| 14 | `Player::HaveRequirementItems` | `(Recipe, Boolean, Int32, Int32) : Boolean` | private | same |
| 15 | `Player::HaveRequirements` | `(Piece, Player/RequirementMode) : Boolean` | public | same |
| 16 | `Player::HaveRequirements` | `(Recipe, Boolean, Int32, Int32) : Boolean` | public | same |
| 17 | `Player::Update` | `() : Void` | private | same |
| 18 | `Player::UpdatePlacement` | `(Boolean, Single) : Void` | private | same |
| 19 | `Smelter::OnAddFuel` | `(Switch, Humanoid, ItemDrop/ItemData) : Boolean` | private | same |
| 20 | `Smelter::OnAddOre` | `(Switch, Humanoid, ItemDrop/ItemData) : Boolean` | private | same |
| 21 | `Smelter::OnHoverAddFuel` | `() : String` | private | same |
| 22 | `Smelter::OnHoverAddOre` | `() : String` | private | same |
| 23 | `Terminal::InputText` | `() : Void` | protected | same |
| 24 | `Player::m_placementStatus` | `: Player/PlacementStatus` | private field | same |

`Player::HaveRequirements` legitimately has two overloads (rows 15–16) — a name-only match cannot tell them
apart, which is why the dump carries parameter types.

**There is no access-modifier change anywhere in the assembly.** That is the strongest form of this result:
the publicized-assembly trap (compile against a publicized reference, fail at runtime because the shipped
assembly does not expose the member) cannot have been sprung by this update.

## 5. Verdict

**No code change is required.** Three independent reasons:

1. No removed or retyped member is referenced by this mod (verified by grep over the source, §3).
2. All 24 bound members are byte-for-byte identical in signature and access (§4).
3. **All 22 patches are `[HarmonyPrefix]`/`[HarmonyPostfix]` — the mod contains zero transpilers**
   (verified: `grep Transpiler|CodeInstruction` over `CraftFromDrawersAndChests/**/*.cs` finds nothing).
   Prefix/postfix hooks depend only on the target's identity and signature, so body-level IL churn in the
   game cannot break them; a transpiler patch would be the exposed style here.

Build and test status at the time of writing (against the 1.0.16 refs, i.e. the pre-change baseline):
`tools/Build.ps1 -Configuration Debug` → `BUILD OK - CraftFromDrawersAndChests 1.0.3 (Debug)`; `dotnet test`
→ **262 passed, 0 failed**.

**Caveat on the tests:** the NUnit suite is `Logic/**`-only and cannot observe game APIs. It is a
regression guard, not compatibility evidence — §4 is the compatibility evidence. In-game behaviour, and
especially the new save-migration path in §7, can only be settled by running the game.

## 6. How to re-run this analysis

The baseline pair is the whole trick: `.references\Valheim\<ver>\Managed\` holds the **unpublicized** game
assemblies this mod was verified against, so it is a true old/new pair against
`<Steam>\steamapps\common\Valheim\valheim_Data\Managed\`.

```powershell
# 1. dump both sides (child processes; do not do this in a long-lived shell)
$s = 'docs\sdd\2026-10-11-craft-from-drawers-and-chests\tools\dump-api.ps1'
$g = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Valheim\valheim_Data\Managed"
$o = ".references\Valheim\<ver>\Managed"
powershell -NoProfile -ExecutionPolicy Bypass -File $s -Assembly "$g\assembly_valheim.dll" -OutFile "$env:TEMP\new_valheim.txt"
powershell -NoProfile -ExecutionPolicy Bypass -File $s -Assembly "$o\assembly_valheim.dll" -OutFile "$env:TEMP\old_valheim.txt"

# 2. diff, case-sensitively
$diff = Compare-Object (Get-Content "$env:TEMP\old_valheim.txt") (Get-Content "$env:TEMP\new_valheim.txt") -CaseSensitive
$diff | Where-Object SideIndicator -eq '<=' | ForEach-Object InputObject   # removed
$diff | Where-Object SideIndicator -eq '=>' | ForEach-Object InputObject   # added
```

To check a specific target, or read IL/callers:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File docs\sdd\2026-10-11-craft-from-drawers-and-chests\tools\cecil.ps1 `
    -Assembly "$g\assembly_valheim.dll" -Type CheatUtils -Method IsCheated -IL -MaxLines 90
```

Always pass `-Method` to `cecil.ps1`: omitting it makes the script prompt for it, and the next command sent
to that shell is consumed as the answer.

## 7. Anti-cheat scope — why item movement between containers is not implicated

`CheatUtils.IsCheated(ZDO)` is a one-line ZDO read (IL from the 1.0.20 assembly):

```
0000  ldarg.0
0001  ldsfld System.Int32 ZDOVars::s_cheated
0006  ldc.i4.0
0007  callvirt System.Int32 ZDO::GetInt(System.Int32,System.Int32)
000C  ldc.i4.2
000D  ceq
000F  ret
```

i.e. *true when the ZDO's `s_cheated` int var is 2*. `ZDOVars::s_cheated` is **not** new (it is absent from
the added list), so this is a new wrapper around an existing flag, not a new save field.

Call sites (`cecil.ps1 -Callers`, scanning 11,048 methods / 390,609 instructions):

- **`IsCheated`** — `Character.OnDeath`, `Container.AddDefaultItems`, `CookingStation.DropAllItems` (and its
  `<DropAllItems>g__drop` closure), `DropOnDestroyed.OnDestroyed`, `Fermenter.DelayedTap`,
  `InventoryGui.DoCrafting`, `Piece.DropResources`, `Ragdoll.SpawnLoot`, `Smelter.Spawn`,
  `ZDOMan.ConvertInventories`.
- **`SetCheated`** — `Character.ApplyDamage`, `Destructible.Destroy`, `Player.PlacePiece`, `Ragdoll.Setup`,
  the cheat console's `Terminal.<InitTerminal>g__spawn` closure, `ZoneSystem.SpawnLocation`.
- **`HasCheatedProperties`** — `ItemDrop.EnsureValid` only.

Every reader/writer sits on a *spawn*, *drop*, *destroy* or *cheat-console* path, i.e. the game deciding
whether an object that is being created or destroyed was cheated. This mod does neither: it moves items
between existing inventories through the vanilla `Inventory` API (`grep` finds no reference to
`CheatUtils`, `ZDOVars` or `s_cheated` anywhere in the source), and it never calls `Player.PlacePiece` or a
terminal spawn. Nothing in the mod's paths sets or depends on the flag.

The one genuinely new *runtime* risk is the migration, not the flag: `Version/World::CheatCleanSlate` and
`Version/Player::CheatCleanSlate` are new save-version stamps, so the first load of an **existing** world
under 1.0.20 runs a clean-slate pass this mod has never been exercised against. That is why loading a
pre-update world is the first item on the owner's in-game checklist.
