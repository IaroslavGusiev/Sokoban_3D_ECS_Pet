# Historical layer refactor: guard review

This records the earlier layer-refactor state. The later text-content task adds parser/external-data validation and runs ViewCatalog.Validate in ServicesInstaller rather than controller Initialize. Current APIs, test counts, and pending Editor setup are documented in [ARCHITECTURE.md](ARCHITECTURE.md) and [ASYNC-CONTENT.md](ASYNC-CONTENT.md). The async migration explicitly permits the Playing-state check and cancellation/failure cleanup; the earlier policy entries below are historical.

Implemented on 2026-10-22 in `Sokoban/Assets/_Sokoban/Code/Core` and `Unity`. This inventory covers runtime code, including domain branches retained by the guard policy. Test fixture checks are outside this policy. No new runtime guards were added.

## Verified library behavior and selected contract

The installed DefaultEcs assembly reports version 0.17.2.0. Inspection of its method IL established:

- `SequentialSystem<float>.Dispose()` disposes child systems in reverse order. `SystemPipeline.Dispose()` therefore delegates once; it does not also dispose its child list.
- `SequentialSystem<float>.Update()` checks only its own `IsEnabled`, then calls each child. It does **not** skip disabled children. The user explicitly selected removing local checks and supporting aggregate pipeline disabling only. Rule-system `IsEnabled` remains a plain auto-property required by `ISystem<float>`; setting an individual rule's property does not suppress execution.

The requested SystemPipeline API remains Tick/Dispose; no new public enable control was added. Constructor rollback is a simple reverse loop, without aggregation. A disposal exception propagates and may interrupt disposal of remaining children. Single ownership requires one disposal, and no use after disposal.

## Removed guards

| Location | Removed check/state | Reason |
| --- | --- | --- |
| Simulation | `_disposed`; `ThrowIfDisposed()` calls in World, Send, and Tick; its ObjectDisposedException helper | The controller owns simulation lifetime. Normal calls occur only while the layer is owned and active. |
| Simulation.Dispose | `_disposed` early return and assignment | One owner disposes once. |
| ViewLayer.Tick | `_disposed` field and ObjectDisposedException check | The controller ticks owned layers in lifecycle order. |
| ViewLayer.Dispose | `_disposed` early return and assignment | One owner disposes once. |
| LevelView.Dispose | `_disposed` field, early return, and assignment | ViewLayer owns the view. The separate Unity destroyed-object check remains. |
| GameplayController.Initialize | `_initialized` field/assignment and `_initialized || _disposed` early return | Zenject initializes before ticking. Invalid lifecycle usage is no longer silently ignored. |
| GameplayController.Tick | `!_initialized || _disposed` early return | Zenject lifecycle determines when ticks are valid. |
| GameplayController.Tick | `Current != null` conditional around simulation/layers | A successful Initialize establishes Current. Failed construction propagates instead of enabling idle ticks with no level. |
| GameplayController.Dispose | `_disposed` field, early return, and assignment | Zenject owns controller disposal. Clearing the owned list releases references without an extra state flag. |
| GameplayController restart (step 3) | `_restartRequested`, RequestRestart, and flag handling | Usages were test-only. RestartPressed is handled after the layer loop; the tests now supply input. |
| MovementValidationSystem constructor | `world ?? throw ArgumentNullException` | Internal system construction receives the simulation-owned World. |
| MovementValidationSystem.Update | `_disposed` field/ObjectDisposedException check | Simulation's pipeline owns system lifetime. |
| MovementValidationSystem.Update | `!IsEnabled` early return | Explicit user choice: aggregate disabling only. |
| MovementValidationSystem.ProcessPlayerRequests | `!player.IsAlive` | Players come from the current EntitySet snapshot. Mutation/reentrancy from commit observers is outside the supported contract. |
| MovementValidationSystem.Dispose | `_disposed` early return and assignment | Pipeline owns and disposes queries once. |
| ApplyMovementSystem constructor | `world ?? throw ArgumentNullException` | Internal system construction receives the simulation-owned World. |
| ApplyMovementSystem.Update | `_disposed` field/ObjectDisposedException check | Simulation's pipeline owns system lifetime. |
| ApplyMovementSystem.Update | `!IsEnabled` early return | Explicit user choice: aggregate disabling only. |
| ApplyMovementSystem.ApplyPlan | `!owner.IsAlive || !owner.Has<MovementPlan>()` early return | Owner comes from this Update's MovementPlan query snapshot. Removing its plan happens after reading it. Commit observers must not mutate other owners or re-enter movement. |
| ApplyMovementSystem.Dispose | `_disposed` early return and assignment | Pipeline owns and disposes its query once. |
| LevelRoot constructor | `transform == null` / ArgumentNullException | Explicitly requested removal for this composition-owned wrapper. |
| LevelView constructor | `parent == null` / ArgumentNullException | Construction receives LevelRoot through owned composition. |
| TransformComponent constructor | `transform == null` / ArgumentNullException | Own-code component construction does not need a second validation boundary. Projection still checks the stored Unity reference, including default/serialized components. |
| GridTransformSyncSystem.Update | `transform.position == worldPosition` early return | Redundant same-position optimization; projection can assign the computed grid position directly. |
| Core composition (step 1) | Feature order sorting and duplicate-order rejection | Explicit feature lists are the ordering contract; Order no longer exists. |

