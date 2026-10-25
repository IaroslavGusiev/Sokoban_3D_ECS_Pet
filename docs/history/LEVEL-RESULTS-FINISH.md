# Animation cleanup, level results, and finish flow

Implemented and verified on 2026-10-24. All changes remain uncommitted, as requested. Existing GameplayLifetime disposal ownership is preserved.

## Step 1 findings

- LevelTimerSystem already excludes both LevelCompletedTag and LevelTimedOutTag. Its timer freezes before the winning tick's decrement; this contract was retained.
- The original RuleFeatures order was MovementFeature (1), WinFeature (2), LevelTimerFeature (3). The implemented order is Movement (1), MoveCount (2), Win (3), Timer (4).
- GameplayController already passed GameplaySettings.MoveDurationSeconds explicitly into RunOptions. One PlayerAnimationTests call used RunOptions' optional default; it now creates settings and passes that value explicitly. The RunOptions default was removed. GameplaySettings already rejected non-finite/non-positive durations and was left unchanged.
- Project EditMode test assemblies are Sokoban.EditModeTests, Sokoban.App.EditModeTests, Sokoban.Core.Tests, and Sokoban.Editor.Tests. The first three reference Core only and disable engine references. Sokoban.Editor.Tests already referenced Presentation through Sokoban.Unity and now hosts the public animation tests; Core and explicit DefaultEcs/NUnit references were added. Sokoban.PlayModeTests already references Presentation and Core. No new asmdef or InternalsVisibleTo entry was needed.
- The attachment's controller-binding assumption was outdated. The user confirmed preserving GameplayLifetime; IGameplayStatus was added to the explicit shared controller contracts without registering its IDisposable contract.

## Implemented behavior

ActorViewState initializes at the start of Update through a dedicated query and reusable entity buffer. Initial state has HasFacing false. The first detected move sets facing; binding and projection preserve prefab rotation until then. Projection avoids unchanged transform writes. Duration, interpolation, input buffering/chaining, and push detection remain unchanged.

MoveCountSystem observes player position changes after movement and before win detection, counting at most once per tick. A push counts as one player move; blocked movement counts zero. Simulation.Result contains readonly Moves, ElapsedSeconds, and RemainingSeconds. Elapsed is the configured limit minus the frozen timer. The solver replay asserts its solution length equals Result.Moves for every level file.

GameplayController exposes IGameplayStatus and GameplayPhase. Entry into Completed appends one result and starts the existing completion pause. Progression loads the next level or enters Finished at the end. Finished retains the final run, ticks only presentation, accepts K, ignores movement/N, and uses R to clear results and load index 0. Ordinary restarts, timeouts, and development N do not append results. The development shortcut still wraps outside Finished. Results are a read-only completion history; no writable list is exposed.

GameplayDebugOverlay is a temporary IMGUI MonoBehaviour injected with IGameplayStatus. Before configs are available, LevelCount is zero and the overlay displays unknown count/time rather than reading unavailable config data. Once available it shows phase, 1-based level, remaining seconds, and moves; Finished adds result rows, totals, and the replay instruction. Scene attachment remains a manual step.

## Files added

Paths below are relative to Sokoban/Assets/_Sokoban unless otherwise stated.

- Code/Core/Components/MoveCount.cs
- Code/Core/Systems/MoveCountSystem.cs
- Code/Core/Features/MoveCountFeature.cs
- Code/Core/LevelResult.cs
- Code/Unity/GameplayPhase.cs
- Code/Unity/IGameplayStatus.cs
- Code/Unity/GameplayDebugOverlay.cs
- Tests/Editor/ActorMotionTests.cs
- Tests/Core/LevelResultTests.cs
- This report, docs/history/LEVEL-RESULTS-FINISH.md, relative to repository root.

Unity generated metadata for the nine new C# files. No metadata was edited by hand. Local verification reports are under ignored Sokoban/Logs/level-results-finish/.

## Files changed

