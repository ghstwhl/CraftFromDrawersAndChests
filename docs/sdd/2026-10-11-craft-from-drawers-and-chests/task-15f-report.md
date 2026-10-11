# Task 15f — fix the total in-game failure (report)

Repo: `C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests`
Branch: `feature/craft-from-drawers-and-chests` · base HEAD `bf8e325` · fix commit **`92c029e`** (not pushed).

Sources used:
- Jötunn 2.30.2 XML docs, `C:\Users\merlin\.nuget\packages\jotunnlib\2.30.2\lib\net462\Jotunn.xml`
  (member names quoted verbatim below).
- The sanctioned probe `tools\cecil.ps1` against
  `.references\Valheim\1.0.16\assembly_valheim_publicized.dll`, plus the plain-text oracle
  `.references\Mods\CraftFromContainers\BepInExPlugin.cs` (CraftFromContainers 4.0.7) for parameter
  *names* only.
- No assembly was decompiled; `ilspycmd` was not run; no ad-hoc probe was written.

---

## 1. Root cause and the fix

`Plugin.Awake` was `PluginConfig.Init(Config)` → `PluginConfig.ReadInto(...)` → `Harmony.PatchAll()`.
`PluginConfig.Init` ended with

```csharp
SynchronizationManager.Instance.RegisterCustomConfig(config);   // always threw
```

Jötunn's own XML says that method registers a **non-default** config file, and its documented
exception list includes *“The config file is a default mod config and is already implicitly
synchronized”* (`M:Jotunn.Managers.SynchronizationManager.RegisterCustomConfig(BepInEx.Configuration.ConfigFile)`).
A plugin's own `BaseUnityPlugin.Config` **is** a default mod config, so the call could never be
correct — it only ever threw `ArgumentException`. Thrown from inside `Awake`, it aborted the method
before `Harmony.PatchAll()`, so **no patch was applied at all** and every feature was dead. BepInEx
still logged `0 failed` because an exception in a plugin's `Awake` is surfaced by Unity, not by the
chainloader — hence the silence.

**Fix.** The call is deleted and replaced by a comment at the deletion site (`PluginConfig.cs`,
immediately after the `OnConfigurationSynchronized` wiring) recording *why* it must not return: a
future reader "restoring" it would silently kill every feature again. `OnConfigurationSynchronized +=
RefreshFlags` is untouched and stays wired before the (now absent) registration point, so the
post-sync re-read is unaffected. Nothing is lost by removing the call: each entry is still bound with
`ConfigurationManagerAttributes { IsAdminOnly = true }`, which is the attribute Jötunn actually needs.

Verified on the built DLL: `cecil.ps1 -Assembly CraftFromDrawersAndChests\bin\Debug\…dll -Method
RegisterCustomConfig -Callers` → **`(none)`** (307 methods / 10916 instructions scanned).

## 2. Is Task 8's feature still intact?

| Question | Answer | Jötunn member relied on |
|---|---|---|
| Does a default mod config get synchronised **without** a registration call? | **Yes.** Jötunn manages "all default and custom config files that [are] associated with plugins that have Jotunn as a dependency", which is exactly why `RegisterCustomConfig` throws "already implicitly synchronized" for one. | `M:Jotunn.Managers.SynchronizationManager.GetConfigFiles` (summary); `…RegisterCustomConfig(…)` (third exception). |
| What makes an entry's value server-authoritative **and** read-only for a non-admin? | `IsAdminOnly` on the entry: "Whether a config is only writable by admins **and gets overwritten on connecting clients**". Every entry already carries it. | `P:ConfigurationManagerAttributes.IsAdminOnly`. |
| What do `SynchronizationModeAttribute` / `AdminOnlyStrictness` control, and is the attribute needed on `Plugin`? | The attribute records *how strictly* the `IsAdminOnly` lock is enforced: `Always` = "always enforced … even if the mod is not installed on the server"; `IfOnServer` = enforced only when the mod is present on the server. Jötunn decides the lock with `ShouldManageConfig`, which reads the strictness "for the plugin that the config file is attached to … and whether the plugin is installed on the server or not". Because this mod is a **hard dependency** with `NetworkCompatibility(EveryoneMustHaveMod)`, the mod is on the server, so the lock is enforced under either value. **With no attribute the strictness is an implicit default the XML does not state**, so it is now pinned explicitly rather than left to chance. | `T:Jotunn.Utils.SynchronizationModeAttribute`; `F:Jotunn.Utils.AdminOnlyStrictness.Always` / `.IfOnServer`; `M:Jotunn.Managers.SynchronizationManager.ShouldManageConfig(BepInEx.Configuration.ConfigFile)`. |

