# Task 7 — Plugin bootstrap — Report

**Task:** Create `CraftFromDrawersAndChests/Plugin.cs` — the BepInEx entry point with the
`[BepInPlugin]` identity, Jötunn/MultiUserChest dependency declarations, `[BepInIncompatibility]`
guards, `[NetworkCompatibility]` enforcement, and an `Awake` that wires up `Instance`,
`Harmony` and `PatchAll()`.

**Branch:** `feature/craft-from-drawers-and-chests`
**Commit:** `5eac15c` — `feat: plugin bootstrap with Jotunn enforcement`
**Status:** DONE

---

## 1. Verification of the prior implementer's work

The prior implementer left two uncommitted changes: a new `Plugin.cs` (48 lines) and a
one-line `<Compile Include="Plugin.cs" />` addition to the csproj. The prior implementer
never self-reported, so nothing was assumed. I compared both against the brief's Step 2 and
Interfaces block and verified the compiled output independently.

**Result: the prior work is correct and required no changes.** I accepted it as-is and
committed it verbatim (no edits).

### 1a. Attributes — exact match (verified in source *and* compiled metadata)

| Brief requirement | Present in `Plugin.cs` | In built DLL (Mono.Cecil) |
|---|---|---|
| `[BepInPlugin(ModInfo.ModGuid, ModInfo.ModName, ModInfo.Version)]` | yes | `BepInPlugin(NorCal_Nerds.CraftFromDrawersAndChests, CraftFromDrawersAndChests, 0.1.0)` |
| `[BepInDependency(Jotunn.Main.ModGuid)]` (hard) | yes | 1 of 3 `BepInDependency` attrs; Jötunn GUID literal `com.jotunn.jotunn` present in binary |
| `[BepInDependency("com.maxsch.valheim.MultiUserChest")]` (hard) | yes | literal present in binary |
| `[BepInDependency("kg.ItemDrawers", SoftDependency)]` (soft) | yes | literal `kg.ItemDrawers` present |
| `[BepInIncompatibility("aedenthorn.CraftFromContainers")]` | yes | `BepInIncompatibility(aedenthorn.CraftFromContainers)` |
| `[BepInIncompatibility("Azumatt.AzuCraftyBoxes")]` | yes | `BepInIncompatibility(Azumatt.AzuCraftyBoxes)` |
| `[NetworkCompatibility(EveryoneMustHaveMod, VersionStrictness.Patch)]` from `Jotunn.Utils` | yes (via `using Jotunn.Utils;`) | `Jotunn.Utils.NetworkCompatibilityAttribute` present |