- Code/Unity/Ecs/Components/ActorViewState.cs — HasFacing.
- Code/Unity/Ecs/Systems/ActorMotionSystem.cs — lazy initialization, reusable buffer, facing activation, query disposal, removed duration validation.
- Code/Unity/Ecs/Systems/GridTransformSyncSystem.cs — conditional writes and facing activation.
- Code/Unity/Ecs/Systems/ViewBindingSystem.cs — preserve prefab rotation until facing exists, including async rebinds.
- Code/Unity/RunOptions.cs — required explicit move duration.
- Code/Core/RuleFeatures.cs — move counting before win; numbered markers updated.
- Code/Core/Simulation.cs — initial MoveCount, retained time limit, public Result.
- Code/Unity/LevelRun.cs — forwards Result.
- Code/Unity/GameplayController.cs — public status, completion history, finish/replay behavior.
- Code/Unity/Installers/GameplayInstaller.cs — adds IGameplayStatus to existing controller contracts.
- Tests/Editor/Sokoban.Editor.Tests.asmdef — Core, DefaultEcs, and NUnit references for existing test assembly.
- Tests/PlayMode/PlayerAnimationTests.cs — explicit duration from settings and settings cleanup.
- Tests/Core/LevelFilesTests.cs — replay move-count assertion.
- Tests/PlayMode/StartupGateTests.cs — finish/replay, no-result actions, terminal simulation gating, updated progression expectations, public phase reads.
- Tests/PlayMode/HardcodedLevelTests.cs — verifies IGameplayStatus resolves to the shared controller and reads public Phase.
- Tests/PlayMode/ViewLayerFlowTests.cs — null-prefab exception expectation follows the initial-rotation branch.
- docs/ARCHITECTURE.md, docs/SETUP.md, docs/ASSEMBLIES.md, and docs/history/README.md — current contracts, setup, assembly references, and report index.

No files were deleted. MovementValidationSystem, ApplyMovementSystem, timer behavior, existing adapter classes, prefab/scene/config assets, animation assets, and friend declarations were not edited by this task.

## Guard audit

Only one defensive validation block was removed: ActorMotionSystem's non-finite/non-positive duration rejection. GameplaySettings remains the external-data validation boundary, and RunOptions callers now pass that validated settings value explicitly.

Retained checks and their reasons, covering the runtime code changed by this task:

| Location | Checks retained | Reason |
| --- | --- | --- |
| ActorMotionSystem.Update | Position changed; ViewMove exists; elapsed reached duration; player tag for IsPushing | Required movement/interpolation and player-versus-box behavior. |
| ActorMotionSystem.InitializeActors | Buffer capacity versus initialization count; Without ActorViewState query | Required safe snapshot-before-membership-change algorithm and reusable storage. No initialized flag or per-empty-frame allocation. |
| ActorMotionSystem.IsSuccessfulPush | Player actually moved; candidate is a box at the old destination; box delta matches player delta | Existing push detection, intentionally unchanged. |
| GridTransformSyncSystem | Unity Transform null check | Unity can independently destroy the referenced object. |
| GridTransformSyncSystem | ViewMove/component/tag presence; HasFacing; position/rotation differ | Select correct projection, preserve initial rotation, and perform only the requested writes. |
| ViewBindingSystem | Post-await cancellation; stale requested key checks, including Fail | Prevent obsolete async results from accessing a cancelled run or binding the wrong skin. |
| ViewBindingSystem | ViewMove/actor state/HasFacing/player presence when choosing pose; optional EntityView via TryGetComponent | Restore valid current presentation state while preserving existing support for prefabs without adapters. |
| ViewBindingSystem | Created Unity view non-null; cancellation check before logging/marking failure; cancellation/failure catches | Release partially acquired Unity resources and preserve async failure behavior. |
| Simulation | Finite positive time limit; finite nonnegative delta | Required Core public-boundary argument validation. |
| Simulation.Send | Cardinal command, no pending MoveRequest, not completed/timed out | Existing deterministic domain rules. |
| Simulation construction/disposal | Catch/finally resource cleanup | Release acquired world/pipeline resources on failure; no disposal flags. |
| MoveCountSystem | Current GridPosition differs from LastPosition | Definition of a counted move; unchanged positions do not count or write. |
| LevelRun.Tick/Send | No active visual movement before submission; one pending cardinal command; reject further buffered/terminal commands | Approved movement timing and first-command-preserving buffer rules. |
| LevelRun construction/disposal | Nullable managed resources in partial-construction cleanup; nested finally | Failure can occur before all acquisitions; attempt each owned cleanup. |
| GameplayController.Tick and handlers | Loading gate, explicit command-processing outcome, readiness/timeout/win/completion-delay phases, next-level bounds, Finished R/N rules | Application state-machine and progression rules. |
| GameplayController | Optional managed run for skin input, status defaults, LevelFailed view ticking, and ReleaseRun | Loading/LevelFailed legitimately may have no run; failure/restart cleanup and public status must handle that domain state. These are not UnityEngine.Object references. |
| GameplayController loading | Token checks after both awaits; cancellation/failure catches | Existing async ownership and recovery boundary. |
| GameplayDebugOverlay | LevelCount greater than zero before reading config-backed time; Finished branch; result iteration bounds | Display available data and phase-specific content. Configs can genuinely be pending or failed. No missing-injection guard was added. |
| Tests | Unity-object cleanup checks; fixture/input branches and assertions | Unity destruction and deliberate scenario setup/verification, not runtime defensive fallback. |

