# Sokoban architecture

Current implementation, verified on 2026-10-22. Earlier experiments are preserved in [architecture history](iterations/ARCHITECTURE-HISTORY.md).

## Purpose

Sokoban is a small test harness for deterministic DefaultEcs rules, explicit OOP ownership, and later UniTask/Addressables lifecycle experiments. Gameplay remains intentionally minimal. Zenject composes the Unity layer; asynchronous loading is future work.

## Assemblies and folders

All runtime code is under `Sokoban/Assets/_Sokoban/Code/`.

| Folder | Assembly | Responsibility |
| --- | --- | --- |
| `Core` | `Sokoban.Core` | Components, commands, level data/population, rule systems, features, and simulation ownership. |
| `Unity` | `Sokoban.Unity` | Installers, controller, input, level provider/sample content, views, and presentation systems. |

Core has `noEngineReferences: true`, no assembly-definition references, `overrideReferences: true`, and `precompiledReferences: ["DefaultEcs.dll"]`. Its compiled dependencies are only netstandard and DefaultEcs. Core contains no UnityEngine, Zenject, UniTask, or Addressables references and knows nothing about restart or progression.

Unity references Core, Zenject, and Unity Input System. Its adapters and view systems also use the installed DefaultEcs plugin. The previous Domain, App, and Presentation assembly split has been replaced; no runtime asmref bridge remains.

Core's exported types are exactly Simulation, IWorldLayer, SystemPipeline, IFeature, RuleFeatures, LevelDefinition, MoveCommand, and the component types. LevelLoader, MovementFeature, and both movement rule systems are internal. Targeted InternalsVisibleTo attributes grant access only to the three existing test assemblies. Sokoban.Unity has no friend access and uses the public Core API. The existing Sokoban.App.EditModeTests assembly name remains a test-assembly name only; no Sokoban.App runtime assembly exists.

## Core data and movement

- GridPosition is an immutable integer X/Y value with equality and hashing.
- PlayerTag, BoxTag, WallTag, and GoalTag are empty readonly tags.
- MoveCommand describes one cardinal direction. Default and undefined values are ignored by Simulation.Send.
- MoveRequest is pending input. Send never overwrites an existing request: first request wins.
- MovementStep contains a target entity, ExpectedSource, and Destination.
- MovementPlan contains one or two ordered steps. Push plans put the box before the player.

MovementValidationSystem consumes a request once, scans EntitySets for walls and boxes, and publishes a completely checked plan. It never writes positions. Invalid or blocked requests are consumed; integer overflow rejects movement. It also discards abandoned plans, including plans on disabled owners.

ApplyMovementSystem snapshots plan owners, checks the whole plan and all participants, consumes the plan, then applies positions with entity.Set(step.Destination). It has no gameplay-tag knowledge. Destroyed, disabled, repositioned, foreign-world, or missing participants reject the whole plan before writes.

Exactly one positioned player and non-overlapping solid entities are assumed. Goals may overlap players or boxes. LevelDefinition checks dimensions and null collections but does not perform full occupancy validation. Cells without walls or boxes are traversable; there are no extra board bounds.

Atomicity means complete preflight before writes and complete application before following systems. DefaultEcs notifications are synchronous, so a push observer can see the box update before the player update. Observers must not mutate participants, re-enter movement, or throw during commit. There is no rollback for arbitrary observer exceptions.

## Features, pipelines, and layers

IFeature exposes only Create(World), returning an ISystem<float>. RuleFeatures.Create() supplies the explicit Core list: MovementFeature. ViewFeatures.Create() supplies the explicit presentation list: TransformSyncFeature. Both lists have numbered entries. There is no numeric order, sorting, uniqueness validation, or dependence on Zenject binding order.

SystemPipeline creates feature systems in list order and owns one SequentialSystem<float>. If construction fails, it disposes successfully returned systems in reverse order and rethrows. A feature owns any partial resources until Create returns. Normal Dispose delegates once to SequentialSystem, which disposes its children in reverse order in the installed DefaultEcs 0.17.2. No exception aggregation or ownership flags are used; a cleanup exception propagates and can interrupt remaining child cleanup.

The installed SequentialSystem.Update checks its own IsEnabled, but does not inspect child IsEnabled properties. Following the explicit decision, the rule systems retain only plain interface properties and have no local enable checks. Disabling individual rule systems is unsupported; disabling applies to the aggregate SequentialSystem. No new public enable API was introduced on SystemPipeline.

