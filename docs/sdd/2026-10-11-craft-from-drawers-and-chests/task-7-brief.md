### Task 7: Plugin bootstrap

**Files:**
- Create: `CraftFromDrawersAndChests/Plugin.cs`

**Interfaces:**
- Consumes: `ModInfo` (Task 6).
- Produces: `class Plugin : BaseUnityPlugin` with `public static Plugin Instance`, `public static Harmony Harmony`, and `internal static ManualLogSource Log`. Later tasks patch from `Plugin.Harmony` and log through `Plugin.Log`.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — `Plugin.cs` missing, MSBuild error `CS2001`/`CS0246`.

- [ ] **Step 2: Implement `Plugin.cs`**

Attributes, exactly:

```csharp
[BepInPlugin(ModInfo.ModGuid, ModInfo.ModName, ModInfo.Version)]
[BepInDependency(Jotunn.Main.ModGuid)]
[BepInDependency("com.maxsch.valheim.MultiUserChest")]
[BepInDependency("kg.ItemDrawers", BepInDependency.DependencyFlags.SoftDependency)]
[BepInIncompatibility("aedenthorn.CraftFromContainers")]
[BepInIncompatibility("Azumatt.AzuCraftyBoxes")]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
public class Plugin : BaseUnityPlugin
```

`Awake` sets `Instance`, assigns `Harmony = new Harmony(ModInfo.ModGuid)`, calls `Harmony.PatchAll()`. MultiUserChest is a **hard** dependency: with it absent the only working chest implementation is the direct mutation this design forbids.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`.

- [ ] **Step 4: Verify in game**

Deploy the DLL to `BepInEx\plugins\` on a client and to the server, launch, and confirm the BepInEx log shows the plugin loading with no Harmony patch errors. Remove MultiUserChest from one client and confirm the connection is refused — that is the enforcement working.
Expected: loads cleanly; the mismatch case refuses the connection.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Plugin.cs
git commit -m "feat: plugin bootstrap with Jotunn enforcement"
```

---

