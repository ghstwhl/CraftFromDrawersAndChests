### Task 10: Chest registry and vanilla source

**Files:**
- Create: `CraftFromDrawersAndChests/Storage/VanillaChestSource.cs`, `CraftFromDrawersAndChests/Storage/StorageRegistry.cs`, `CraftFromDrawersAndChests/Storage/ContainerTrackingPatches.cs`

**Interfaces:**
- Consumes: `IStorageSource` (Task 9), `Eligibility` (Task 3).
- Produces: `static class StorageRegistry` with `static IReadOnlyList<IStorageSource> Collect(Vec3 origin, FeatureFlags flags)` and `static void Clear()`. `ContainerTrackingPatches` maintains the tracked `Container` set.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — files missing.

- [ ] **Step 2: Implement the three files**

`ContainerTrackingPatches` Harmony-postfixes `Container.Awake` to add the instance to a static list and `Container.OnDestroyed` to remove it. `VanillaChestSource.Count(itemName)` sums `GetInventory().CountItems(itemName)`. `VanillaChestSource.Take` is implemented in Task 11 — for now it throws `NotImplementedException`. `StorageRegistry.Collect` builds a `StorageCandidate` per tracked chest and per `DrawerApi.AllInRange` handle, runs `Eligibility.IsEligible`, and returns the surviving sources cast to `IStorageSource`.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game**

Place two chests, one inside and one outside the configured range, and a drawer inside it. Add temporary debug logging in `Collect` printing each source's name, kind and distance.
Expected: the far chest is absent from the list; the near chest and the drawer are present; no exceptions with zero chests in the world.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/Storage
git commit -m "feat: chest registry and storage collection"
```

---

