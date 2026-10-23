# LevelRun and completion flow

Implemented and verified on 2026-10-23. Work remains uncommitted, following the user's clarification. This manifest describes changes made during this task, separate from the substantial pre-existing working-tree changes.

## Behavior

GameplayController owns one internal LevelRun. LevelRun constructs Simulation then ViewLayer, requests views with an initial Tick(0f), ticks simulation before presentation, and disposes presentation before simulation. Construction failure releases acquired layers and rethrows; normal disposal uses try/finally so a view disposal exception cannot skip simulation disposal. IWorldLayer and its two implementations remain.

Loading returns before input, ticking, or controls. Otherwise the run ticks, then the controller advances its state, then resolves N before R. The requested index is stored before loading; Failed + R reloads that index, including after an unsuccessful N load. Other R commands rebuild from the existing definition. Loading ignores R/N.

RuleFeatures orders MovementFeature then WinFeature. WinCheckSystem loops directly through goal and box entity sets, with no LINQ, HashSet, arrays, or per-tick collection creation. When every goal is covered, it adds LevelCompletedTag to the player exactly once. Simulation.IsCompleted and LevelRun.IsCompleted expose the tag. Core completion is latched; the controller owns the policy of withholding further movement.

LevelParser rejects already solved input after the box/goal-count rule, using the requested exact diagnostic. Shipped levels remain unchanged.

Playing becomes Completed after a winning simulation tick. Completed stops movement input, keeps layers ticking, and accumulates subsequent deltaTime values. After 0.75 seconds the controller loads the next index, temporarily wrapping at Count. That tick returns after initiating the load so synchronous completion cannot cause a second control action. R works during Completed before this automatic transition. Keyboard N is compiled for UNITY_EDITOR or DEVELOPMENT_BUILD; ordinary players return false for N.

## File manifest

Added source files, under Sokoban/Assets/_Sokoban/Code:

- Unity/LevelRun.cs
- Core/Components/LevelCompletedTag.cs
- Core/Features/WinFeature.cs
- Core/Systems/WinCheckSystem.cs

Unity generated the four corresponding .cs.meta files during import. No metadata was hand-edited.

Changed source files under the same root:

- Unity/GameplayController.cs: LevelRun ownership, single command dispatch, failed-index retry, completion timer and progression.
- Unity/KeyboardMoveInput.cs: conditional N shortcut and required-keyboard input contract.
- Core/Simulation.cs: IsCompleted query.
- Core/RuleFeatures.cs: numbered WinFeature entry after movement.
- Core/LevelParser.cs: already-solved rejection.

Changed tests under Sokoban/Assets/_Sokoban/Tests:

- EditMode/Application/SimulationTests.cs: one-box completion/idempotence and two-box partial/final completion cases.
- Core/LevelParserTests.cs: exact already-solved diagnostic case.
- Core/LevelFilesTests.cs: before/after IsCompleted assertions in existing solver replay.
- PlayMode/HardcodedLevelTests.cs: existing reflection helper follows LevelRun ownership.
- PlayMode/PresentationPipelineTests.cs: existing reflection helper and paused-Loading expectation.
- PlayMode/StartupGateTests.cs: existing reflection helper/null-run assertion, Failed + R reload expectations, and a non-completed fixture with one off-goal box. No PlayMode cases were added.

Documentation: changed ARCHITECTURE.md and added this report. No files were deleted by this task. No scenes, prefabs, authoring assets, packages, asmdefs, or project settings were edited for the implementation. Temporary runtime verification objects/settings were cleaned up.

## Guard review

This review covers the production files changed by this task. Test assertions and fixture branches remain test logic; fixture teardown still skips Unity objects already destroyed during the scenario. Unchanged Addressables cache, text-loading, binding, and view-layer implementations were not broadened into a separate guard refactor.

Removed:

