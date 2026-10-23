# Architecture history

**Historical record.** Behavior, setup steps, pass counts, and pending proposals below belong to the recorded milestone. Use [current architecture](../../ARCHITECTURE.md) and [current setup](../../SETUP.md) for the implemented project.

Historical snapshot before the readability refactor on 2026-10-21. Statements about "current" or "active" implementations below refer to that snapshot. See [the current architecture](../../ARCHITECTURE.md) for the implemented design and names.

# Sokoban Architecture — Working Design

Status: intended direction except for the implemented baseline recorded below. Update after decisions are approved and verified.

## Purpose

Use a minimal isometric 3D Sokoban game as a controlled environment for two engineering experiments:
1. Pure C# DefaultEcs gameplay with OOP application infrastructure.
2. Correct asynchronous Unity Addressables integration through UniTask and explicit lifecycles.

Visuals and content are intentionally minimal. Architecture, correctness, tests, and lifecycle reasoning are the priorities.

## Proposed boundaries

### Gameplay / Domain (pure C#)
- A DefaultEcs `World` per gameplay session, with clear creation and disposal ownership.
- Small data components such as `GridPosition`, `PlayerTag`, `BoxTag`, `WallTag`, `GoalTag`.
- Synchronous systems for movement, push validation, occupancy, and completion.
- Explicit command inputs and observable simulation results; deterministic behavior.
- No UnityEngine, Zenject, UniTask, Addressables, or MonoBehaviour references.
- Use `Set()` for position changes where change notifications and indexes matter; verify installed DefaultEcs behavior before assuming exact API details.

### Application / OOP
- Owns session lifecycle and high-level state transitions.
- Connects player input to domain commands and domain results to presentation.
- Orchestrates asynchronous work through interfaces, without introducing async into ECS systems.
- Explicitly guards against callbacks/results targeting replaced or disposed sessions.

### Infrastructure / Unity
- Zenject composition root; Unity views and scene lifecycle.
- UniTask and Addressables service for loading/releasing level or theme assets.
- Explicit cancellation and asset-handle ownership; failures are observed and handled.
- Minimal isometric 3D visuals; logical grid X/Y may map to Unity world X/Z.

## Async experiment scenarios

1. Load a level/theme successfully, then start the appropriate session.
2. Cancel loading when the player changes level or leaves the state.
3. Complete an old load after a newer session exists; ignore stale results and release assets safely.
4. Handle Addressables failure without leaking handles or corrupting gameplay state.
5. Dispose a session while work is in flight; no mutation of disposed ECS state.

Note: cancellation of an awaiting task is not necessarily cancellation of the underlying Addressables operation. Verify the specific UniTask/Addressables API and define release ownership carefully.

## Testing priorities

- Pure NUnit gameplay tests: empty movement, wall collision, single-box push, blocked push, adjacent boxes, goal completion.
- Lifecycle tests: cancellation, late completion, repeated transitions, failure cleanup, disposal.
- Unity integration tests only where Unity-specific behavior matters.

## Design rules

- Prefer minimal interfaces at actual boundaries over interfaces for every class.
- Avoid premature general-purpose frameworks and overengineered event buses.
- Keep experimental design decisions visible and revise them when tests provide evidence.
- Verify package versions and APIs against installed dependencies before writing implementation.

## Initial milestones

1. Package installation and a minimal ECS installation test.
2. Pure C# movement simulation and focused tests.
3. OOP session/application composition and state transitions.
4. Addressables/UniTask integration with explicit ownership and lifecycle tests.
5. Minimal 3D presentation and documentation of lessons learned.

## Implemented baseline — Phase 1, milestone 1

- `Sokoban/Assets/_Sokoban/Code/Ecs/Core/` contains immutable `GridPosition` and empty value tags `PlayerTag`, `BoxTag`, `WallTag`, and `GoalTag` in `Sokoban.Domain.Components`. The domain assembly disables Unity engine references.
- `Sokoban/Assets/_Sokoban/Tests/EditMode/EcsInstallationTests.cs` creates a fresh DefaultEcs world with a player, box, wall, and goal for each test. The fixture owns world disposal, and query sets are disposed within each test. Tests cover entity identity/liveness/ownership, component values, and position/tag query filtering.
- The existing `EcsTest` scene smoke script uses the domain component. No new runtime infrastructure or gameplay systems were added.
- Verified on 2026-10-20 with Unity `6000.0.63f1`, DefaultEcs `0.17.2`, and Unity Test Framework `1.6.0`: compilation succeeded and all four focused NUnit EditMode tests passed. The test assembly explicitly references `DefaultEcs.dll` and `nunit.framework.dll`.

