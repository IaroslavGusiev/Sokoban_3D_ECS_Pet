# ECS-driven asynchronous views

This records the preceding migration. The current startup gate and failed-cache retry behavior supersede the immediate-Playing and retained-failure policy below; see [STARTUP-GATE.md](STARTUP-GATE.md) and [ARCHITECTURE.md](ARCHITECTURE.md).

Implemented and verified on 2026-10-22. No commits were made, following the explicit clarification. The existing UniTask installation and Addressables exit-handle fix were retained. Shared prefab-load failures log once per cached kind, following the explicit logging clarification.

## Steps and checks

1. Renamed only the four Core test source namespaces to Sokoban.CoreTests. Added the diagonal-only edge case. LevelSolver now returns moves and the independently calculated player position after every move; replay asserts every intermediate player position. Removed the stale EntityView session comment. Compilation and 164 EditMode cases passed.
2. Added FloorTag and a floor entity at every rectangle cell, including wall cells. GoalTag is on the corresponding floor entity; walls, boxes, and player remain separate. Updated entity-count assertions and verified floor/goal membership and unchanged solid/player counts. The old LevelView temporarily skipped floor-only entities and kept the same visuals. Compilation, 164 EditMode cases, and saved-scene PlayMode checks passed. One old post-disposal count was corrected and its test rerun.
3. Replaced synchronous view building and per-level prefab ownership with ECS view binding and an application-lifetime provider. Core source hashes matched the step-2 snapshot throughout this step. Compilation, the 30 migrated PlayMode cases, and six new binding/cache cases passed.
4. Removed the redundant ComponentAdapter duplicate-component guard, updated its tests, and audited the retained checks below. Final compilation, all 164 EditMode cases, and all 36 PlayMode cases passed.

The six added cases cover a single pending request per entity, latest-position placement and prefab rotation, untagged-entity exclusion, a late result after world disposal, optional EntityView and adapter-before-transform ordering, mapping priority, concurrent shared waits with caller-only cancellation, and one logged failure across multiple entities and runs. Some cases cover several assertions.

The two movement system files were not edited. Assembly names, asmdef files, existing file names, and existing metadata identities were preserved. Unity imported metadata for new scripts and removed the three obsolete scripts and their metadata through AssetDatabase. No scene, prefab, asset, or metadata file was edited by hand.

## Runtime ownership and flow

Core remains synchronous and Unity-independent. The compiled Core assembly references only netstandard and DefaultEcs.

Every floor, goal floor, wall, box, and player follows:
needs view -> ViewPending -> TransformComponent, or ViewFailed.

ViewBindingSystem uses a buffered DefaultEcs 0.17.2 query with WithEither/Or for the five tags. It requires GridPosition and excludes ViewPending, ViewFailed, and TransformComponent. ViewKindMap checks Wall, Box, Player, Goal, Floor in that order. It sets ViewPending before starting BindAsync. The async operation awaits the run token, checks cancellation before any entity access after the await, instantiates at the entity's current grid position with the prefab rotation, installs optional adapters, sets the authoritative TransformComponent with entity.Set, and removes ViewPending.

Load failures are logged by ViewAssets once when its cached operation completes unsuccessfully. Each waiting entity independently becomes ViewFailed and loses ViewPending; the binding catch does not log the same shared load failure again. Failed handles remain cached until provider disposal, so a new run cannot retry or repeat the load log. Exceptions during instantiation or adapter installation are logged by the binding operation, clean up its partial view, and mark that entity failed. There is no automatic retry.

ViewLayer owns its root, cancellation source, and ordered presentation pipeline: ViewBindingFeature, then TransformSyncFeature. It cancels first, disables/destroys its root, disposes the pipeline, and disposes its cancellation source. Construction failure cleans up acquired resources before rethrowing. The controller disposes this layer before Simulation disposes the world.

StartRun performs one initial presentation tick to request views immediately. Instantiation itself supplies the final position; placement does not depend on a zero-delta transform update. Cached successful handles resolve synchronously, so R can show all replacement views before returning. Initial cold loads can complete over several frames while simulation is already Playing.

GameplayController owns the current LevelDefinition, simulation, layers, and controller lifetime token. ILevelSource now returns only a parsed LevelDefinition. Next awaits its text first, then replaces the old run. R uses the current definition without awaiting or loading. Both keep the same application-lifetime IViewProvider.

ServicesInstaller validates and binds the existing catalog/list/root/input/source plus BindInterfacesAndSelfTo<ViewAssets>().AsSingle(). FeaturesInstaller and GameplayInstaller remain. Provider disposal belongs to Zenject; the controller never disposes the provider.

## Cache, cancellation, and release while loading

ViewAssets starts no loads in its constructor. The first LoadAsync for a kind creates exactly one Addressables handle and stores it in its dictionary. Every caller builds its own UniTask waiter over that same handle and uses AttachExternalCancellation for its own token. Cancelling a caller cancels only that wait; it neither cancels the operation nor releases the shared handle. No shared single-consumer UniTask is reused concurrently.