| Location | Check | Reason |
| --- | --- | --- |
| GameplayController.Tick | `_level == null` restart special case | Failed + R always reloads the attempted index; other states have a definition. |
| GameplayController.Tick | Second `_state != Loading` check before N | Single N/else-if-R dispatch prevents multiple commands; the early Loading return handles loading. |
| KeyboardMoveInput.NextLevelPressed | `keyboard != null` | User explicitly requires a keyboard; missing input hardware is no longer converted into an unpressed N key. |
| KeyboardMoveInput.RestartPressed | `keyboard != null` | Same required-keyboard contract for R. |
| KeyboardMoveInput.ReadMove | `null => default` | Same required-keyboard contract for movement; absent hardware now fails at access rather than returning no command. |

Retained or introduced:

| Location | Check | Reason |
| --- | --- | --- |
| GameplayController.Tick | Early Loading return | Required state policy: no input, ticks, or controls during loading. |
| GameplayController.Tick | Playing input check | Required state policy: movement only while playing. |
| GameplayController.Tick | BindingViews switch and readiness cases | Required startup state transitions; failure has priority in ViewLayer. |
| GameplayController.Tick | Playing + IsCompleted | Required win-to-Completed transition. |
| GameplayController.Tick | Completed timer threshold | Required automatic progression domain policy. |
| GameplayController.Tick | NextLevelPressed / else-if RestartPressed | Command selection with exactly one winner; N takes priority. |
| GameplayController.Restart | Failed state branch | Explicit requested policy: reload on failure, rebuild otherwise. |
| GameplayController.Tick / ReleaseRun | `_run?.Tick` / `_run?.Dispose` | Explicitly requested optional plain-C# run: initial loading or failed construction can leave it absent. |
| GameplayController.LoadLevelAsync | Cancellation check immediately after await | Prevent a late definition from starting a run after cancellation/disposal. |
| GameplayController.LoadLevelAsync | Cancellation and exception catches | Observe the owned async operation; cancellation is quiet, failure logs once and enters Failed. Existing .Forget call sites remain bounded by this handling. |
| GameplayController.StartRun | Constructor exception catch | Log construction failure once and enter Failed without publishing a partial run. |
| LevelRun constructor | `_viewLayer?.Dispose` / `_simulation?.Dispose` | Explicitly requested acquisition cleanup: a constructor can fail before either resource is assigned. These are plain C# classes. |
| LevelRun construction/disposal; controller release | try/catch or try/finally cleanup | Required resource ownership and reverse order; no disposed flags or repeat-disposal masking. |
| Simulation constructor | Null level argument | Existing Core public-boundary validation. |
| Simulation.Send | Cardinal command and pending MoveRequest checks | Existing domain rules; invalid/no-op commands are ignored and the first pending request wins. |
| Simulation constructor/disposal | Acquisition catch and disposal finally | Existing ownership: release the world on construction failure or throwing pipeline disposal. |
| LevelParser.Parse | Null/empty text, blank/ragged rows, invalid-symbol switch | Existing external-data validation and actionable diagnostics. |
| LevelParser.Parse | Exactly one player; nonzero boxes; equal box/goal counts | Existing domain requirements. |
| LevelParser.Parse | Off-goal-box search and alreadySolved rejection | New requested domain rule, checked after count validation. |
| LevelParser.ValidateEnclosure | Edge-cell check; wall and visited checks | Existing enclosure rule and traversal termination. |
| WinCheckSystem.Update | Absence of LevelCompletedTag | Required idempotence; completion is written once through entity.Set. |
| WinCheckSystem.AllGoalsCovered | Position match and uncovered-goal return | Required win rule. Zero goals satisfies the universal check; parser input rejects zero-box/zero-goal levels, while low-level LevelDefinition still permits synthetic test data. |
| KeyboardMoveInput.NextLevelPressed | UNITY_EDITOR / DEVELOPMENT_BUILD | Requested compile-time shortcut availability, with false in ordinary players. |