## Implemented baseline — Phase 1, Iteration 1

The EntitySet implementation is preserved verbatim in [MovementSystem.EntitySet.cs.txt](iteration-1/MovementSystem.EntitySet.cs.txt), outside Unity's compiled assets. This section describes that baseline; Iteration 2 records the subsequent map experiment. The component-driven movement section below describes the current implementation.

- `MoveCommand` in `Sokoban.Domain.Commands` represents one cardinal step: Up increases Y, Down decreases Y, Left decreases X, and Right increases X. The default enum value and undefined values are invalid commands.
- `MovementSystem` in `Sokoban.Domain.Systems` exposes synchronous `bool TryMove(MoveCommand command)`. It maintains three `EntitySet` queries for positioned players, boxes, and walls. Spatial lookups scan the sets and compare `GridPosition` values; there is no spatial map or index.
- A move validates the destination and, if pushing, the box destination before any position updates. Successful pushes move exactly one box, then the player, using `entity.Set(position)` with the computed immutable position. Walls and other boxes block pushes. Goals do not block movement or pushing.
- Rejected commands return `false` without changing any entity positions. Rejection includes blocked paths, missing/multiple positioned players, invalid command values, and destinations outside the integer coordinate range. The baseline assumes a valid level with no overlapping box/wall occupants. There are no board bounds beyond walls; a cell without a wall or box is traversable.
- The caller owns the world and disposes the movement system before the world. The system owns and disposes its query sets, never the world. Calls after system disposal throw `ObjectDisposedException`. Session orchestration and presentation remain future work.
- `Sokoban/Assets/_Sokoban/Tests/EditMode/MovementTests.cs` tests behavior through `TryMove`, without inspecting lookup internals. Coverage includes all four directions, blocked-move snapshots of every positioned entity, goals, repeated commands, query membership updates, coordinate overflow, and deterministic replay with different creation orders. Additional checks use disposable DefaultEcs `SubscribeComponentChanged<GridPosition>` subscriptions to verify successful-move notifications (including old/new values) and no notifications for rejected moves/pushes. A two-step push onto/off a goal verifies its position and tag remain intact without change/removal notifications.
- Verified on 2026-10-20 with Unity `6000.0.63f1` and DefaultEcs `0.17.2`: compilation succeeded and all 73 EditMode cases passed (50 movement, 19 position equality, 4 ECS installation), preserving all 70 original cases. The compiled domain references only `netstandard` and `DefaultEcs`.

### Movement world invariants

- Exactly one player exists, and every solid entity (player, box, or wall) has a `GridPosition`.
- Solid entities must not overlap; in particular, multiple boxes cannot occupy the same grid position.
- Goals may overlap players or boxes and remain separate, unchanged entities during movement.
- Future level construction/validation will enforce these invariants. `MovementValidationSystem` guards the positioned-player count but does not validate the whole level; no level validator is implemented.

## Implemented — Phase 1, Iteration 2

The Iteration 2 `MovementSystem` kept the player `EntitySet` and replaces box/wall sets with separate `EntityMap<GridPosition>` queries. `ContainsKey` checks occupancy, and `TryGetEntity` retrieves a box. Goals remain outside these maps, allowing the documented goal overlaps. The public API, move validation, box-before-player update order, `Set(position)` writes, and caller ownership of the world are unchanged. The system owns and disposes its player set and both maps.

| Concern | Iteration 1: EntitySet | Iteration 2: EntityMap |
| --- | --- | --- |
| Box/wall spatial lookup | Linear scan comparing current positions | Key lookup by `GridPosition` |
| Position updates | Scans read the current component | `Set(position)` immediately replaces the old map key |
| Key uniqueness | Scanning does not enforce uniqueness | Each map permits one entity per position |
| Owned query containers | Three sets | One set and two maps |

Map lookups use a hash index, which adds index storage and maintenance on membership/key changes. No performance benchmark has been run; this iteration establishes behavioral equivalence and records integration behavior rather than a measured performance improvement.