MovementFeature creates validation followed immediately by application. There is no win rule, so no WinFeature was introduced. Simulation.IsCompleted remains false.

Simulation and ViewLayer implement Core's public IWorldLayer (Tick and IDisposable) over the same World. Simulation's public constructor accepts only LevelDefinition and uses RuleFeatures.Create(); its internal constructor accepts IReadOnlyList<IFeature> for existing composition tests. It owns a fresh World, populates it through LevelLoader, then builds SystemPipeline. Constructor failure disposes the world after the pipeline cleans up its returned systems.

Simulation exposes Send(MoveCommand), Tick(float), IsCompleted, World, and Dispose. Send only queues a request. Tick executes synchronous rules. Dispose releases the pipeline, then the world; a simple finally ensures world release if pipeline disposal throws. There are no disposed flags, disposed-object checks, or idempotence guarantees. Ownership requires one disposal and no later Send/Tick calls.

World access supports view construction and projection during the simulation lifetime. DefaultEcs.World is mutable; views must not change gameplay state. Adapters may install presentation components such as TransformComponent. Gameplay input goes through Send. Raw world/entity handles must not be reused for a replacement simulation.

## Unity input and frame order

GameplayController is a plain C# class implementing Zenject IInitializable, ITickable, and IDisposable. It is the only production tickable and the only application owner that constructs/replaces/disposes a Simulation and ViewLayer.

```text
Zenject -> GameplayController.Tick
  -> IPlayerInput.ReadMove -> Simulation.Send
  -> tick List<IWorldLayer> in order
     -> Simulation.Tick
        -> explicit Core features
           -> MovementValidationSystem
           -> ApplyMovementSystem
     -> ViewLayer.Tick
        -> explicit view features
           -> GridTransformSyncSystem
  -> read RestartPressed / handle R
     -> dispose old view, then old simulation
     -> create replacement simulation and view
```

KeyboardMoveInput implements IPlayerInput. It reads new presses using Up/W, Down/S, Left/A, Right/D priority. Held keys do not repeat steps. KeyboardInputSystem has been removed from the ECS pipeline.

Restart is handled after simulation and view updates. A simultaneous movement press and R can update the old world and notify its observers before replacement. The replacement world starts at initial positions. This ordering was explicitly selected during the refactor. RequestRestart had only test callers and was removed. RestartPressed is read after all layers finish; no restart flag is needed.

There is no IProgress implementation, next-level command handling, or win rule to reuse. The controller therefore implements restart only. ILevelProvider and HardcodedLevelProvider supply the existing hardcoded definition, with sample content in the Unity layer.

## Ownership and failure cleanup

| Owner | Resources |
| --- | --- |
| Simulation | One world and its complete Core feature pipeline. |
| ViewLayer | One LevelView and a SystemPipeline of presentation systems over Simulation.World. |
| LevelView | Generated GameObjects and their hierarchy. |
| GameplayController | Concrete Current simulation and an ordered List<IWorldLayer> (simulation, view), startup/restart, and teardown. |
| Zenject | Scene services and GameplayController initialization/ticking/disposal. |

Normal teardown walks the layer list in reverse: ViewLayer disables and schedules destruction of its hierarchy before disposing its pipeline; Simulation disposes its pipeline before its world. The controller then clears the list and Current. Zenject guarantees Initialize before Tick and Dispose last. Application lifecycle guards are removed: callers must respect that order and single ownership. Deferred GameObject destruction never touches entity handles and cannot affect a replacement simulation.

If view construction fails, ViewLayer removes its partial hierarchy and presentation systems; the controller disposes the newly created simulation. If simulation construction fails, Simulation performs its own cleanup. The controller retains no half-built current level. Construction errors propagate; continuing normal ticks after failed construction is invalid. Explicit reconstruction requires restoring valid inputs and entering initialization again, as exercised by the existing failure fixture; automatic recovery is not implemented.

## Views and coordinates

LevelView builds floors, entity views, and the placeholder player. ViewLayer owns GridTransformSyncSystem separately from Core rules and updates it after Simulation.Tick.

Every newly instantiated view is explicitly snapped to GridCoordinates.ToWorld(position), mapping grid X/Y to absolute Unity X/Z as (X, 0, Y). Initial placement never depends on a zero-delta simulation or view-system update. Ordinary floors have no ECS binding and stay static. Players, boxes, walls, and goal floors install TransformComponent and are projected by the view system.

