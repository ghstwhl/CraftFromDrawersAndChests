### Task 11: MultiUserChest removal bridge

**Files:**
- Create: `CraftFromDrawersAndChests/MultiUserChestBridge.cs`
- Modify: `CraftFromDrawersAndChests/Storage/VanillaChestSource.cs` — implement `Take`

**Interfaces:**
- Consumes: `ContainerHandler` from `MultiUserChest.dll`.
- Produces: `static class MultiUserChestBridge` with `static int RemoveFromChest(Container chest, ItemDrop.ItemData item, Inventory destination, int amount)` returning how many were actually removed.

- [ ] **Step 1: Write the failing check**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: FAIL — `MultiUserChestBridge.cs` missing.

- [ ] **Step 2: Implement the bridge**

If `chest.m_nview.HasOwner()` is false, call `chest.m_nview.ClaimOwnership()` first (a closed chest has nobody to route to). Then call `ContainerHandler.RemoveItemFromChest(chest, item, destination, to, sender, amount)`, convert the returned `RequestChestRemove` to its response the way `InventoryHandler` does, and return the number of items that ended up removed. Never touch `chest.GetInventory()` directly. `VanillaChestSource.Take` locates the `ItemDrop.ItemData` for `itemName` in the chest, calls the bridge, and returns its result.

- [ ] **Step 3: Build**

Run: `powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug`
Expected: `BUILD OK`.

- [ ] **Step 4: Verify in game with a second player**

Have a second player open the chest and leave it open. Trigger a manual debug call that removes 1 item from it.
Expected: the item moves exactly once (Review Focus 3); both clients still show consistent chest contents; the second player sees the change.

- [ ] **Step 5: Commit**

```bash
git add CraftFromDrawersAndChests/MultiUserChestBridge.cs CraftFromDrawersAndChests/Storage/VanillaChestSource.cs
git commit -m "feat: route chest removal through MultiUserChest"
```

---