Notes on the metadata check: the custom attribute *counts* on the compiled `Plugin` type are
exactly 1 `BepInPlugin`, 3 `BepInDependency`, 2 `BepInIncompatibility`, 1
`NetworkCompatibilityAttribute` — matching the source. Cecil could not decode the
enum-typed constructor arguments (`DependencyFlags`, `CompatibilityLevel`, `VersionStrictness`)
without resolving BepInEx/Jötunn, so I confirmed the string GUIDs independently by scanning
the built DLL's UTF-8 string heap. All eight expected literals were found: `NorCal_Nerds.
CraftFromDrawersAndChests`, `CraftFromDrawersAndChests`, `0.1.0`, `com.jotunn.jotunn`,
`com.maxsch.valheim.MultiUserChest`, `kg.ItemDrawers`, `aedenthorn.CraftFromContainers`,
`Azumatt.AzuCraftyBoxes`.

### 1b. Members

- `public static Plugin Instance;` — static, public field. ✔
- `public static Harmony Harmony;` — static, public field. ✔
- `internal static ManualLogSource Log;` — **implemented as a forwarding property**
  `internal static ManualLogSource Log => Instance.Logger;` (see §6 self-review — this is the
  one interpretation-sensitive point; it satisfies the brief's member signature and keeps
  `Awake` to the exact three actions the brief enumerates).

### 1c. `Awake`

Source:

```csharp
Instance = this;
Harmony = new Harmony(ModInfo.ModGuid);
Harmony.PatchAll();
```

Confirmed at the IL level — the `Awake` body is exactly (nop-instrumented Debug build):
`this → Instance`, `new Harmony("NorCal_Nerds.CraftFromDrawersAndChests") → Harmony`,
`Harmony.PatchAll()`. No extra behaviour: no config binding, no Harmony patches registered by
this task (there are none to register yet — later tasks supply them).

### 1d. csproj change

Exactly one line was added, inside the existing legacy-style `<Compile>` item group, after
`ModInfo.cs`:

```xml
<Compile Include="Plugin.cs" />
```

This is required by the global constraint that the product project is legacy-style with
explicit `<Compile>` items — without it `Plugin.cs` would be invisible to the build. Verified
effective: the built DLL grew from 15,360 bytes (Task 6) to 16,384 bytes and the `Plugin` type
is present in the assembly's metadata.

### 1e. Prohibited content

No subagent was dispatched. No other mod's identity, feature description, path or URL appears
in the file. No Nexus-flavoured artefact. `Logic/**` was not touched. No patches, config, or
game-facing behaviour were added (out of scope for Task 7).

---

## 2. Build

Command:

```
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug
```

Final lines:

```
  CraftFromDrawersAndChests -> C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll
    Built C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll (16,384 bytes)

BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```

Matches the expected `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`. The only warning is
the deliberately-unsuppressed `MSB3274` (`MultiUserChest` is `net472` against this project's
`net462`) — expected and not a failure.

---

## 3. Tests

Command:

```
dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj
```

Result:

```
NUnit3TestExecutor discovered 41 of 41 NUnit test cases
Test summary: total: 41, failed: 0, succeeded: 41, skipped: 0, duration: 2.1s
Build succeeded in 4.1s
```

Matches the expected 41 passing, 0 failing. No test was skipped, modified, or added.

---

## 4. Files changed

| File | Change |
|---|---|
| `CraftFromDrawersAndChests/Plugin.cs` | new, 48 lines |
| `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` | +1 line (`<Compile Include="Plugin.cs" />`) |

Commit `5eac15c` — 2 files changed, 49 insertions(+). Working tree clean afterward.

---

## 5. AUTHOR-VERIFY (in-game — brief Step 4, not executed here)

The brief's Step 4 requires a running Valheim server and a second player, which is not
available in this environment, so it was **not** run. The exact steps for a human are below.

**Environment required:** Valheim 1.0.16 with BepInEx pack 5.4.2350, Jötunn 2.30.2, and
MultiUserChest 0.6.2 (Item Drawers 1.4.0 optional — it is a *soft* dependency).

1. **Build the DLL.**
   `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
   → `CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll`.

2. **Deploy to the server.** Copy that DLL into the server's
   `BepInEx\plugins\` (e.g. `...\BepInEx\plugins\CraftFromDrawersAndChests\CraftFromDrawersAndChests.dll`),
   alongside Jötunn and MultiUserChest. Start the server.

3. **Deploy to a client.** Copy the same DLL into client A's `BepInEx\plugins\`, alongside
   Jötunn and MultiUserChest. Launch the game.

4. **Confirm a clean load.** Open `BepInEx\LogOutput.log` (server) and the client's
   `LogOutput.log`. Expect:
   - a line showing the plugin loading, e.g. `Loading [CraftFromDrawersAndChests 0.1.0]`;
   - the plugin GUID `NorCal_Nerds.CraftFromDrawersAndChests` registered;
   - **no** Harmony patch exceptions for this plugin (Task 7 registers no patches, so the
     expected state is "no errors" — this is a regression guard for later tasks).

5. **Connect client A** to the server normally. Jötunn's network-compatibility handshake must
   succeed (client connects, no version-mismatch rejection).

6. **Negative check — enforcement (brief wording).** Remove/rename
   `MultiUserChest.dll` from one client's `plugins\`, then connect that client.
   - **Expect:** the connection is refused (the brief's "enforcement working").
   - **What to actually look for (two distinct gates):**
     - *Local hard-dependency gate:* with MultiUserChest absent, BepInEx will *not load*
       `CraftFromDrawersAndChests` on that client and will log the missing hard dependency
       GUID `com.maxsch.valheim.MultiUserChest`. The plugin is disabled locally.
     - *Network gate:* with the plugin absent/disabled on the client while the server (and
       Jötunn) require `EveryoneMustHaveMod`, Jötunn refuses the connection with a
       version/compatibility message. This is the `[NetworkCompatibility(...)]` enforcement.
   - **Caveat:** MultiUserChest itself ships its own `NetworkCompatibility`. Keep MultiUserChest
     versions identical across client/server wherever you want the rejection to be
     attributable to *this* mod rather than to MultiUserChest's own check.

7. **Optional — soft dependency sanity.** With Item Drawers absent, the plugin must still load
   cleanly (it is declared `SoftDependency`).

---

## 6. Self-review findings

- **No code changes were made.** The prior implementer's `Plugin.cs` and the csproj line match
  the brief; the right action was to verify and accept, not to rewrite. I verified against the
  brief, the design spec, and the *compiled* artifact (attribute metadata, string heap, and
  `Awake` IL), not just by eye.
- **`Log` is a property, not a field (the one judgment call).** The brief's Interfaces line
  lists `internal static ManualLogSource Log` alongside two fields, and separately states that
  `Awake` does exactly three things: *"sets `Instance`, assigns `Harmony = new Harmony(...)`,
  calls `Harmony.PatchAll()`"* — it does **not** list assigning `Log`. Those two statements are
  only mutually consistent if `Log` is **not** a field assigned in `Awake`. The implemented
  forwarding property `Log => Instance.Logger` therefore satisfies both statements: it declares
  the required member (`internal static ManualLogSource Log`, accessible as `Plugin.Log` for
  later tasks) and leaves `Awake` at exactly the three enumerated actions. I judged this the
  most literal compliance with the brief and left it unchanged. If the reviewer's intent is
  specifically a *field* (`Log = Logger;` added to `Awake`), the fix is a two-line change; I did
  not make it because it would add an `Awake` action the brief does not list.
- **csproj edit is in-scope and necessary.** The brief's Files list names only `Plugin.cs`, but
  the global constraint (legacy-style explicit `<Compile>`) makes the csproj line an intrinsic
  part of adding the file. It is committed in the same commit.
- **Failure-path check performed first (in effect).** Task 6's ledger noted the build failing at
  `Plugin.cs` missing; this task's build now compiles it, so the red→green transition is
  satisfied.
- **Scope discipline.** No Harmony patches, no `Config.Init`, no game-facing behaviour; `Awake`
  is only Instance/Harmony/PatchAll. (Task 8 will add `Config.Init` to `Awake`.)
- **Guarded against file lock.** The DLL was inspected in child PowerShell processes (and via
  Cecil on in-memory bytes) so the build output was never locked for the subsequent build.

## 7. Concerns

1. **`Log` property-vs-field (low risk).** Documented above; the only element open to
   interpretation. Functionally equivalent for all declared consumers (`Plugin.Log` /
   `Plugin.Harmony`).
2. **In-game verification not executed (by instruction).** Brief Step 4 needs a live server and
   a second player; handed over in §5. Everything that can be checked statically/build-side is
   green.
3. **Method note (not a defect).** The `Plugin` type could not be reflected into a runtime
   `Type` without BepInEx present (its base type is `BepInEx.BaseUnityPlugin`); inspection was
   done via Mono.Cecil metadata + a UTF-8 string scan + IL dump, which is dependency-free and
   therefore more authoritative anyway.