Release while loading uses the previously verified immediate-owner-release approach: Dispose releases each created, still-valid handle without waiting for the underlying operation. Addressables retains its internal execution reference until completion and performs final destruction when references reach zero. UniTask auto-release is disabled. A failed kind also retains its single cached handle until application teardown.

The IsValid checks remain at provider disposal and the temporary text-handle finally block. Addressables 4.1.1 can invalidate handles on ExitingPlayMode before Zenject OnDestroy. These explicit, approved checks prevent a second release of an already-invalidated operation. No disposed flags or idempotence contract were introduced.

One owned handle per kind does not mean every transient internal reference count is always one: Addressables can retain an internal completion reference while invoking callbacks. Settled runtime counts are one per kind.

## Guard audit

Removed or eliminated:

| Check | Outcome and reason |
| --- | --- |
| ComponentAdapter<T>.Install entity.Has<T>() | Removed. Installation uses Set and can replace the component; the binder sets the view transform after adapters. |
| LevelView's required EntityView check | Eliminated with LevelView. TryGetComponent now implements the explicitly optional adapter path. |
| Controller's conditional content disposal / unadopted-content cleanup | Eliminated with LevelContent. Definitions contain no disposable view resources. |
| Static preload factory failure-release loop | Eliminated with the preload factory. Cache ownership lasts until provider disposal, including failed or still-loading handles. |

Retained checks and branches:

| Location | Reason |
| --- | --- |
| GameplayController.Tick Playing check | Stops ticking and input while a level definition is loading. |
| GameplayController post-await token check | Prevents adopting a result after controller cancellation. |
| AddressableLevelSource and ViewAssets initial token checks | Avoid starting work for a cancelled caller. |
| ViewBindingSystem post-await and failure-path token checks | No entity access after a run ends; late success/failure cannot mutate a disposed world. |
| ViewAssets.Dispose and AddressableLevelSource finally IsValid checks | Explicitly approved Editor shutdown fix. |
| GridTransformSyncSystem transform == null | Unity may independently destroy a view transform. |
| ViewLayer root != null | Scene shutdown may independently destroy the root. |
| EntityView adapter == null | Serialized Unity-object references can be missing or independently destroyed; fail loudly. |
| Binding partial view != null | Destroys an instantiated Unity object after an installation failure. |
| Binding TryGetComponent | Implements optional EntityView as requested. |
| ViewAssets TryGetValue | Starts one load on cache miss; reuses the cached handle on hit. |
| ViewAssets IsDone / failed-status branches | Logs already-completed failures immediately or subscribes once to completion. |
| ViewKindMap fallback exception | Invalid tag input fails loudly; the binding query restricts its inputs to viewable entities. |
| ViewCatalog and LevelList validation | External serialized data: missing, duplicate, invalid references, and empty lists. |
| KeyboardMoveInput's three no-keyboard branches | External input availability: no attached keyboard produces no move/restart/next input. These managed-device checks are part of normal input handling. |
| Simulation null-definition check | Public Core argument validation. |
| LevelDefinition positive dimensions and nonnull collections | Public Core argument validation. Its null-coalescing operators apply to managed collections. |
| LevelParser null/empty input, blank/unequal rows, symbol, player/box/goal count, enclosure checks | External text validation; four-direction enclosure deliberately ignores diagonal adjacency. |
| MovementPlan index/step-count validation | Public data-boundary validation and movement-plan shape rules. |
| Simulation command and already-pending-request checks | Real command rules: valid cardinal commands, first request wins. |
| MovementValidationSystem request/player-count, direction, offset/overflow, wall, box, push-target checks | Existing deterministic movement rules; the file is unchanged. |
| ApplyMovementSystem plan shape, distinct participants, world/liveness/enabled/component/source-position checks | Existing atomic-preflight domain rules; the file is unchanged. No new entity.IsAlive or system IsEnabled guard was added. |
| Floor goal-membership check | Selects GoalTag on the same floor entity; normal population logic. |
| R/N branches and tag-priority cases | Requested behavior and mapping decisions, rather than defensive lifecycle checks. |
| Construction/disposal try/catch/finally cleanup | Acquired resources are cleaned up before errors propagate; no errors are swallowed as a lifecycle workaround. |

No _disposed, ThrowIfDisposed, initialized flag, entity.IsAlive check, or system-local IsEnabled check was added. There is no null-coalescing or null-conditional operator on UnityEngine.Object in runtime code.

## Adapter inventory

- EntityAdapter: abstract MonoBehaviour declaring Install(Entity).
- ComponentAdapter<T>: generic struct adapter; installs its serialized value using entity.Set.
- TransformAdapter: the only concrete adapter, specializes ComponentAdapter<TransformComponent>. Reset captures its transform in the serialized preset.
- EntityView: optional root component; installs its assigned adapters.

TransformAdapter still sets TransformComponent. This is redundant and harmless: the binder then sets that component to the new view's transform. Removing TransformAdapter from prefab roots is optional. The adapter architecture remains available for later Unity-side components.

