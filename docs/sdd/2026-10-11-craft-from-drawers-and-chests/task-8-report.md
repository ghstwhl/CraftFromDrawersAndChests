# Task 8 — Server-authoritative config — Report

**Task:** Bind the mod's BepInEx configuration surface in a new game-facing `PluginConfig`,
make it server-authoritative by registering it with Jötunn's `SynchronizationManager`, and
populate the shared `FeatureFlags` snapshot from `Plugin.Awake` before any patch can run.

**Branch:** `feature/craft-from-drawers-and-chests`
**Commit:** `255d0dd` — `feat: server-synced configuration`
**Status:** DONE_WITH_CONCERNS

---

## 1. What was implemented

### 1a. `CraftFromDrawersAndChests/PluginConfig.cs` (new)

A `static class PluginConfig` with the interface the brief specifies, plus the rulings:

| Member | Purpose |
|---|---|
| `public static ConfigFile File` | The plugin's config file, as handed to `Init`. |
| `public static FeatureFlags Flags` | The shared, resolved snapshot; created by `Init`. |
| `public static void Init(ConfigFile config)` | Binds every entry, then `SynchronizationManager.Instance.RegisterCustomConfig(config)`. |
| `public static void ReadInto(FeatureFlags flags)` | Copies every entry value into the snapshot. |
| `public static Color FlashColor` / `UnFlashColor` | Game-facing accessors for the two flash-colour entries. |

**27 entries bound, one per `ConfigModel` key-name constant** (the ZDO key is deliberately
*not* bound — Ruling 5). Section/key/default plumbing all reads `ConfigModel`:

| Section (`ConfigModel`) | Entries |
|---|---|
| `SectionServer` ("1 - Server") | `MaxRange`, `EnableChests`, `EnableDrawers`, `LeaveOne`, `PullByDistance`, `PullFuel`, `PullOre`, `PullCooking`, `FuelDisallowTypes`, `OreDisallowTypes`, `ResourceString`, `PulledMessage`, `FlashColor`, `UnFlashColor`, `Ignore{Ship,Wagon,Wood,Private,BlackMetal,Reinforced}*`, `ModEnabled` — 21 |
| `SectionKeys` ("2 - Keys") | `PullItemsKey`, `ToggleStorageKey`, `PreventModKey`, `FillAllModKey`, `SwitchPrevent` — 5 |
| `SectionDebug` ("9 - Debug") | `IsDebug` — 1 |

Types follow the brief: `maxRange` is `float`; the key settings, the two disallow-type lists,
`resourceString` and `pulledMessage` are `string`; the two flash colours are
`ConfigEntry<Color>`; everything else is `bool`.

Rulings honoured:

- **R1 / Ruling 18** — the class is `PluginConfig`, not `Config`.
- **R2 / Ruling 13** — `preventModKey`, `fillAllModKey` and `switchPrevent` are bound and
  written into `FeatureFlags`; **no behaviour** for them is implemented (that is T14/T15).
- **R3 / Ruling 14** — the flash colours are `ConfigEntry<Color>` on `PluginConfig`, exposed
  through `FlashColor`/`UnFlashColor`; they are **not** members of `FeatureFlags`.
- **R4 / Ruling 15** — `ReadInto` is the only code that assigns to a `FeatureFlags` member.
  Verified mechanically (see §5).
- **R5** — no config entry for `ConfigModel.ZdoKey`; `ZdoKey` appears nowhere in
  `PluginConfig.cs`.
- **R6** — every section name, key name and default that `ConfigModel` defines is referenced
  as a constant, never re-typed. See §5 for the mechanical proof.

### 1b. `CraftFromDrawersAndChests/Plugin.cs` (modified)

`Awake` now populates the snapshot before `PatchAll`, so every patch observes a filled
`FeatureFlags` rather than the all-false values a non-defaulted DTO would hold:

```csharp
Instance = this;
Harmony = new Harmony(ModInfo.ModGuid);

PluginConfig.Init(Config);
PluginConfig.ReadInto(PluginConfig.Flags);

Harmony.PatchAll();
```

`Config` here is `BaseUnityPlugin.Config` (a `ConfigFile`) — exactly the binding that made
Ruling 18 necessary.

### 1c. `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` (modified)

One line added to the legacy `<Compile>` item group: `<Compile Include="PluginConfig.cs" />`
(Ruling 9 — the product project is legacy-style and would not otherwise see the new file).

