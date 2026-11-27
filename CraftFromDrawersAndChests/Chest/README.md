# `Chest/` — the absorbed MultiUserChest layer

Everything in this folder is **upstream MultiUserChest 0.6.2**, Copyright © 2022 Maximilian Schmöcker,
released under the MIT licence (shipped as [`LICENSE-MULTIUSERCHEST.txt`](../LICENSE-MULTIUSERCHEST.txt)).
It is compiled into this mod rather than required as a separate plugin so that the chest layer's behaviour
can be observed with this mod's own `Debug` setting and, when it misbehaves, fixed here.

Upstream: <https://github.com/MSchmoecker/MultiUserChest> (plugin GUID `com.maxsch.valheim.MultiUserChest`).
Absorbed: 2026-11-27, from a fresh clone of 0.6.2.

## Rules for editing anything in here

- **Only change what has a reason written down below.** This is a vendored copy, not our code; every
  deviation is a thing we can no longer compare against upstream.
- **Wire format and save data are frozen.** The ten `MUC_*` ZRoutedRpc route names and the `MUC_Ignore`
  ZDO flag are network and save-file data. Renaming any of them breaks interoperability with a peer's
  saved world or any other mod that reads the flag.
- **This mod refuses to load when the real MultiUserChest is installed** (`[BepInIncompatibility]` in
  `Plugin.cs`). Both patch `Inventory.AddItem`/`RemoveItem` and `Container.RPC_RequestOpen` and both
  register the same routes.
- Everything decision-shaped that we touch belongs in `Logic/` with a table test, not inline in a patch.

## File map (upstream name → here)

Namespaces were rewritten from `MultiUserChest` to `CraftFromDrawersAndChests.Chest` (and
`MultiUserChest.Patches` to `CraftFromDrawersAndChests.Chest.Patches`); file names and folder structure are
unchanged so a future diff against upstream stays readable.

| Upstream | Here | Note |
|---|---|---|
| `Plugin.cs` | `ChestPatches.cs` | rewritten, see below |
| `Properties/AssemblyInfo.cs` | — | not absorbed (version attributes, `InternalsVisibleTo`) |
| everything else | same path | namespace rewrite only, except where listed below |

## Divergence register

| File | Change | Why |
|---|---|---|
| `ChestPatches.cs` | Replaces `Plugin : BaseUnityPlugin`. Keeps the upstream identity as constants, drops the `[BepInPlugin]`/`[BepInDependency]`/`[NetworkCompatibility]` attributes, and exposes `InitialiseLogging` + `ApplyCompatPatches`. | A second `[BepInPlugin]` in our assembly would make BepInEx treat the assembly as two plugins. The plugin's own attributes are ours now: Jötunn is already a hard dependency and version enforcement is already declared. |
| `Helper/Log.cs` | `LogDebug`/`LogCodeInstruction` are compiled in and gated on a runtime probe (`SetDebugProbe`, `IsDebug`) instead of `#if DEBUG`; they log at **Info** level. | Upstream's verbose output existed only in a debug build, which its changelog calls slow. BepInEx's default `[Logging.Disk]` levels exclude Debug, so a `LogDebug` line would never reach the file a support report sends us. |
| `Helper/Timer.cs` | Compiled in (upstream wrapped the whole class in `#if DEBUG`); both methods return early unless `Log.IsDebug`. | Keeps the call sites unguarded and makes round-trip timing available in a release build. |
| `Data/IPackage.cs` + the ten `Data/Request*.cs` | `PrintDebug()` declared and implemented unconditionally. | So the per-request field dump can be turned on at runtime. |
| `PackageHandler.cs`, `Patches/InventoryPatch.cs`, `Patches/PickupPatch.cs`, `InventoryHandler.cs`, `Patches/GamePatches.cs` | `#if DEBUG` guards removed around logging and `Timer` calls. `PackageHandler`'s `total` counter is now maintained in every build. | Same reason: the logging underneath already gates itself. |
| `ContainerRPCHandler.cs` | The "not handling RPC: instance is null / is not owner" warnings are always on (upstream compiled them out of Release). | They fire only when a request reaches a peer that cannot serve it — the event that precedes a stuck chest. Expect them in a log as an anomaly, not as routine noise. |
| `Patches/ContainerPatch.cs` | Both open prefixes (`RPC_RequestOpen`, `RPC_RequestStack`) go through one `HandleOpenRequest` that asks `Logic.ChestOpenRules.Decide` and logs every input when `Debug` is on. The unused `IsContainerInUse` helper was removed with it. **2.1.0:** the branch is chosen by **custody** (does the ZDO have an explicit owner) instead of by upstream's in-use flag, so a claimed container is never handed over. | The four branches decide whether the requester ends up owning the container; upstream had them as a chain of early returns that nothing could test. `Tests/Logic/ChestOpenRulesTests.cs` pins every row. The custody change is the 2.1.0 fix — see the changelog for the log evidence. |
| `Patches/ContainerPatch.cs` | `m_nview` is null-checked (`nview && nview.IsOwner()`) instead of dereferenced. | Upstream would throw a null-reference if `Container.Awake` had not run yet; a null view now falls through to vanilla's own answer. |
| *carried over* | `[BepInIncompatibility]` for `aedenthorn.QuickStore`, `aedenthorn.SimpleSort` and `org.bepinex.plugins.valheim.quick_stack` moved onto our plugin class. | Upstream declared them; its inventory interception assumes those quick-move mods are absent. |

## Not absorbed

- **The Unity test project** (`MultiUserChestUnity/Assets/Tests`, 24 files). It is a Unity 2022.3 Test
  Framework assembly whose `Helper.cs` boots a real `Game`/`ZNetScene`/`ZNetView` and intercepts
  `ZRoutedRpc`, so it cannot run under `dotnet test`. The pure decisions we touch are covered instead by
  `Tests/Logic/ChestOpenRulesTests.cs`.
- **`Properties/AssemblyInfo.cs`** — version attributes and `InternalsVisibleTo("Tests")`.

## Known upstream behaviour we are now responsible for

Upstream's in-use branch answered a request for a container another player had open "you may open" and
returned **without** `ForceSendZDO`/`SetOwner`, while every other request moved ownership. Because our
availability arithmetic treats a container owned by another player as unusable, that made opening a chest
change which player could count it. **2.1.0 replaces the discriminator**: `Decide` now hands a container
over only when nobody has claimed it (`ZDO.GetOwner() == 0`), so a claimed container keeps its owner and a
second opener is granted access without taking it. The trade is documented in `CHANGELOG.md` under
2.1.0: the non-owner can open and move items (routed to the owner) but does not count that chest in their
own crafting arithmetic.