## Assembly and friendship audit

The full list of all 20 source/vendored asmdefs and the resolved-package snapshot remains in [ASSEMBLIES.md](ASSEMBLIES.md). No assembly or asmdef was renamed.

| Core friend | Outcome | Evidence |
| --- | --- | --- |
| Sokoban.EditModeTests | Kept | Direct construction of internal MovementValidationSystem and ApplyMovementSystem. |
| Sokoban.App.EditModeTests | Kept | Internal Simulation feature-list constructor and internal MovementFeature in pipeline/order/cleanup tests. |
| Sokoban.PlayModeTests | Removed | Assembly exists but now exercises public Core APIs only. All 36 cases compile and pass without friendship. |

No nonexistent friend entries were present. Sokoban.Core.Tests remains independent and uses only public Core APIs; it was never added as a friend.

## File manifest

Paths below are relative to Sokoban/Assets/_Sokoban.

Added source:

- Code/Core/Components/FloorTag.cs
- Code/Unity/IViewProvider.cs
- Code/Unity/ViewContext.cs
- Code/Unity/ViewKindMap.cs
- Code/Unity/ViewBindingFeature.cs
- Code/Unity/Ecs/Systems/ViewBindingSystem.cs
- Code/Unity/Ecs/Components/ViewPending.cs
- Code/Unity/Ecs/Components/ViewFailed.cs
- Tests/PlayMode/ViewBindingTests.cs

Changed source:

- Code/Core/AssemblyInfo.cs
- Code/Core/LevelLoader.cs
- Code/Unity/AddressableLevelSource.cs
- Code/Unity/ILevelSource.cs
- Code/Unity/ViewAssets.cs
- Code/Unity/ViewLayer.cs
- Code/Unity/ViewFeatures.cs
- Code/Unity/GameplayController.cs
- Code/Unity/Installers/ServicesInstaller.cs
- Code/Unity/Ecs/Model/EntityView.cs
- Code/Unity/Ecs/Model/ComponentAdapter.cs
- Tests/Core/LevelFiles.cs
- Tests/Core/LevelFilesTests.cs
- Tests/Core/LevelParserTests.cs
- Tests/Core/LevelSolver.cs
- Tests/EditMode/Application/SimulationTests.cs
- Tests/PlayMode/HardcodedLevelTests.cs
- Tests/PlayMode/PresentationPipelineTests.cs

Deleted through Unity AssetDatabase:

- Code/Unity/IViewAssets.cs and its metadata
- Code/Unity/LevelContent.cs and its metadata
- Code/Unity/LevelView.cs and its metadata

Documentation: updated ARCHITECTURE.md, ASSEMBLIES.md, and ASYNC-CONTENT.md; added this report. New-script metadata was generated by Unity. No packages, assembly definitions, prefabs, scenes, or serialized assets were changed by this task.

## Verification and manual Editor steps

Final checks:

- Compilation succeeded after each step.
- 164 EditMode and 36 PlayMode cases passed. Results: Logs/ecs-views-final-editor.json and Logs/ecs-views-final-playmode.json.
- Real saved-scene initial run: 76 entities and 76 views; five cached handles, settled reference counts all one.
- R: fresh simulation, all 76 views available, identical cached handles.
- N: indices 0 -> 1 -> 2 -> 0; entity/view counts 76 -> 78 -> 98 -> 76; identical five handles and settled counts one throughout.
- Real Play Mode exit after those switches produced no new errors.
- Temporary input settings/devices were restored; GameplayScene is clean and left outside Play Mode.
- No standalone player build or package changes. The existing local Addressables content remained usable; script/API changes required no authoring reassignment.

No asset migration is required. Optionally open GameplayScene, confirm the same ServicesInstaller references, and save it. Optionally remove redundant TransformAdapter components through Prefab Mode; keep EntityView and other adapters as needed. Do not remove all adapters as a requirement.

Manual repeat checklist:

1. Enter Play Mode. The first level should appear over a few frames while the controller is already Playing.
2. Move/push into open cells and walls. R should reset the run immediately using the cached prefabs.
3. Press N through all three levels and wrap to the first. Prefabs should remain loaded.
4. Inspect the five owned handles while switching/restarting and on exit. Addressables 4.1.1 uses the Addressable Assets Profiler module; it has no legacy Event Viewer menu. Debug Build Layout and Use Existing Build remain configured. The known package Profiler detail-view layout error can obstruct the UI; runtime handle checks and a prior recording are documented in ASYNC-CONTENT.md.
5. For a failure check, change a catalog entry before entering Play Mode to a valid-format key that cannot resolve. Keep Log Runtime Exceptions disabled to avoid the package's additional logger. Expect one load exception for that kind; all affected entities get ViewFailed, lose ViewPending, and do not retry. R/N should not log the cached failure again. Exit releases the failed handle as well as successful ones. Restore the catalog entry afterward.
6. Run all Sokoban EditMode and PlayMode assemblies. Current totals are 164 and 36.