---

## 2. TDD evidence

The brief's Step 1 ("build fails — `Config.cs` missing") is only a real red once something
references the new class. So the red was produced honestly by wiring the consumer first:

1. **Red.** Modified `Plugin.cs` to call `PluginConfig.Init(Config)` and
   `PluginConfig.ReadInto(PluginConfig.Flags)` with `PluginConfig.cs` absent. Build:

   ```
   CraftFromDrawersAndChests\Plugin.cs(50,13): error CS0103: The name 'PluginConfig' does not exist in the current context
   CraftFromDrawersAndChests\Plugin.cs(51,13): error CS0103: The name 'PluginConfig' does not exist in the current context
   CraftFromDrawersAndChests\Plugin.cs(51,35): error CS0103: The name 'PluginConfig' does not exist in the current context
   !! Build failed (exit 1).
   ```

2. **Green.** Created `PluginConfig.cs` and added the csproj `<Compile>` entry → `BUILD OK`
   (§3).

---

## 3. Build

Command:

```
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug
```

Final lines:

```
  CraftFromDrawersAndChests -> C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll
    Built C:\Users\merlin\Documents\Repos\CraftFromDrawersAndChests\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll (23,040 bytes)

BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```

Matches the expected `BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)`. The DLL grew from
16,384 bytes (Task 7) to **23,040 bytes**. The only warning is the expected, deliberately
unsuppressed `MSB3274` (`MultiUserChest` is `net472` against this project's `net462`).

---

## 4. Tests

Command:

```
dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj
```

Result:

```
Passed!  - Failed:     0, Passed:    41, Skipped:     0, Total:    41, Duration: 190 ms - CraftFromDrawersAndChests.Tests.dll (net462)
```

41 passing, 0 failing, 0 skipped — as required. No test was added, changed or skipped: the
`Tests` project links only `Logic/**`, and `PluginConfig` is game-facing
(`BepInEx`/`UnityEngine`/`Jötunn`), so it is not unit-testable there and this task adds none.

---

## 5. Files changed

| File | Change |
|---|---|
| `CraftFromDrawersAndChests/PluginConfig.cs` | new — 197 lines |
| `CraftFromDrawersAndChests/Plugin.cs` | +8 (the two config calls, with a comment) |
| `CraftFromDrawersAndChests/CraftFromDrawersAndChests.csproj` | +1 (`<Compile Include="PluginConfig.cs" />`) |

Commit `255d0dd` — 3 files changed, 206 insertions(+). The only other working-tree change is
`docs/superpowers/plans/2026-10-11-craft-from-drawers-and-chests.md`, which was already
modified before this task began and was **not** staged or committed.

---

## 6. AUTHOR-VERIFY (in-game — brief Step 4, not executed here)

The brief's Step 4 needs a running Valheim server and a second player, which this environment
cannot provide, so it was **not** run. Exact steps for a human:

**Environment required:** Valheim 1.0.16 with BepInEx pack 5.4.2350, Jötunn 2.30.2, and
MultiUserChest 0.6.2, on both server and client (Item Drawers 1.4.0 optional — soft
dependency). Also install **BepInEx ConfigurationManager** on the server, since the check
below edits config through its window.

1. **Build the DLL.**
   `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
   → `CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll`.

2. **Deploy the same build to the server and the client.** Copy it into each
   `BepInEx\plugins\`, alongside Jötunn and MultiUserChest.

3. **Start the server, then launch the client and join.** On first run the plugin writes its
   config file and Jötunn registers it for synchronisation. Open the ConfigurationManager
   window (default `F1`) on the **server**.

4. **Confirm the entries exist and are grouped** under `1 - Server`, `2 - Keys` and
   `9 - Debug`, with no entry for a ZDO/"NoPull" key.

5. **Change `Maximum range` (the `maxRange` entry) on the server**, e.g. to `20`, through the
   ConfigurationManager window. **Restart the server** (or trigger a synchronisation) so the
   new value is pushed.

6. **Set a *different* local value on the client.** With the client **not** connected,
   hand-edit the client's own config file (`BepInEx\config\NorCal_Nerds.CraftFromDrawersAndChests.cfg`)
   and set `Maximum range` to something obviously different, e.g. `1`.

7. **Join the server from that client.** Expected:
   - the client's effective `Maximum range` is the **server's** value (`20`), not its local
     `1` — i.e. the mod pulls from storage up to 20× the expected distance;
   - in the client's ConfigurationManager window, the entry is **read-only/locked** because
     the client is not an admin, and any attempt to change it locally does not change the
     behaviour.

8. **Negative check — server-authority, not client-authority.** Log in as a **non-admin**
   client and try to lower `Maximum range` locally; confirm the client still behaves with the
   server's value. Then promote the client to admin (server-config admin list), change the
   value, and confirm the change is what the server accepts.

9. **Regression guard.** The BepInEx log shows no error from config binding or from Jötunn's
   synchronisation, and the plugin still loads cleanly.

> **Note for the author:** step 7/8 assert the *server-authoritative* behaviour. That depends
> on every entry being marked admin-only (§7 concern 1), which this implementation does. If
> the entry turns out **not** to be locked for the non-admin client, that is the signal to
> revisit that decision.

---

## 7. Self-review findings

- **No literal duplicates a `ConfigModel` constant.** Mechanical check: extracted all 33
  `public const string` values from `ConfigModel.cs` and searched `PluginConfig.cs` for each
  as a quoted literal → **0 duplicates**. The only string literals in `PluginConfig.cs` are
  the descriptions and the defaults `ConfigModel` does not define (`"left ctrl"`,
  `"{0}/{1}"`, `"Pulled items to inventory"`, `"RoundLog,FineWood"`).
- **Every entry binds via a constant.** 27 `Bind(config, …)` calls — `SectionServer` ×21,
  `SectionKeys` ×5, `SectionDebug` ×1. Searching for `Bind(config, "` (a bare quoted
  section/key) returns nothing; the section and key arguments are always
  `ConfigModel.*`.
- **No ZDO entry.** `ZdoKey` occurs 0 times in `PluginConfig.cs` (Ruling 5).
- **`ReadInto` is the only `FeatureFlags` writer.** A repo-wide search for
  `new FeatureFlags` / `Flags.<Member> =` returns only `PluginConfig.cs`: the instance is
  created in `Init` (`Flags = new FeatureFlags()`) and every member is assigned only inside
  `ReadInto`.
- **No overbuilding.** No Harmony patch, no game-facing behaviour, no behaviour for the three
  reinstated keys. `Logic/**` is untouched. No Nexus-flavoured artefact and no other mod's
  identity, feature description, path or URL appears in the new file.
- **Build and test output pristine** apart from the pre-existing `MSB3274` (§3).
- **`Awake` order is correct** — `Init` then `ReadInto` then `PatchAll`, so no patch can
  observe an unfilled snapshot (Ruling 4/15).

---

## 8. Concerns

1. **`IsAdminOnly` was added (the main judgment call).** Jötunn's own documentation for
   `SynchronizationManager.RegisterCustomConfig` states: *"Registers a non default config file
   for possible synchronisation with all clients. **Entries still need the IsAdminOnly
   attribute in order to be synchronized.**"* — so registering the file alone does not make
   anything synchronised. To satisfy the design's G5 ("settings live on the server … and are
   pushed down to clients") and the global constraint's "**non-admins cannot change them**",
   every entry is bound through a small `Bind<T>` helper that adds
   `new ConfigurationManagerAttributes { IsAdminOnly = true }`
   (`ConfigurationManagerAttributes` is shipped by Jötunn itself; `IsAdminOnly` confirmed
   present in `Jotunn.xml`). **The brief and the rulings mention only
   `RegisterCustomConfig`.** If the reviewer considers this scope creep, the change is
   contained to the single private `Bind<T>` helper and can be removed without touching
   anything else — but removing it leaves the sync non-functional.

2. **Defaults for the settings `ConfigModel` does not pin come from CraftFromContainers
   4.0.7**, which the design names as the behavioural baseline. That checkout happens to be on
   this machine at
   `C:\Users\merlin\Documents\Repos\storagework\ValheimMods\CraftFromContainers\BepInExPlugin.cs`,
   and I read its `Config.Bind` calls directly: `pullItemsKey = "left ctrl"`,
   `resourceString = "{0}/{1}"`, `pulledMessage = "Pulled items to inventory"`,
   `fuelDisallowTypes`/`oreDisallowTypes = "RoundLog,FineWood"`,
   `flashColor = Color.yellow`, `unFlashColor = Color.white`, `pullFuel/PullOre/PullCooking =
   true`, all six `ignore*` = `false`, `modEnabled = true`, `isDebug = true`. `ConfigModel`'s
   `Default*` values deliberately override the CFC value where they exist (notably
   `maxRange` 4 instead of 10). These literal defaults are **not** `ConfigModel` constants, so
   they cannot be "re-typed" from it; per the task's self-review wording the test is "no
   string literal duplicates a `ConfigModel` constant", which holds. A reviewer wanting the
   remaining defaults to live in `ConfigModel` would require editing a Task 5 file, which is
   outside this task's file list, so I did not.

3. **`toggleStorageKey` default is `string.Empty`** — the key has no CraftFromContainers
   ancestor (it is new, for G4), and the AzuCraftyBoxes source the design references is not on
   this machine and its GitHub repo could not be reached from here, so I could not copy its
   default. Empty means the toggle is unbound until the author sets a key. **Author decision
   point**; flagging so it is not mistaken for an omission.

4. **`isDebug` defaults to `true`** (CraftFromContainers parity). That is louder than many
   would choose for a new mod; the author may prefer `false`. Left at parity pending guidance.

5. **`File` and `Flags` are public mutable fields**, matching the brief's Interfaces block
   verbatim (`public static ConfigFile File`, `public static FeatureFlags Flags`). Nothing
   prevents a later task from clobbering them; a `{ get; private set; }` would be safer but
   would deviate from the specified signature.

6. **No unit test covers `PluginConfig`.** It is game-facing, and `Tests` links only
   `Logic/**`, so the test count is unchanged at 41. Behaviour is therefore covered only by
   the in-game checklist (§6) and by the compiler — this is inherent to the task's split, not
   a gap, but it means a typo in a default would not be caught until runtime.

---

# Task 8 — Fix round (post-review) — Report

**Fix commit:** `72b4a8b` — `fix: refresh config snapshot on Jotunn configuration sync`
**Status:** DONE

Three findings from the task review were fixed: the important snapshot-refresh gap, plus two
documentation corrections. No new wiring, patches or game-facing behaviour was added beyond
the listed fixes.

## F1 (important) — the snapshot was never re-read after the server's values arrived

`PluginConfig.ReadInto` ran exactly once, from `Plugin.Awake`. Jötunn pushes the *server's*
config into the `ConfigEntry` objects after `Awake` (on client connect), so `Flags` kept the
values from the client's own local file. The subscriptions were added in
`CraftFromDrawersAndChests/PluginConfig.cs`:

- `Init` now subscribes `RefreshFlags` to Jötunn's **static** event
  `SynchronizationManager.OnConfigurationSynchronized` (confirmed against the shipped
  `Jotunn.xml` and by decompiling `Jotunn.dll`: `System.EventHandler<Jotunn.Utils.ConfigurationSynchronizationEventArgs>`;
  the event backing field is `static`).
- `RefreshFlags(object, ConfigurationSynchronizationEventArgs)` calls `ReadInto(Flags)`, so
  the entries Jötunn has just overwritten are re-copied into the shared snapshot.

`Flags` is created in `Init` (so it is never null once subscribed) and every property is
assigned synchronously inside `ReadInto` (no `await`, no yielding), so a patch can never
observe a null or half-populated snapshot.

The subscription was placed **before** the `RegisterCustomConfig` call rather than after it.
The two calls are order-independent for Jötunn (it cannot raise the event during the
synchronous registration), but wiring the refresh independently of registration means the
handler is established even if registration throws — and it makes the shipped `Init` path
directly verifiable end-to-end (see Verification below).

## F2 (minor) — `Plugin.cs` `Awake` doc comment was false

The `<see cref="Log"/>` comment claimed `Awake` "stays limited to exactly three actions".
`Awake` now also runs `PluginConfig.Init` and `PluginConfig.ReadInto`. The comment now
describes the real fixed start-up sequence (assign `Instance`, create `Harmony`, initialise
and read the configuration, then `PatchAll`).

## F3 (minor) — `Logic/` named a class that no longer exists

`Logic/FeatureFlags.cs` and `Logic/ConfigModel.cs` referred to `<c>Config</c>` /
`<c>Config.ReadInto</c>`. The config class is `PluginConfig`. The three doc references were
updated to `PluginConfig` / `PluginConfig.ReadInto`. Comments only — `Logic/**` behaviour is
unchanged and still references no `UnityEngine`, `Valheim`, `BepInEx` or `Jötunn` (a search
for those tokens in `Logic/**` returns only pre-existing doc prose, never code).

## Verification

### Build

```
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug
```

```
  CraftFromDrawersAndChests -> ...\CraftFromDrawersAndChests\bin\Debug\CraftFromDrawersAndChests.dll
    Built ...\CraftFromDrawersAndChests.dll (23,552 bytes)

BUILD OK - CraftFromDrawersAndChests 0.1.0 (Debug)
```

Matches the expected line. Only the expected `MSB3274` warning (MultiUserChest `net472` vs
this project's `net462`). The DLL grew 23,040 → 23,552 bytes.

### Tests

```
dotnet test Tests\CraftFromDrawersAndChests.Tests.csproj
```

```
Test summary: total: 41, failed: 0, succeeded: 41, skipped: 0, duration: 1.9s
```

41 passing, 0 failing.

### How a post-sync refresh was shown to reach `Flags`

`PluginConfig` is game-facing (`BepInEx`/`UnityEngine`/`Jötunn`), so it cannot be exercised by
the `Logic`-only `Tests` project. It was instead driven directly, in a child PowerShell process
(`%TEMP%\verify_sync_refresh.ps1`, no repo artifacts), by loading the **built**
`bin\Debug\CraftFromDrawersAndChests.dll` plus `BepInEx.dll` and `Jotunn.dll` through an
`AppDomain.AssemblyResolve` probe. The harness set `BepInEx.Paths.ConfigPath` to a temp dir,
created a `BepInEx.Configuration.ConfigFile` there, and invoked `PluginConfig.Init(config)`
via reflection. Output:

```
Subscribed handlers: [PluginConfig.RefreshFlags]
Flags non-null after Init: True
Flags.MaxRange before: 0
Flags.MaxRange after RefreshFlags: 9.5
Flags.MaxRange after raising event: 12.25
FeatureFlags properties: 25; null string props: []
```

Reading the lines in order:

1. **The event is wired to the handler.** After `Init` ran, the invocation list of
   `SynchronizationManager.OnConfigurationSynchronized` contained exactly `PluginConfig.RefreshFlags`.
2. **`Flags` is never null.** `Flags` was non-null immediately after `Init`, and none of the
   25 `FeatureFlags` string properties were null — no half-populated snapshot.
3. **Refreshing re-reads the entries into `Flags`.** Setting `maxRange.Value = 9.5` and
   invoking `RefreshFlags` moved `Flags.MaxRange` from `0` to `9.5`.
4. **The real event reaches `Flags`.** Setting `maxRange.Value = 12.25` and then invoking the
   actual event delegate (`SynchronizationManager.OnConfigurationSynchronized`, i.e. exactly
   what Jötunn does when the server's values arrive) moved `Flags.MaxRange` to `12.25`.

Together: raising Jötunn's `OnConfigurationSynchronized` → `RefreshFlags` → `ReadInto(Flags)`,
so a client that connects and then reads `Flags` sees the values Jötunn wrote into the entries.

> The harness runs outside a booted BepInEx, so `RegisterCustomConfig` itself cannot complete
> (`BepInEx.Bootstrap.Chainloader`'s static initialiser needs the real BepInEx root) — the
> harness reports `Init completed cleanly: False` for that reason. Every part of `Init` that
> this fix concerns — the binding, the `Flags` creation and the **event subscription** — runs
> before that call and is what the four lines above prove. In the game BepInEx is bootstrapped,
> so the registration completes normally.

## Files changed

| File | Change |
|---|---|
| `CraftFromDrawersAndChests/PluginConfig.cs` | added `using Jotunn.Utils;`, the `OnConfigurationSynchronized` subscription in `Init`, the `RefreshFlags` handler, and updated the class/`ReadInto` doc remarks |
| `CraftFromDrawersAndChests/Plugin.cs` | `Awake` doc comment corrected |
| `CraftFromDrawersAndChests/Logic/FeatureFlags.cs` | two doc references `Config` → `PluginConfig` |
| `CraftFromDrawersAndChests/Logic/ConfigModel.cs` | one doc reference `Config` → `PluginConfig` |

Commit `72b4a8b` — 4 files changed, 31 insertions(+), 8 deletions(-). No csproj change was
needed (no new source file). `.superpowers/` is gitignored (`.gitignore:14`), so this report
is not part of any commit, as before.