The user explicitly chose to remove missing-keyboard handling. KeyboardMoveInput requires Keyboard.current to exist for movement, restart, and the Editor/development next-level shortcut. The ordinary-player N branch still returns false without reading the keyboard.

No _disposed, ThrowIfDisposed, initialized flags, entity.IsAlive checks, or local system IsEnabled guards were added. WinCheckSystem has the IsEnabled property required by ISystem, without a local check. No ?. or ?? is used on UnityEngine.Object. No new code comments were added except the requested numbered feature marker. Both movement files are byte-for-byte unchanged.

## Verification

- Installed versions checked: Unity 6000.0.63f1, DefaultEcs 0.17.2, Zenject 9.2.0, UniTask 2.5.11, Addressables 4.1.1, Unity Test Framework 1.6.0. CLI 1.0.0-beta.14 was confirmed current.
- Unity compilation succeeded after steps 1, 2, 3, and 4.
- All 167 project EditMode cases passed, including the three new cases and replaying all shipped levels.
- All 53 existing project PlayMode cases passed; none were added.
- After the user's required-keyboard clarification, the three missing-keyboard checks were removed, Unity compilation succeeded again, and all 53 existing project PlayMode cases passed again. The guard review is complete.
- Runtime scene startup reached Playing/Ready and frame advancement was confirmed.
- Applying the real six-move level-1 solution through run commands reached Completed. The next load began after 0.7534981 game seconds, and level 2 reached Playing/Ready, with 167 observed frames advancing across the transition.
- A second runtime check injected keyboard R during Completed: it replaced the run, entered BindingViews, and reached Playing. Injecting keyboard N requested index 1 and entered Loading. Temporary keyboard/input settings were restored.
- An invalid Addressables level reference was injected into a temporary cloned LevelList, without editing the authoring asset. Loading the requested index 2 produced exactly one captured exception and Failed. Restoring the source and invoking the same Restart path retried index 2 and recovered to Playing. The temporary clone was destroyed.
- Both Play Mode sessions exited without additional captured errors. The original runInBackground=false setting was restored and GameplayScene remained clean.
- Compiled Core references remain only netstandard and DefaultEcs.
- SHA-256 hashes of MovementValidationSystem.cs and ApplyMovementSystem.cs match the saved task-start hashes.
- Focused source diff whitespace checks passed. A full-worktree check reports pre-existing trailing spaces in LevelViewCatalog.asset, which was not edited by this task.

Saved results are in ignored Sokoban/Logs/level-run-task/: editmode.json, playmode.json, required-keyboard-playmode.json, completion-smoke.json, completed-controls-smoke.json, load-failure-smoke.json, and movement-baseline.json.

No standalone player build was run. The ordinary-player N branch was inspected in source; runtime keyboard N was checked in the Editor. The earlier review's gaps in existing partial-world-disposal and deferred-root-destruction assertions remain: these tests still do not independently prove those cleanup claims. This task adds no PlayMode tests or production test seams to address them.

## Manual checklist

1. Open GameplayScene and enter Play Mode. Solve level 1 by hand (Left, Up, Down, Right, Right, Up from its initial position). Confirm movement input stops for about 0.75 seconds, then level 2 appears.
2. Press R while binding, playing, and during the completion pause. Confirm it restarts the same level. In Failed, confirm R reloads the attempted level index. R/N during Loading are intentionally ignored.
3. Press N in the Editor or a development build to cycle levels. In an ordinary non-development player, verify N has no effect.
4. Temporarily break a level reference, load that index, and confirm one exception plus Failed. Correct the reference and press R; confirm the same attempted index retries successfully. Restore the authoring asset afterward.
5. Open Window > General > Test Runner. In EditMode, select the project's Sokoban test assemblies and run them: expect 167 cases. In PlayMode, run Sokoban.PlayModeTests: expect 53 cases. Expected fault-injection exceptions are asserted by tests.