### Verified DefaultEcs 0.17.2 behavior

- Maps index matching entities present at construction and added later. Component/tag removal and entity disposal remove the corresponding key; restoring the required component/tag reindexes the entity.
- Position changes through `Set(position)` synchronously remove the old key and expose the new key. Maps dispose their subscriptions without disposing the world.
- Duplicate keys during map construction, entity insertion, or key changes throw `ArgumentException`. On insertion/change, the component assignment has already happened; the exception does not roll it back. A failed key change also removes the entity's old map key. These failures violate the valid-level contract and are not normal `TryMove` rejection results. Future level construction/validation must enforce the existing uniqueness invariants before indexing or assigning occupied positions.
- `EntityMapTests.cs` characterizes these behaviors in isolated worlds, including intentionally invalid duplicate-key cases. It introduces no production level validator or lookup abstraction.

Verified on 2026-10-20 with Unity `6000.0.63f1` and DefaultEcs `0.17.2`: compilation succeeded and all 83 EditMode cases passed. All 73 existing cases and their source files were preserved unchanged; 10 EntityMap cases were added. The compiled domain references only `netstandard` and `DefaultEcs`.

The component-driven refactor below supersedes this runtime map implementation with EntitySet scans. The package-level `EntityMapTests` remain as characterization tests; production movement no longer constructs maps.

## Presentation prefab placement

- A one-unit grid cell maps domain `(X, Y)` to Unity `(X, 0, Y)`. Prefab roots are at the cell center on the walkable surface, with zero local position, identity rotation, and unit scale. Geometry offsets are stored in a `Visual` child.
- `Floor.prefab` uses a unit cube centered at local Y = -0.5, so its top is Y = 0. Its mesh and existing BoxCollider are on the visual child. `Wall.prefab` uses a unit cube centered at local Y = +0.5, so its bottom is Y = 0 and its top is Y = 1; its existing collider follows the visual child.
- `Box.prefab` keeps its imported mesh and material. The visual is uniformly scaled and centered horizontally, with its bottom at Y = 0 and height of one unit. Verified imported bounds are approximately 0.8469 x 1 x 0.8847 units. No collider was added; gameplay collision remains in the ECS rules.
- Normal floor uses `DarkGray_Cell_mat`; goal-floor views assign the existing `Green_Cell_mat` to their renderer. Goal cells use the same floor geometry and anchor, while `GoalTag` remains in the domain. Goal material assignment is a presentation step and needs no extra goal mesh.
- Verified in Unity `6000.0.63f1` on 2026-10-20 using prefab-content editing and reloading the saved assets: all three roots, geometry bounds, existing cube colliders, prefab GUIDs/root object IDs, and material references were checked. The temporary Editor utility was removed. Existing scene-instance transform overrides were not reset.

## Hardcoded presentation test level

- `HardcodedTestScene` is a separate playable harness. `HardcodedLevelController` in the Unity-dependent `Sokoban.Presentation` assembly composes the ordered ECS pipeline, a pure C# `LevelSession`, and a Unity `LevelView`. A separate `EcsRunner` drives frames. It uses direct prefab references; Addressables, asynchronous loading, and Zenject composition remain future work.
- `HardcodedLevel.Create()` supplies the closed 7 x 7 board with one player, two boxes, and two separate goal entities. The view reuses the normalized floor, wall, and box prefabs. Goal floors use `Green_Cell_mat`; the temporary player is a narrowed wall instance using `Blue_Cell_mat`. The scene supplies an orthographic isometric camera and fixed HDRP exposure.
- Arrow keys or WASD submit one cardinal request per press through Input System `1.20.1`; simultaneous presses use Up, Down, Left, Right priority. Up/W increases domain Y (Unity Z). Transform projection runs after movement in the same tick. ECS remains authoritative; no physics movement or animation is involved.
- R requests a rebuild after the current pipeline finishes. Restart, disable, and destruction unbind the runner, release the view bindings, then dispose the session's pipeline and world. Enabling the controller starts a fresh session. The controller remains a small scene composition root rather than a general application framework.
- To try it, open `Sokoban/Assets/_Sokoban/Scenes/HardcodedTestScene.unity`, enter Play Mode, and focus the Game view. A solution from the initial state is Left, Up, Down, Right, Right, Up. Both boxes then cover goals; victory detection/UI, undo, level loading, and camera controls are not implemented.
- The previous untitled prefab layout was preserved as `PrefabSandbox.unity`. The box material was upgraded from Standard to HDRP/Lit during Editor integration, retaining its albedo and normal textures; prefab geometry and anchors were unchanged.
- Verified on 2026-10-20 in Unity `6000.0.63f1`: all 83 existing NUnit EditMode cases passed through the installed Test Runner API. A temporary Editor harness also entered actual Play Mode, confirmed advancing frames, queued Input System keyboard events for the solution and R restart, checked ECS/view agreement, goal preservation, blocked moves, repeated restarts, disable/re-enable, and destruction cleanup using world-disposal notifications. Initial and solved camera renders were visually inspected. The temporary setup/verification utility was removed; detailed results and screenshots remain under the ignored `Sokoban/Logs/` directory.