GameplaySettings' existing validation of time limit, completion delay, and move duration remains intact. ActorMotionSystem.IsEnabled is the required interface property; no local IsEnabled guard was added. No _disposed, ThrowIfDisposed, initialized flags, entity.IsAlive checks, Unity-object ?. or ?? expressions, redundant status fallback, or extra constructor guards were introduced. New struct component writes use entity.Set(...).

## Verification

- Step 2 compiled and all 14 tests in Sokoban.Editor.Tests passed.
- Step 3 compiled and all 204 project EditMode tests passed, including all shipped-level solver replays and new result tests.
- Step 4 compiled and all 136 project PlayMode tests passed, including finish/replay, ignored Finished controls, skin changes, no result on timeout/restart/N, IGameplayStatus resolution, prior async/lifecycle tests, and existing animation/chaining checks.
- No tests were skipped for lack of scaffolding; the finish test reuses existing fakes.
- Unity 6000.0.63f1 and the installed/current Unity CLI 1.0.0-beta.14 were used. No packages were added.
- No standalone player build or manual saved-scene overlay check is claimed. The overlay has deliberately not been added to the saved scene.

## Manual editor steps

1. Wait for Unity to import the new scripts and generate their metadata. No new asmdef was created. The existing Sokoban.Editor.Tests reference changes apply automatically; confirm compilation has no errors.
2. Open Assets/_Sokoban/Scenes/GameplayScene.unity. Create an active scene GameObject, for example GameplayDebugOverlay, outside generated Level view roots, and add the GameplayDebugOverlay component. Keep it in the gameplay SceneContext's scene. Zenject finds its Inject method and supplies IGameplayStatus; no extra binding or serialized reference is needed. Save the scene.
3. Select Assets/Addressables/Config/GameplaySettings.asset. Confirm Move Duration Seconds is finite and greater than zero; 0.16 preserves the approved feel. Keep the existing valid time limit and completion delay. Rebuild Addressables if you change the asset; this code-only task did not change saved config data.
4. Enter Play Mode with a connected keyboard. The corner box should show phase, Level n / N, remaining seconds to one decimal, and moves. Before configs load, unknown count/time is expected.

## Manual gameplay checklist

- [ ] Successful walks and pushes add exactly one move; walls and blocked pushes add none.
- [ ] The winning move is already included when Completed appears.
- [ ] Timeout and R restart append no result and reset the current run's count/budget.
- [ ] Finishing level 1 shows the configured completion pause and then level 2.
- [ ] Finishing the last level shows Finished, one row per completed level in a normal full playthrough, totals, the replay instruction, and the last board still on screen.
- [ ] Repeated Finished frames do not append duplicate results or advance simulation/time.
- [ ] K changes skins in Finished; N and movement do nothing there.
- [ ] R in Finished returns to level 1 with empty results and a fresh move counter/timer.
- [ ] Editor/development N still cycles and wraps outside Finished without recording a result.
- [ ] A player prefab with initial rotation retains it until the first move; movement facing survives a skin replacement.
- [ ] Pushing, mid-move skin changes, configured visual duration, and chained key presses feel unchanged.
- [ ] All project EditMode and PlayMode tests pass in Window > General > Test Runner.