Only guards and their now-unused imports/state were changed in the two movement systems. Their planning, domain checks, snapshot iteration, plan consumption, and entity.Set writes remain unchanged.

## Kept guards and branches

| Location | Kept check | Reason |
| --- | --- | --- |
| Simulation constructor | `level == null` | Public Core input validation; the public constructor delegates to the shared internal constructor. |
| Simulation.Send | Command is one of Up/Down/Left/Right | Public command boundary; undefined/default values are ignored. |
| Simulation.Send | `_player.Has<MoveRequest>()` | Domain rule: first pending request wins. |
| LevelDefinition constructor | `width <= 0`; `height <= 0` | Public external level data must have positive dimensions. |
| LevelDefinition constructor | Null walls, boxes, and goals collections | Public external data validation. These `??` operands are C# collections, not UnityEngine.Object. |
| MovementPlan indexer | StepCount conditions for indices 0/1 and default out-of-range throw | Public data access must not expose a nonexistent step. |
| MovementValidationSystem.ProcessPlayerRequests | `!player.Has<MoveRequest>()` | The player query filters position and PlayerTag, not MoveRequest. Idle players legitimately have no input. |
| MovementValidationSystem.ProcessPlayerRequests | `_players.Count == 1` and successful TryCreatePlan | Existing domain requirement: ambiguous players and invalid plans do not move. |
| MovementValidationSystem.TryCreatePlan | TryGetDirection succeeds | Requests can be installed directly as public components; invalid commands cannot produce movement. |
| MovementValidationSystem.TryCreatePlan | TryOffset succeeds; no wall at destination | Domain movement blocking and integer-range validity. |
| MovementValidationSystem.TryCreatePlan | Box at destination | Domain choice between a simple move and push planning. |
| MovementValidationSystem.TryCreatePushPlan | Box offset succeeds; no wall or box behind it | Domain push blocking and integer-range validity. |
| MovementValidationSystem.TryFindAt | GridPosition equality; no-match result | Actual wall/box occupancy lookup. |
| MovementValidationSystem.TryGetDirection | Nonzero direction result | Invalid command rejection. |
| MovementValidationSystem.TryOffset | X/Y fit int.MinValue through int.MaxValue | Domain arithmetic must not wrap into a different grid cell. |
| ApplyMovementSystem.ApplyPlan | `!canApply` | Rejects the entire invalid plan before position writes. |
| ApplyMovementSystem.CanApplyPlan | One/two steps, distinct targets, valid participants; other counts rejected | Existing plan shape and atomic preflight rules. |
| ApplyMovementSystem.IsTargetUnchanged | Current position equals ExpectedSource | A stored plan must not apply after a participant has moved. |
| ApplyMovementSystem.IsTargetAvailable | Target.World is the owned world | MovementStep stores handles across the validation/application boundary; foreign-world targets are rejected. |
| ApplyMovementSystem.IsTargetAvailable | Target.IsAlive | Stored handles can outlive or be invalidated between stages, including entity-ID reuse. |
| ApplyMovementSystem.IsTargetAvailable | Target enabled; GridPosition present and enabled | Stored participants may be disabled or lose their position after validation. The complete plan must be rejected. These are not redundant owner-query checks. |
| GameplayController.Tick | RestartPressed | Actual input/flow decision, performed after every layer has ticked. |
| HardcodedLevel.Create | Border-cell condition | Existing level content generation, not defensive state checking. |
| KeyboardMoveInput.RestartPressed | Keyboard.current is not null | External input device absence/hotplug is legitimate. Keyboard is not a UnityEngine.Object. |
| KeyboardMoveInput.ReadMove | Null keyboard fallback; key/direction cases; no-press fallback | External input validity and existing priority/idle behavior. |
| ViewCatalog.Validate | Floor, wall, box prefabs and goal/player materials compared to null | External serialized Unity references. Called once by controller Initialize. Unity fake-null semantics are preserved. |
| LevelView.CreateFloors | isGoal | Selects existing goal material/view mapping. |
| LevelView.CreateEntityViews | Wall/box/player/goal tag selection | Maps existing entity types to views; not a redundant component-presence check. |
| LevelView.CreateEntityViews | `view != null` | A branch may produce no view, and Unity can invalidate references. Uses Unity equality. |
| LevelView.BindView | `view == null` | External prefab authoring validation: EntityView is required on grid prefabs. Uses Unity equality. |
| LevelView.Dispose | `_root != null` | Unity may already have destroyed the hierarchy during scene unload, independently of ECS ownership. |
| EntityView.Install | `adapter == null` | External serialized adapter reference may be missing/destroyed. Uses Unity equality. |
| ComponentAdapter.Install | `entity.Has<T>()` then duplicate-component exception | External prefab adapter configuration must not silently replace an already installed component. This is not an entity taken from a matching query. |
| GridTransformSyncSystem.Update | `transform == null` | Stored Unity references may be default, missing, or independently destroyed. Skip projection safely using Unity equality. |
| GridPosition.Equals(object) | `obj is GridPosition` | Value equality contract for arbitrary objects. |