## Implemented — Application session and ordered ECS pipeline

| Assembly / type | Responsibility |
| --- | --- |
| `Sokoban.Domain` / `MoveRequest` | Immutable pending cardinal command on a player entity. |
| `Sokoban.Domain` / `MoveTo`, `ValidatedMovement` | Immutable target/source/destination instructions and one transient component holding the complete ordered operation (one or two instructions). |
| `Sokoban.Domain` / `MovementValidationSystem` | Consumes requests, scans player/box/wall EntitySets, validates the entire move or push, and creates a plan without writing positions. |
| `Sokoban.Domain` / `ApplyMovementSystem` | Preflights every target and applies the complete plan using `Set(GridPosition)`. Owns a plan EntitySet and knows no gameplay tags or collision rules. |
| `Sokoban.Application` / `LevelDefinition` | Immutable rectangular board data: width, height, player start, walls, boxes, and goals. Every cell has a floor. Input collections are copied and exposed as read-only collections. |
| `Sokoban.Application` / `HardcodedLevel` | Supplies the current sample data without Unity or prefab dependencies. |
| `Sokoban.Application` / `LevelSession` | Owns one world and an `ISystem<float>` pipeline, populates entities from the definition, exposes `RequestMove` and `Tick`, and disposes the pipeline before the world. The default constructor composes a pure validation/application pipeline for isolated tests. |
| `Sokoban.Ecs.Unity` / `KeyboardInputSystem` | Polls directional keys and writes one movement request. Does not validate, execute, or handle restart. |
| `Sokoban.Ecs.Unity` / `RestartInputSystem` | Runs after directional input; R cancels requests/plans and requests a deferred restart. |
| `Sokoban.Ecs.Unity` / `TransformComponent`, `TransformAdapter` | A serializable transform reference and a small prefab adapter that supplies its preset through `EntityBaseComponent<TransformComponent>`. Installation writes the complete component with `Set`; the adapter performs no coordinate projection. |
| `Sokoban.Ecs.Unity` / `GridTransformSyncSystem` | An `AEntitySetSystem<float>` with a cached `GridPosition` + `TransformComponent` query. Projects every bound grid entity into Unity world positions after movement. |
| `Sokoban.Ecs.Unity` / `ABaseAdapter`, `EntityBaseComponent<T>`, `EntityView` | Shared session checks, duplicate guards, component ownership, prefab adapter grouping, rollback on installation failure, and idempotent uninstall. Views bind existing entities and never create or dispose them. |
| `Sokoban.Presentation` / `LevelView` | Owns generated Unity objects, installs their authored `EntityView` bindings, places ordinary floors once, and applies materials. |
| `Sokoban.Presentation` / `HardcodedLevelController` | Keeps serialized scene references, `Restart`, and a `RequestMove` entry point. Composes dependencies and coordinates their lifetimes; has no `Update` or keyboard polling. |
| `Sokoban.Presentation` / `EcsRunner` | The only Unity `Update` entry point. Calls the session tick and applies a requested restart after it returns. Owns no world or systems. |

The `Code/Ecs` module groups all ECS code. `Ecs/Core` retains the existing `Sokoban.Domain` assembly and namespaces and contains the pure movement components and systems. `Ecs/Unity` contains Unity systems, transform components, and adapter infrastructure in `Sokoban.Ecs.Unity`. The module has two compilation boundaries so pure gameplay remains enforceable.