Prefab roots carry EntityView and TransformAdapter. EntityAdapter exposes Install(Entity). ComponentAdapter stores serialized data and installs it with Set, rejecting duplicate components. Adapters never create/dispose gameplay entities, retain entity handles, or remove individual components on destruction. The world owns installed components. Transform synchronization skips destroyed/missing Unity transforms and never writes gameplay positions or TransformComponent back into ECS.

Prefab roots represent the walkable surface; local Visual offsets correct mesh pivots. Floor tops and wall/box bottoms remain at Y=0. Scene, prefab, and script identities were preserved during moves.

## Installers and harness

GameplayScene's SceneContext contains:

- ServicesInstaller: IPlayerInput, ILevelProvider, ViewCatalog, and LevelRoot (a wrapper around the serialized scene Transform).
- FeaturesInstaller: ViewFeatures.AsSingle(). Core rule features are supplied by RuleFeatures, not Zenject.
- GameplayInstaller: BindInterfacesAndSelfTo<GameplayController>().AsSingle().

Worlds, rule pipelines, view pipelines, and simulations are never globally bound. SystemsInstaller was replaced by FeaturesInstaller; no FlowInstaller existed.

Open `Sokoban/Assets/_Sokoban/Scenes/GameplayScene.unity`. The unchanged 7x7 board has one player, two boxes, two green goals, and border walls. A solution is Left, Up, Down, Right, Right, Up. WASD/arrows move and R restarts. Assets remain direct references through LevelViewCatalog.asset. No animation, undo, new lookup mechanism, completion feature, progression, or packages were added.

## Removed/replaced runtime files

- LevelFlow.cs, LevelScope.cs, LevelScopeFactory.cs, LevelSession.cs, SystemUpdater.cs, and KeyboardInputSystem.cs were removed with their metadata.
- SystemsInstaller.cs was renamed/replaced by FeaturesInstaller.cs while preserving its script GUID.
- The former services-bearing GameplayInstaller.cs became ServicesInstaller.cs with its serialized references preserved; the new GameplayInstaller binds only the controller.
- IMoveInput.cs became IPlayerInput.cs.
- Sokoban.App.asmdef and the old bridge asmref were removed. Domain and Presentation asmdefs became Sokoban.Core and Sokoban.Unity respectively.

## Verification

Verified on 2026-10-22 with Unity 6000.0.63f1, DefaultEcs 0.17.2, Zenject/Extenject 9.2.0, and Input System 1.20.1:

- All five steps of the layer/list/guard refactor compiled before continuing.
- The layer/list/guard refactor passed 142 EditMode and 24 PlayMode cases. Existing cases were migrated for explicit list order, aggregate enable behavior, and single ownership. One obsolete EditMode case testing only disposed-object exceptions was removed. The failed-view fixture injects its invalid reference in memory and restores it, without serializing a scene object into the catalog asset.
- After test cleanup, all 128 EditMode and 24 PlayMode cases passed. Removed four basic DefaultEcs installation/query cases and ten EntityMap experiment cases (production uses EntitySets). Consolidated one duplicate push case into the four-direction movement test, moved first-pending-command coverage into a focused Simulation EditMode case, and strengthened scene-unload checks for world/controller cleanup. Simulation tests use the public default composition except when explicitly testing custom feature composition. Test names now follow Simulation, MovementPlan, and pipeline terminology; the renamed SimulationTests.cs retains its GUID. Results are saved locally in the ignored Sokoban/Logs/test-cleanup-editmode.json and test-cleanup-playmode.json files.
- Compiled runtime assemblies are exactly Sokoban.Core and Sokoban.Unity. Core references only netstandard and DefaultEcs and exports only the requested surface. Source scanning found no leftover UnityEngine or Zenject dependencies in Core.
- GameplayController is the only runtime ITickable. The saved scene contains the three expected installers and no missing scripts. All three existing prefabs retain valid EntityView/TransformAdapter bindings and their original GUIDs.
- The guard-by-guard decisions, library findings, and cleanup limits are recorded in [GUARD-REVIEW.md](GUARD-REVIEW.md).
- No commits were created, following the latest instruction. No standalone player build was run.

## Future asynchronous work

Future UniTask/Addressables workflows belong in Unity infrastructure. Completions must validate the originating simulation/controller lifecycle before installing presentation data. Cancellation, stale results, failures, and asset-handle release must remain explicit; unobserved fire-and-forget tasks are not the default.