**Change made.** `Plugin` now carries `[SynchronizationMode(AdminOnlyStrictness.Always)]` (its ctor,
`M:Jotunn.Utils.SynchronizationModeAttribute.#ctor(Jotunn.Utils.AdminOnlyStrictness)`, takes the enum
value). This makes "non-admin clients see every entry read-only" hold independently of the undocumented
default and of server detection, which is a superset of Task 8's requirement. Admins and the local host
still edit (`PlayerIsAdmin` is "always true on local games"); in-game checks will not block.

## 3. The `Awake` guard — behaviour, and why it cannot be silent

`Plugin.Awake` now initialises the config inside a guard:

```csharp
try
{
    PluginConfig.Init(Config);
    PluginConfig.ReadInto(PluginConfig.Flags);
}
catch (Exception e)
{
    if (PluginConfig.Flags != null)
        PluginConfig.Flags.ModEnabled = false;   // deterministic "off", not half-configured
    Log.LogError("CraftFromDrawersAndChests: configuration initialisation failed; the mod is being " +
                 "left fully disabled until this is fixed (Harmony patches are still applied, but " +
                 "every config-gated feature is inert). Cause: " + e);
}

Harmony.PatchAll();
```

- **Non-fatal.** A throw from `Init`/`ReadInto` can no longer skip `Harmony.PatchAll()`; every patch is
  still registered, so one config defect cannot take the whole mod down again.
- **Loud.** The failure is reported with `Log.LogError`, which is written to `LogOutput.log` at Error
  level regardless of the user's configured log level, and the message **names the mod** so it is
  findable. This is the specific signal that was missing (the original failure was a `[Error: Unity Log]`
  line that the chainloader still counted as `0 failed`).
- **Cannot be a silent no-op.** `FeatureFlags` is a non-defaulted DTO: a read that failed partway would
  otherwise leave an *unpredictable* half-populated snapshot (some flags true), which is worse than inert
  because it is erratic. The guard forces `ModEnabled = false`, so a failure collapses to one clean,
  deterministic state — fully disabled — and the `LogError` above is always emitted with it. Every
  config-gated feature checks `ModEnabled` (directly, or through `Enabled()`), so one assignment disables
  them all consistently. The outcome is "loud and disabled", never "quiet and doing nothing".

`Harmony.PatchAll()` is deliberately left unguarded: §4 proves every target resolves, and an unhandled
`PatchAll` failure would be the same Unity-logged silence this task is closing — but there is currently
no target for it to fail on.

## 4. Complete patch-target audit

Every `[HarmonyPatch]` in the product, probed against `assembly_valheim_publicized.dll`
(`cecil.ps1 -Method <name> -Definitions` / `-Fields`). "`[HarmonyPrepare]`?" is "No" unless the class
declares one. **No patch target is a member the mod *calls*, and no target is `private`** — the private
members the mod does touch (`Smelter.GetQueueSize`/`GetFuel`, `CookingStation.GetFuel`/`HaveDoneItem`/
`GetFreeSlot`/`IsFireLit`, `Player.m_placementStatus`, `InventoryGui.UpdateCraftingPanel`) are reached via
`AccessTools` and are **not** patch targets, so the R29/R30 rule is respected on both sides.