Both Domain and Application set `noEngineReferences: true`. Application depends on Domain and DefaultEcs. `Sokoban.Ecs.Unity` depends on both pure assemblies and Unity Input System; Presentation depends on the pure assemblies and `Sokoban.Ecs.Unity`. Dependencies remain acyclic. The two adapter base classes centralize binding mechanics; no service interfaces, factories, event buses, or state-machine framework were introduced.

### Commands and ownership

```text
EcsRunner.Update
  -> LevelSession.Tick(deltaTime)
     -> SequentialSystem<float>
        1. KeyboardInputSystem
        2. RestartInputSystem
        3. SequentialSystem<float> (enable/disable this movement pair together)
           a. MovementValidationSystem
           b. ApplyMovementSystem
        4. GridTransformSyncSystem
  -> apply deferred restart, if requested
```

`LevelSession.RequestMove` and the controller's matching method return whether a request was accepted, not whether movement succeeded. They replace the previous session/controller `TryMove` API so its synchronous result is not silently reinterpreted. There is one pending request per player; the first request wins, and additional requests are rejected until it is consumed. Undefined commands are rejected on submission. The old `MovementSystem` and its immediate `TryMove` API were removed; test-only helpers submit requests and observe actual pipeline results.

Validation clears abandoned plans, then consumes each positioned player's current request, including blocked/invalid requests. Exactly one positioned player is required; multiple players have their requests consumed without plans. The movement pair is enabled/disabled together, so pausing leaves requests pending. Delta time never changes a grid step. Spatial lookups scan the box/wall EntitySets and compare current positions; goals do not participate in blocking.

The transform system runs on the Unity main thread after the entire movement stage, so both sides of a push are complete before projection. It assigns `Transform.position` from the shared `GridCoordinates.ToWorld` mapping: absolute unit cells `(X, 0, Y)` on the world X/Z plane. Players, boxes, walls, and goal-floor views all bind their existing entities and use this single cached query (29 bindings in the hardcoded level). Goals reuse their green floor view rather than spawning another object. Ordinary floor tiles have no ECS entity or installed transform binding and are positioned once using the same mapping. The short-lived goal-view dictionary is used only during construction, not for gameplay spatial lookup.

Projection preserves the user's current direct `Transform.position` implementation and skips unchanged positions. It does not call `Set` on `TransformComponent`, attach a standalone Unity `Transform` component, or emit ECS transform-component notifications. `GridPosition` remains read-only in this system. The two older transform-notification tests were adapted to assert world-position projection and unchanged ECS components; movement still publishes `GridPosition` notifications.

After view construction, the composition root explicitly invokes the transform system once, without polling input or moving the simulation. Adapters only install data. Subsequent projection happens through the ordered pipeline, including for bindings installed after the query was constructed. Scene parents organize the hierarchy; their translation/rotation is not a grid origin. Spawned view roots start with the prefab's world rotation. Mesh-pivot offsets remain local on `Visual` children. Parent scale and later parent rotation still affect geometry; arbitrary transformed-parent geometry is not normalized by the position system. ECS never reads transforms back into simulation state. Reactive filtering, interpolation, fixed/late pipelines, and parallel updates are not introduced.

The composition root creates the world and ordered pipeline and transfers ownership to the injected `LevelSession` constructor only when construction succeeds. On failure before transfer, the composition root disposes the pipeline and world; on later view failure, it releases the session. A default session creates and owns its own pure pipeline. Session disposal is idempotent and releases the world in a `finally` block even if pipeline disposal fails.

On restart or disable, the controller first unbinds the runner, then removes the view bindings and disables its root before Unity's deferred destruction, then disposes the session. The session disposes every contained system/query before its world. The runner never separately disposes these resources. R has priority over movement and is applied only after the active pipeline returns. A replacement session receives a fresh world, pipeline, pending-request state, and view bindings.

`LevelSession.GetEntities()` remains a construction/read bridge, not a transfer of ownership. It checks disposal when enumerated. Unity adapters may attach/remove their own `TransformComponent`; gameplay state changes go through requests and domain systems. Adapters retain the originating session as well as the entity and check `session.Owns(entity)` before removing a binding. Late cleanup from a disposed session cannot remove a replacement session's binding. DefaultEcs entity handles alone are not proof of session identity because world identifiers can be reused; bindings must be created from the intended current session, and raw handles must not cross sessions. No asynchronous integration is present yet.