## Kept cleanup boundaries

These are resource ownership, not lifecycle state guards. No ownership flags or nested exception aggregation were introduced.

| Location | Cleanup | Reason |
| --- | --- | --- |
| SystemPipeline constructor catch | Dispose returned child systems in reverse, rethrow | Explicitly requested rollback before pipeline ownership can be returned. |
| Simulation constructor catch | Dispose World, rethrow | Explicitly requested release if population/pipeline creation fails. |
| MovementFeature.Create catch | Dispose validation if later system/group construction fails | A feature owns partial construction until Create returns. |
| ViewLayer constructor catch | Dispose partial LevelView, rethrow | Removes generated objects if building/binding views or constructing the pipeline fails. Pipeline cleans its own partial systems. |
| GameplayController.StartLevel catch | Release the layers already added, rethrow | Removes the newly owned simulation when view construction fails. |
| Simulation.Dispose finally | Dispose World after pipeline attempt | Ensures World release even when child disposal throws; existing failure case retained. |
| ViewLayer.Dispose finally | Dispose pipeline after LevelView attempt | Ensures view queries are released if hierarchy teardown throws. |

## Existing test migration and verification

Existing tests now dispose owned resources once, avoid Send/Tick after disposal, and disable the aggregate SequentialSystem rather than an individual application rule. The scene fixture unregisters a manually disposed controller from Zenject's tick and disposal managers. Its failure scenario stops automatic ticks before inducing construction failure, restores the invalid prefab reference, and explicitly reconstructs before resuming manual movement.

One obsolete case, `DisposedSession_RejectsMovementAndEntityEnumeration`, was removed because every assertion required the deleted disposed-object exception contract. No new test cases were added. All 142 remaining EditMode and 24 PlayMode cases pass. All five implementation steps compiled. Core's compiled references are only netstandard and DefaultEcs; its friend assemblies are the existing three test assemblies only. GameplayController is the only production ITickable.

No new gameplay, packages, asynchronous behavior, or commits were introduced. No standalone player build was run.