| # | Declaring class | Target type | Target member (as resolved) | Exists in publicized asm? | `[HarmonyPrepare]`? |
|---|---|---|---|---|---|
| 1 | `CraftPatches` | `Player` | `HaveRequirementItems(Recipe, Boolean, Int32, Int32)` | **Yes** (public) | No |
| 2 | `CraftPatches` | `Player` | `HaveRequirements(Piece, RequirementMode)` | **Yes** (public) | No |
| 3 | `CraftPatches` | `Player` | `ConsumeResources(Requirement[], Int32, Int32, Int32)` | **Yes** (public) | No |
| 4 | `PullKeyPatches` | `InventoryGui` | `OnCraftPressed()` | **Yes** (public) | No |
| 5 | `PullKeyPatches` | `Player` | `UpdatePlacement(Boolean, Single)` | **Yes** (public) | No |
| 6 | `PullKeyPatches` | `InventoryGui` | `SetupRequirement(Transform, Requirement, Player, Boolean, Int32, Int32)` | **Yes** (public) | No |
| 7 | `PullKeyPatches` | `InventoryGui` | `Update()` | **Yes** (public) | No |
| 8 | `StationPatches` | `Fireplace` | `Interact(Humanoid, Boolean, Boolean)` | **Yes** (public) | No |
| 9 | `StationPatches` | `Smelter` | `OnAddOre(Switch, Humanoid, ItemData)` | **Yes** (public) | No |
| 10 | `StationPatches` | `Smelter` | `OnAddFuel(Switch, Humanoid, ItemData)` | **Yes** (public) | No |
| 11 | `StationPatches` | `CookingStation` | `OnAddFuelSwitch(Switch, Humanoid, ItemData)` | **Yes** (public) | No |
| 12 | `StationPatches` | `CookingStation` | `OnInteract(Humanoid)` | **Yes** (public) | No |
| 13 | `StationPatches` | `Turret` | `UseItem(Humanoid, ItemData)` | **Yes** (public) | No |
| 14 | `StationPatches` | `Smelter` | `OnHoverAddFuel()` | **Yes** (public) | No |
| 15 | `StationPatches` | `Smelter` | `OnHoverAddOre()` | **Yes** (public) | No |
| 16 | `TerminalPatch` | `Terminal` | `InputText()` | **Yes** (public) | No |
| 17 | `ContainerTrackingPatches` | `Container` | `Awake()` | **Yes** (public) | No |
| 18 | `ContainerTrackingPatches` | `Container` | `OnDestroyed()` | **Yes** (public) | No |
| 19 | `TogglePatches` | `Container` | `GetHoverText()` | **Yes** (public) | No |
| 20 | `TogglePatches` | `Player` | `Update()` | **Yes** (public) | No |
| 21 | `DrawerHoverTextPatch` | `kg_ItemDrawers.DrawerComponent` (soft dep) | `GetHoverText()` | Runtime-resolved, not in Valheim | **Yes** — `Prepare()` returns `DrawerComponentType() != null`, so the class is skipped when Item Drawers is absent |

**Injected-field types were checked too** (a wrong `___field` type also throws at `PatchAll`):
`InventoryGui.m_selectedRecipe : RecipeDataPair`, `m_craftUpgradeItem : ItemData`, `m_multiCrafting :
Boolean`, `m_multiCraftAmount : Int32`, `m_animator : Animator`; `Player.m_placementStatus :
PlacementStatus`, `m_buildPieces : PieceTable`, `m_placementGhost : GameObject`; `Container`/`Fireplace`/
`CookingStation.m_nview : ZNetView` — all match the patch signatures.

**Overload safety (the recurring trap).** `Player.HaveRequirements` has two overloads; the patch pins the
`(Piece, Player.RequirementMode)` pair with an explicit `System.Type[]`, so it is unambiguous. Every
other target name resolves to exactly one member on its declaring type.

## 5. Verification

| Step | Command | Result |
|---|---|---|
| Debug build | `Build.ps1 -Configuration Debug` | **`BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`**, DLL 80,896 B |
| Tests | `dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj` | **238 total, 0 failed, 238 succeeded** |
| Release build + package | `Build.ps1` (Release) | **`BUILD OK - CraftFromDrawersAndChests 0.1.0 (Release)`**, DLL **73,728 B** |
| DLL check | `cecil.ps1 … -Method RegisterCustomConfig -Callers` | `(none)` — the throwing call is gone from the artifact |

**Installable artifacts (Release):**
- Plugin DLL: `CraftFromDrawersAndChests\bin\Release\CraftFromDrawersAndChests.dll` — **73,728 bytes**
- Staged package: `Release\NorCal_Nerds-CraftFromDrawersAndChests\`
- Zip: **`Release\CraftFromDrawersAndChests-0.1.0.zip`** (48,943 bytes; identical
  `CraftFromDrawersAndChests-release.zip`)

`MSB3274` (MultiUserChest is net472, we target net462) appeared as expected and was not touched.
No new files were added, so no legacy-csproj `<Compile>` registration was needed.

## 6. Concerns / notes for the owner

- **`AdminOnlyStrictness.Always` is a judgement call.** It is a superset of Task 8 (the lock is now
  independent of the implicit default and of server detection). It follows the mod's own hard
  `EveryoneMustHaveMod` policy; in local games the host is admin and can still edit. If a "non-admin may
  edit when the mod is absent from the server" posture is ever wanted, switch it to `IfOnServer`.
- **The in-game failure was never reached at `PatchAll` before this fix**, so the audit in §4 is the
  first proof that the patch set is registrable. It is static verification only; the owner's own in-game
  pass remains the real confirmation.
- Parameter *names* for the three 1.0.16 three-argument station methods (`Switch, Humanoid, ItemData`)
  cannot be read from XML docs and were cross-checked against the CFC 4.0.7 oracle for the unchanged
  trailing `user`/`item` names; Valheim added the leading `Switch` argument, which the patches do not
  declare.
- Commit `92c029e` is local only — **not pushed** (the owner pushes in batches).