Floor, wall, and box prefab roots contain an authored `EntityView` and `TransformAdapter`; the adapter preset references the root transform and the view lists its adapters. Ordinary floors leave these adapters uninstalled. The generic base rejects existing components, installs complete values with `Set`, and removes a component only while its originating session is valid and the value still matches its installation. Uninstall clears local ownership state even after session disposal. `EntityView` rolls back earlier successful adapters when a later installation fails and uninstalls in reverse order. Disabling/destroying either the view or adapter removes bindings without disposing simulation entities. Re-enabling does not create an entity or automatically rebind it. Level data still performs only basic dimension/null checks; full solid-overlap, occupancy, and bounds validation remains future work under the existing valid-level contract.

The ordered systems and explicit adapters were informed by [DefaultEcs-Sample](https://github.com/Drovss/DefaultEcs-Sample/tree/9d86ab3c9ad897b0c19af84b85e41be1be07c88e/Assets/Scripts/Game/ECS). Sokoban retains pure gameplay data and session-owned entities rather than copying that sample's Unity-dependent gameplay model or view-driven entity lifetime. A later Zenject `ITickable` can replace `EcsRunner` as the single frame driver; the systems and pipeline need not change.

### Verification

The preceding session/view split passed 91 EditMode and 6 PlayMode cases on 2026-10-20. The initial ordered pipeline was verified on 2026-10-21 through Unity MCP/CLI in Unity `6000.0.63f1`, with DefaultEcs `0.17.2`:

- Compilation succeeded. The compiled Domain references only `netstandard` and `DefaultEcs`; Application references only `netstandard`, `Sokoban.Domain`, and `DefaultEcs`.
- All 109 EditMode cases passed. The original 83 rule/equality/map/installation test sources remain unchanged. Session tests now use request/tick semantics; new cases cover request consumption and rejection, idle and disabled ticks, multiple-player rejection, pipeline order, independent sessions, pending-request replacement, and disposal failure/ownership.
- All 13 PlayMode cases passed: 8 scene/input/lifecycle cases and 5 binding/pipeline cases. They cover same-tick complete-push projection, initial binding, board-local coordinates, simulation authority, unbinding without entity disposal, duplicate bindings, cleanup after session replacement, held/simultaneous keys, R priority, and restart only after systems finish.
- Existing scene serialization, prefab references, materials, components other than the new request type, and the core movement rules were preserved. The Editor returned to `HardcodedTestScene` outside Play Mode, with no current compilation errors. These tests do not cover standalone player builds.

Results are saved under ignored `Sokoban/Logs/pipeline-editmode-tests.json` and `Sokoban/Logs/pipeline-playmode-tests.json`. Earlier layer-refactor reports remain under `layers-*.json`. Zenject composition and asynchronous asset loading remain later milestones.

### ECS module and world-space revision verification

Implemented and verified on 2026-10-21 in Unity `6000.0.63f1`, DefaultEcs `0.17.2`, and Input System `1.20.1`:

- Compilation succeeded. Loaded Domain references remain only `netstandard` and `DefaultEcs`; Application references only `netstandard`, `Sokoban.Domain`, and `DefaultEcs`.
- All 109 EditMode and 18 PlayMode cases passed. The movement implementation and all EditMode test sources are unchanged. PlayMode coverage now asserts world-space placement beneath translated/rotated parents, initial projection, static ordinary floors, wall/goal synchronization, complete pushes, binding failure rollback/retry, missing presets, duplicate/foreign bindings, stale-session cleanup, and adapter reuse after disposal, alongside the existing keyboard/restart/unload cases.
- The first PlayMode run passed 17/18: the new immediate-restart assertion selected the inactive old board before Unity's deferred destruction. The test helper now selects the active board; the complete rerun passed 18/18.
- All three prefabs were saved and reloaded through Unity's prefab APIs. Their adapter/view references and asset GUIDs were verified; normalized visual offsets and scales were preserved. Existing scenes were not edited.
- No standalone player build was run. The coordinate contract is absolute world position; arbitrary parent scale/rotation normalization, Zenject composition, and asynchronous asset loading remain outside this revision.

Results are saved under ignored `Sokoban/Logs/ecs-module-editmode-tests.json` and `Sokoban/Logs/ecs-module-playmode-tests.json`.

## Implemented — Component-driven movement pipeline

Approved and implemented on 2026-10-21. The runtime uses EntitySet scans as requested for this iteration; the earlier EntityMap experiment remains documented above. No custom interfaces, forwarding systems, services, factories, or transaction framework were introduced.

### Plan and application contract

- `MoveRequest` remains on the player. First pending request wins; four directions and key priority are unchanged. Validation consumes the current request once. Invalid, blocked, missing-player, and multiple-player cases do not produce movement. Bounds remain wall-defined, with explicit integer-overflow rejection.
- `MoveTo` is a readonly value containing a target entity, expected source, and destination. It is an instruction inside a plan, not an independent per-entity intention component. `ValidatedMovement` is one readonly component on the requesting player, containing one or two ordered instructions. Validation publishes it only after the entire path is valid; pushes encode box first, player second.
- `ApplyMovementSystem` queries only plans, without requiring an owner's grid position or gameplay tags. Before any position writes, every target must belong to the current world, be alive/enabled, have an enabled position component, and still match its recorded source. Default/empty plans and duplicate targets are rejected. Cancellation, missing/stale positions, disabled targets, destroyed entities, reused entity IDs, and foreign worlds cannot produce half a push. Application consumes the plan on success and rejection and never consumes a later `MoveRequest`.
- Cancellation before validation removes `MoveRequest`; after validation it removes `ValidatedMovement`. Destroying the plan owner removes the complete plan. Destroying a different participant makes preflight reject the whole plan. There are no separately installed box intentions to orphan.
- Validation and application are adjacent, synchronous stages. No occupancy-changing system runs between them. Enable/disable the configured movement pair together. Validation clears abandoned output before creating fresh plans, including plans on disabled owners. In the normal pipeline, no plan survives a completed tick. Other future plan producers must run after this cleanup and are responsible for producing non-conflicting valid operations; simultaneous movement planning is not implemented.
- Both stages implement `ISystem<float>` with explicit disposed checks and own their EntitySets. Validation snapshots players before consuming requests; application snapshots plan owners before removing components. Spatial scans only read component values. World enumeration is used for transient cleanup, including disabled owners; component removal does not change its entity membership. Every system releases queries before session-owned world disposal.

The atomicity guarantee is rejection before any writes and completion of all validated writes before subsequent systems. `Set(GridPosition)` publishes synchronously per entity: an observer of the first write may see the second participant's old position. Movement-phase observers must not mutate participants/occupancy, re-enter the pipeline, or throw during commit. There is no rollback after arbitrary observer exceptions and no guarantee against external occupancy changes inserted between the stages. Such mutations must be deferred to a boundary outside the movement phase.

### Verification

- Baseline execution: all 109 EditMode cases passed; PlayMode passed 18/20. The two failures were the pre-existing transform-component notification assertions, inconsistent with the user's refactored projection code. The user explicitly selected keeping direct Transform updates and adapting those tests.
- Final execution in Unity `6000.0.63f1`, DefaultEcs `0.17.2`, and Input System `1.20.1`: compilation succeeded, all 143 EditMode cases passed, and all 22 PlayMode cases passed.
- Existing movement behavior tests now submit commands to the real validation/application pipeline. Equality, ECS installation, EntityMap characterization, level-definition, keyboard-scene, and lifecycle coverage remain. Added 34 pure cases cover stage outputs, validation without position writes, generic application to untagged/player/box/goal entities, multi-plan query removal, stale/default plans, cancellation, both push participants' destruction, source changes, missing/disabled components, entity-ID reuse, foreign/disposed worlds, ordering, later requests, and disposal. Two new PlayMode cases isolate directional input and restart cancellation.
- Loaded Domain references remain only `netstandard` and `DefaultEcs`; Application references only `netstandard`, `Sokoban.Domain`, and `DefaultEcs`. No UnityEngine, Zenject, UniTask, or Addressables dependencies were introduced into the pure assemblies.
- Existing transforms, adapter code, prefab and scene assets were preserved. The Editor returned to `HardcodedTestScene`, stopped and without unsaved scene changes. No standalone player build was run.

Detailed baseline/final reports are under ignored `Sokoban/Logs/movement-split-*.json`.
