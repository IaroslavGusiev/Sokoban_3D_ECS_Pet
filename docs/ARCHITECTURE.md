# Sokoban architecture

Current design, checked against source on 2026-10-23. This document describes implemented behavior and ownership. Start with [setup](SETUP.md) to run or author content. Completed milestones and their test results are [historical records](history/README.md), not current setup instructions.

## Purpose and boundaries

This pet project exercises deterministic DefaultEcs rules and explicit OOP ownership around asynchronous Unity presentation. Gameplay includes moves, pushing, walls, win detection, automatic level progression, R restart, K player-skin cycling, and temporary N level cycling in Editor/development builds. Skin selection and all skin components live in presentation; switching skins does not mutate gameplay state.

| Folder under Sokoban/Assets/_Sokoban/Code | Assembly | Namespace | Responsibility |
| --- | --- | --- | --- |
| Core | Sokoban.Core | Sokoban.Core | Commands/components, parsing/population, movement, Simulation and Core pipeline. |
| Unity | Sokoban.Unity | Sokoban.Presentation | Composition, input/controller, text loading, shared prefab cache, per-run binding and projection. |
| Editor | Sokoban.Editor | Sokoban.Editor | Addressable string-address validation; excluded from players. |

Unity is 6000.0.63f1, DefaultEcs 0.17.2, Zenject 9.2.0, Addressables 4.1.1, and UniTask 2.5.11 from the existing Assets/Plugins installation. No packages were added. Core has noEngineReferences/overrideReferences enabled; its compiled references remain netstandard and DefaultEcs only.

Editor address validation and its tests live in the Editor-only Sokoban.Editor and Sokoban.Editor.Tests assemblies. [ASSEMBLIES.md](ASSEMBLIES.md) inventories all 22 source/vendored asmdefs and the resolved-package snapshot. Core friends only Sokoban.EditModeTests (internal movement systems) and Sokoban.App.EditModeTests (internal Simulation constructor/MovementFeature). The unused Sokoban.PlayModeTests entry was removed. Sokoban.Core.Tests uses public Core APIs, has no Unity references, and its source namespace is Sokoban.CoreTests.

## Deterministic Core

GridPosition is an immutable integer X/Y value. PlayerTag, BoxTag, WallTag, GoalTag, and FloorTag are readonly structs. Simulation.Send accepts cardinal MoveCommand values and preserves the first pending MoveRequest.

Simulation owns a new World and a SystemPipeline composed from the ordered RuleFeatures list. MovementFeature builds validation then application; WinFeature runs afterward. WinCheckSystem iterates goal and box entity sets directly without per-tick collections or LINQ, and sets LevelCompletedTag on the player once every goal is occupied by a box. Simulation.IsCompleted exposes that tag. Completion is latched; Core itself does not block further movement commands. Construction failures release acquired resources before propagating; normal disposal releases systems before the world. Cleanup exceptions propagate, without aggregation or idempotence flags.

LevelLoader creates the player, walls, and boxes as separate entities. It also creates exactly Width * Height floor entities covering every cell, including cells underneath walls. Each floor has GridPosition and FloorTag; goal positions additionally have GoalTag on that same floor entity. There are no separate goal entities. Initial entity/view counts for the three shipped levels are 76, 78, and 98.

MovementValidationSystem consumes requests, rejects blocked/invalid moves and integer overflow, and creates a checked MovementPlan. A push stores box then player steps with expected source positions. ApplyMovementSystem preflights every participant, consumes the plan, and writes positions through entity.Set. Destroyed, disabled, moved, foreign-world, or missing participants reject the plan before writes.

DefaultEcs notifications are synchronous: an observer can see the box position change before the player's change. Observers must not mutate participants, re-enter movement, or throw during commit; there is no rollback for arbitrary observer exceptions. Runtime movement has no explicit board-bounds check; text parsing validates playable-area enclosure.

