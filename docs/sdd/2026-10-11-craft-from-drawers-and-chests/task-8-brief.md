### Task 8: Server-authoritative config

**Files:**
- Create: `CraftFromDrawersAndChests/Config.cs`
- Modify: `CraftFromDrawersAndChests/Plugin.cs` — call `Config.Init(Config)` from `Awake`

**Interfaces:**
- Consumes: `ConfigModel` (Task 5), `Plugin` (Task 7).
- Produces: `static class Config` with `public static ConfigFile File`, `public static FeatureFlags Flags`, `public static void Init(ConfigFile config)`, and `public static void ReadInto(FeatureFlags flags)` which copies every `ConfigEntry` value into the flags object.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — `Config.cs` missing.

- [ ] **Step 2: Implement `Config.cs`**

Bind one `ConfigEntry` per constant in `ConfigModel` using the section and key names from `ConfigModel` (never string literals written here), then call `SynchronizationManager.Instance.RegisterCustomConfig(config)`. Types: `maxRange` float; the two key settings and the two disallow-type lists strings; flash colours `ConfigEntry<Color>`; everything else bool. `ReadInto` assigns each entry into the matching `FeatureFlags` property.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game**

On the server, edit `maxRange` through the ConfigurationManager window and restart. Join from a client with a *different* local value in its own config file.
Expected: the client behaves with the server's value; the entry is read-only for the non-admin client.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Config.cs CraftFromDrawersAndChests/Plugin.cs
git commit -m "feat: server-synced configuration"
```

---