SystemPipeline creates features in explicit list order. It owns a SequentialSystem, which checks its own IsEnabled and disposes children in reverse. Child rule systems retain their interface properties without local enable guards. Individual child-system disabling is unsupported.

## Parsing and tests

LevelParser accepts #, -, @, $, ., *, and +. Text starts at the highest y coordinate; Up is +y. CRLF is normalized, trailing empty rows ignored, and row characters preserved. Diagnostics identify invalid symbols/rows and begin with line N: or level:. Validation checks rectangular rows, one player, at least one box, equal box/goal counts, and a four-direction flood fill enclosed by walls. Boxes/goals are passable for enclosure; unreachable edge cells, including diagonal-only adjacency, are valid.

After checking box/goal counts, LevelParser rejects an already solved level with "level: the level is already solved; leave at least one box off its goal." The three shipped levels still parse and solve. LevelDefinition validates positive dimensions and nonnull collections at its public boundary. No Unity or async types enter the model.

Core.Tests discovers every Assets/Levels/*.txt. The independent BFS LevelSolver returns the shortest move sequence plus the player's predicted GridPosition after each move, with a 2,000,000-state cap. Replay sends each move through a real Simulation and asserts each player position and final box/goal positions. The shipped solutions remain 6, 12, and 11 moves. Solver code is test-only.

## Ownership

| Owner | Resources |
| --- | --- |
| AddressableLevelSource.LoadAsync | One temporary text handle, released in finally after parsing/failure/cancellation. |
| ViewAssets, bound by Zenject | One cached prefab handle per resolved ViewKey. Fallback requests share the Default handle. Same-frame failures are shared; failed entries are released and evicted on a request in a later frame. |
| GameplayController | Controller cancellation source, current LevelDefinition/index, completion timer, chosen player SkinId, and one private LevelRun. |
| LevelRun | Simulation and ViewLayer; construction, initial binding tick, explicit tick order, and reverse disposal. |
| Simulation | World and Core SystemPipeline. |
| ViewLayer | Per-run view root, cancellation source, ViewContext, presentation SystemPipeline, missing-view, failed-view, and player EntitySets. |
| Zenject | Service composition and controller/provider disposal. |

ILevelSource exposes Count and UniTask<LevelDefinition> LoadAsync(index, token). AddressableLevelSource depends only on LevelList. It awaits the Addressable TextAsset with explicit caller cancellation and auto-release disabled, parses it, and releases the handle in finally.

ViewCatalog groups skins by kind: each serializable KindEntry contains ViewKind and a SkinEntry[]; each serializable SkinEntry contains SkinId and a string Address. Validation requires exactly one group for every kind, unique skins within a group, a Default skin per group, and nonempty addresses. Resolve selects the kind group, then returns the exact SkinEntry or that group's Default. ViewAssets combines the requested kind with the returned skin to preserve resolved-key caching and passes Address directly to LoadAssetAsync<GameObject>. An existing but broken Alternate entry fails rather than falling back. LevelList stores ordered string addresses, loaded as TextAsset by AddressableLevelSource. See [setup](SETUP.md) for addresses and authoring, and [history](history/README.md) for the migrations.

Tools > Sokoban > Validate Addressable Addresses checks all saved ViewCatalog and LevelList assets against the Editor's Addressables settings: each configured address must match exactly one asset of the expected type in an included group, with address inclusion enabled for bundled groups. Editor APIs remain in Sokoban.Editor. Runtime validation checks structure and blank addresses only; missing/wrong-type built content follows the existing load failure and recovery paths. Editor validation does not prove that an existing content build is current; rebuild Addressables after changing content or addresses.

ViewAssets implements IViewProvider and IDisposable. IViewProvider.LoadAsync takes ViewKey and CancellationToken. ViewKey is an immutable equatable Kind/Skin pair; SkinId currently contains Default and Alternate. The provider resolves the catalog entry first and keys its cache by that entry's pair, so absent alternate skins reuse the same Default handle. Its constructor retains the catalog without loading anything. Cache misses start one Addressables.LoadAssetAsync<GameObject>; concurrent callers for the same resolved pair reuse the pending/successful handle. Each caller creates a ToUniTask waiter with its cancellation token and autoReleaseWhenCanceled disabled, then applies AttachExternalCancellation with the same token. Cancellation stops the caller's wait; the shared handle remains owned by the provider. Cancelling a caller never cancels or releases the shared handle. Successful prefabs stay cached across K, R, and N. CachedLoad.OnCompleted records Time.frameCount on failure, including immediate failure at creation. Requests in that frame share the failed handle and exception. A request in a later frame removes the failed cache entry and releases its still-valid handle before starting a fresh load. This prevents synchronous invalid-key failures from creating one failed handle/log per entity in a binding tick.

Failures log once per load attempt, covering synchronous catalogue/setup/conversion failures and asynchronous operation failure. A CachedLoad entry deduplicates its completion-callback log and its callers' exception-path logs, even if all callers stopped waiting. Cancellation is propagated without an error log. Each affected entity is separately marked ViewFailed; the binding operation does not duplicate provider logs. A later request can retry and can produce a new failure log. Current entities with ViewFailed do not retry automatically; a skin request clears their failure marker, while R or N builds a fresh run. A stale request's result or failure cannot bind or mark failure for a different desired ViewKey. Exceptions from instantiation/adapter installation remain logged by their binding operation.

Release while loading uses immediate owner release, without awaiting underlying completion. Addressables' internal execution reference survives until completion and final destruction. Addressables 4.1.1 also invalidates outstanding handles during ExitingPlayMode, potentially before Zenject's OnDestroy disposal. The approved IsValid checks remain at provider disposal and temporary text cleanup to skip already-invalidated handles.

## ECS presentation flow

### Entity and view lifetime contract

Within a run, existing entity identities, kind-selection tags, and GridPosition component presence remain stable. Movement changes GridPosition values through entity.Set; binding reads the latest value after awaiting. Presentation-owned SkinChangeRequest/ViewSkin updates support replacing a bound view. Individual entity destruction/reuse, retagging, and external removal/replacement of a bound view remain unsupported within a run.

Presentation exclusively owns ViewPending, ViewFailed, ViewSkin, SkinChangeRequest, TransformComponent, and instantiated view GameObjects. Gameplay and external callers must not remove these components or destroy the views independently; request player skin changes through the owned application/view-layer API. Presentation failures may leave partial adapter writes until run disposal; Ready describes component presence, not ongoing Unity-object health. Tests that deliberately violate these conditions exercise query/failure mechanics, not a supported arbitrary dynamic-view API.

R/N replaces a whole run. ViewLayer cancels its binding token before Simulation disposes the world, so pending results cannot access entities from the old run. This run-level contract does not provide individual entity-generation validation. Dynamic entity/view ownership requires a separate design before it is supported.

### Binding and projection

Every visual now follows one path:

needs view -> ViewPending -> TransformComponent, or ViewFailed.

ViewKeyMap prioritizes WallTag, BoxTag, PlayerTag, GoalTag, FloorTag exactly as before, and reads the desired skin from ViewSkin, defaulting to Default when absent. Goal floors therefore select the Goal prefab.

ViewBindingSystem is a buffered AEntitySetSystem<float>. ViewQuery.Viewable defines the common filter once: GridPosition and one of those tags using DefaultEcs 0.17.2 WithEither/Or. Binding adds exclusions for ViewPending, ViewFailed, and TransformComponent. It sets ViewPending first, then starts its owned async bind. No per-entity pending dictionary or retained task list exists outside ECS components.

Each bind captures the requested ViewKey. After awaiting IViewProvider with the run token, the bind checks cancellation before touching the entity, then recomputes ViewKeyMap.Get(entity). A different key drops the result and removes ViewPending, allowing a new request next tick without instantiating a stale view. Both provider and instantiation/adapter failures call Fail(entity, requestedKey): cancelled runs return before ECS access, stale requests only remove ViewPending, and current requests set ViewFailed and remove ViewPending. The existing active-run instantiation-exception log remains. For a current success, binding reads the latest GridPosition, instantiates under ViewContext.Root at GridCoordinates.ToWorld(position), keeps the prefab rotation, and names the instance Key (x, y). ViewKey.ToString preserves Kind for Default and prints Kind/Alternate for Alternate. If the prefab root has EntityView, its adapters install first. The binder then uses entity.Set(new TransformComponent(view.transform)) and removes ViewPending.

EntityView and the adapter hierarchy stay. EntityAdapter declares Install and Uninstall; ComponentAdapter<T> installs its serialized struct through Set and uninstalls through Remove<T>. TransformAdapter remains the sole concrete production adapter and inherits both methods. Its initial write is redundant but harmless because the binder sets the authoritative view transform afterward. EntityView forwards both operations to its adapters. Uninstall is invoked only by SkinSwitchSystem, never from OnDisable/OnDestroy. EntityView is optional, and ordinary prefabs can bind without it.

FeaturesInstaller binds one ViewFeatures instance. GameplayController passes that injected instance to ViewLayer, which calls its instance Create method. The presentation feature list is explicitly SkinSwitchFeature, ViewBindingFeature, then TransformSyncFeature. The buffered SkinSwitchSystem consumes SkinChangeRequest, uninstalls adapters from an existing EntityView, destroys the old GameObject, removes TransformComponent, writes ViewSkin, clears ViewFailed, and consumes the request. Existing ViewPending remains until its awaited operation completes or the run is cancelled. GridTransformSyncSystem projects every bound entity into Unity world space (X, 0, Y), including floor cells. Destroyed Unity transforms are skipped using Unity null semantics. Projection never writes gameplay positions back.

ViewLayer creates its own root, token source, context, pipeline, and two readiness EntitySets, plus its player query. The missing-view set uses ViewQuery.Viewable without TransformComponent; the failed-view set contains every entity with ViewFailed. Status is Failed if the failed set is nonempty, Binding if the missing set is nonempty, otherwise Ready. This includes unrequested viewable entities and gives failure priority even when a transform is present. No readiness system or stored aggregate ECS marker is needed.

ViewLayer owns a player query, writes the initial ViewSkin directly before the first tick, and SetPlayerSkin writes one SkinChangeRequest to every player. Dispose cancels before any world can be disposed, disables/destroys the root, disposes the pipeline and all three query sets, then disposes the token source. A late result returns without touching the old entity; Constructor failure releases acquired resources and rethrows.

## Controller and composition

GameplayController is the sole Zenject IInitializable/ITickable/IDisposable driver. Its only run field is a private LevelRun. LevelRun is internal, retains private Simulation/ViewLayer fields, forwards movement and completion/readiness queries, and ticks simulation before presentation. Both layers still implement IWorldLayer; no layer list is needed.

The controller's _playerSkin survives restarts and level changes. Every new LevelRun receives it and forwards it to ViewLayer before initial binding. In a non-Loading tick with a run, HandleSkinInput uses SkinCycle.Next and sends SetPlayerSkin before layer ticking. SkinCycle caches Enum.GetValues and wraps over contiguous SkinId values 0..n-1, so adding a third enum value needs no controller change. KeyboardMoveInput maps this to K, with the existing required-keyboard contract. Readiness is re-evaluated in BindingViews and ViewsFailed. Playing ignores Binding status but transitions to ViewsFailed on Failed status after ticking the run. Subsequent movement input pauses while both layers keep ticking; K can clear the failure and recover through BindingViews to Playing without R.

The controller states are Loading, BindingViews, Playing, Completed, ViewsFailed, and LevelFailed. Initialize asynchronously loads definition 0. Loading returns before input, layer ticking, or command handling. In every other state an existing run ticks both layers; only Playing reads/sends movement input. A queued Core request therefore remains pending while a replacement definition loads. The attempted index is stored before awaiting, so LevelFailed + R retries that index even if N failed before producing a definition. ViewsFailed always has a run; LevelFailed can have no run or retain the old run after a failed definition load. That old run is still ticked without movement input, and its view status cannot recover LevelFailed.

After a definition arrives, the controller checks cancellation, releases the old run, stores the definition, and starts LevelRun. StartRun enters BindingViews. TickBinding, used for both BindingViews and ViewsFailed, ticks the run then selects Playing on Ready, ViewsFailed on Failed, or BindingViews while still binding. Thus changing a failed startup skin can clear failure, wait for its new view, and return to Playing without R. LevelFailed does not evaluate readiness.

StartRun constructs LevelRun and publishes it only after successful construction. LevelRun creates Simulation, then ViewLayer, and performs one initial presentation tick to request views. Constructor failure disposes acquired presentation before simulation and rethrows. StartRun logs the error once, leaves no run, and enters LevelFailed. Initial loading, R, and N share this failure boundary. LevelFailed + R reloads the attempted definition. In every other non-Loading state, including ViewsFailed, R rebuilds from the existing definition. ViewLayer includes root creation/parenting in its acquisition cleanup boundary. LevelRun disposal uses try/finally so simulation disposal still runs if view disposal throws; ReleaseRun clears its reference in finally. Cleanup exceptions otherwise propagate without aggregation.

Spawning supplies final positions explicitly, so placement does not depend on a zero-delta transform update. Cached successful prefabs allow warm restarts to create views immediately.

A non-Loading Tick calls HandleSkinInput, then TickState(deltaTime), then HandleCommands if commands are still allowed and the state is not Loading. TickState dispatches to TickBinding, TickPlaying, TickCompleted, or TickLevelFailed. TickPlaying sends movement, ticks Simulation (validation -> application -> win check) then ViewLayer (skin switch -> binding -> projection), checks view failure, and otherwise detects completion. View failure takes priority over a win in the same tick. Core completion remains latched and can enter Completed after view recovery. TickCompleted ticks the run and advances the timer; at 0.75 seconds it loads (index + 1) % Count. TickState returns false after this automatic load, preventing a second command even if loading completes synchronously. Wrapping is temporary. HandleCommands gives N priority over R. Loading ignores controls. Definition-load exceptions enter LevelFailed. Run layers dispose in reverse: view cancellation/root/pipeline/queries first, simulation systems/world second. Controller disposal cancels its own text loading and releases its run; it never disposes the shared provider.

ServicesInstaller keeps its serialized catalog/list/root fields and FormerlySerializedAs aliases, validates both authoring assets, and binds ILevelSource, IPlayerInput, LevelList, ViewCatalog, LevelRoot, and BindInterfacesAndSelfTo<ViewAssets>().AsSingle(). FeaturesInstaller binds ViewFeatures. GameplayInstaller binds the controller interfaces/self. No World, run, or pipeline is globally bound.

KeyboardMoveInput requires a connected keyboard, per the approved input contract. Movement, R, and Editor/development N access Keyboard.current directly; there is no missing-keyboard fallback. Ordinary-player N still returns false without accessing the device.

## Verification and limits

Run the project EditMode assemblies, including Sokoban.Core.Tests and Sokoban.Editor.Tests, and Sokoban.PlayModeTests for integration/lifecycle coverage. Editor tests validate configured addresses; tests against built content require a fresh Addressables build. See the [setup checks](SETUP.md#verification).

Historical pass counts, build results, smoke checks, and known tooling limitations are recorded in [history](history/README.md). Those results apply to their recorded revisions; this documentation cleanup does not rerun or establish gameplay/build verification.
