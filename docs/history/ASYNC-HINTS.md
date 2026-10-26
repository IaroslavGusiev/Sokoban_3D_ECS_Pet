# Asynchronous hints

Implemented and verified on 2026-10-25, starting from commit e95eecd (deterministic recording/replay) with a clean working tree. Changes remain uncommitted.

## Implementation

- Pure Core HintSolver uses breadth-first search over copied immutable LevelDefinition data, sorted box states, cooperative cancellation, and an explicit visited-state budget. The application service runs it on Task.Run with a 100,000-state cap. Found, NoSolution, BudgetExhausted, and AlreadySolved are distinct results. The independent test-only LevelSolver remains unchanged.
- BoardRevision lives on the existing level-state entity, starts at zero, and increases after successful forward/undo commits. Invalid/blocked moves, failed undo, idle/time updates, and skins do not increment it. GameplaySnapshot includes it, and normal replay reconstructs it.
- GameplayController captures run reference, revision, and request identity. It applies completion data only in its normal tick after checking current Playing/readiness/motion and all identities. Invalidation cancels and hides; old successes and failures cannot update status or visuals.
- HintCalculation observes cancellation/errors even after retirement. The detached static observer retains only copied input, service, and cancellation owner, not controller/run/world/view references. Source disposal and cancellation synchronize. No new unobserved fire-and-forget task or main-thread blocking is introduced.
- HintArrow is a lazy, reusable run-root child. Its project-owned prefab/mesh/yellow HDRP unlit material have no collider, ECS entity, or Addressables request. The saved scene assigns it through ServicesInstaller. The existing IMGUI panel adds Hint, disabled Calculating…, and outcome/direction labels.
- No package versions, Addressables groups/catalogs, movement/undo rules, timer policy, player animation, box feedback, or keyboard mappings were changed.

## Verification performed

| Project suite | Fresh baseline | Final |
| --- | --- | --- |
| EditMode (Sokoban assemblies) | 257 passed / 257 | 268 passed / 268 |
| PlayMode (Sokoban.PlayModeTests) | 150 passed / 150 | 167 passed / 167 |

Compared FullName and Status for each original case: none missing, none regressed. No ignored, explicit, expected-failure, or deleted behavioral assertions were introduced. The first focused lifecycle run exposed an exact Vector3 equality in a new quaternion-facing assertion; that new assertion now uses a 1e-5 distance tolerance, and the final full suite passes.

New coverage includes shipped-level recommendations checked through real Core against the independent shortest-solution oracle, unsolvable/budget/cancellation outcomes, revision/replay behavior, movement/undo invalidation, returning to identical positions, stale successes/failures versus newer requests, same-level restart, terminal/timeout/disposal cleanup, cancellation-ignoring completion, responsive clock/input, unavailable clicks, lazy reuse, no extra provider loads, all four saved-prefab directions, and the production scene/service/prefab path. Existing reverse-preflight assertions also verify that failed undo preserves the revision and a successful retry advances it.

The initial GameplayScene inspection confirmed advancing frames before capture, then used paused manual controller ticks for reproducible screenshots. A controlled pending backend kept the real overlay at disabled Calculating…; the original HintService performed the actual worker search and returned Left for level 1. The arrow and Hint: Left were visible, with no move executed by the hint and no colliders. A successful move hid it, and restart left zero arrow instances with a fresh 60-second timer. That initial check inspected UI visibility but drove commands through the controller API. Native mouse verification was completed in the follow-up below. The original service, tick registration, background-running setting, and stopped Editor state were restored; no runtime test hooks were saved.

Raw baseline/final reports and screenshots are local ignored evidence under Sokoban/Logs/hints/: hintBaselineEdit.json, hintBaselinePlay.json, hintFinalEdit.json, hintFullPlay.json, hint-idle.png, hint-pending.png, hint-arrow.png, and hint-movement-cleanup.png. git diff --check passed. The saved scene is not dirty.

No dependency suite, standalone player build, or Addressables rebuild was run. Hint assets are direct scene dependencies and no Addressable content/dependency changed. Existing built-content scene tests passed.

## Verification follow-up: mid-search cancellation and native click

Completed on 2026-10-25 while preserving the existing uncommitted hint implementation and unrelated changes.

HintSolver has one internal overload accepting an optional state-expanded observation callback. The existing public overload passes null, and production services are unchanged. The callback runs after a state's neighbor expansion; it is not a public progress API. HintSolverCancellationTests lives in the existing Sokoban.EditModeTests friend assembly, with no new friend or assembly reference.

The worker expands three real search states before signaling a ManualResetEventSlim checkpoint and waiting on a second event. The test observes three expansions and an incomplete task, cancels, verifies the held task is still active, then releases the checkpoint in finally. The callback's wait does not use the token: the OperationCanceledException must come from the solver's own cancellation check. The task completes canceled, and the expansion count remains three. Ten-second waits are failure watchdogs only; no sleep or scheduling delay determines the sequence. The former immediate Task.Run cancellation case is now named AlreadyCancelledToken_IsRejectedBeforeSearch and retains explicit pre-canceled-token rejection.

The first focused run passed 9/10: cancellation threw correctly, but Task.IsCanceled was false because the test's Task.Run had not received the token on Unity's runtime. Passing the same token to Task.Run corrected that task-status assertion while preserving the deterministic in-flight checkpoint. Subsequent focused and full suites passed:

| Check | Result |
| --- | --- |
| Affected HintSolver tests | 10 / 10 passed |
| Project EditMode, Sokoban assemblies | 269 / 269 passed |
| Project PlayMode, Sokoban.PlayModeTests | 167 / 167 passed |

Compared names/statuses against the preceding 268/167 reports: the cancellation case has the explicit rename above, one new mid-search case was added, and every other previous case remains present and passes. Dependency tests and standalone builds were not run.

Native UI input used the Computer Use skill's Windows sky.click on the observed saved GameplayScene window. From Playing/HintStatus.None with zero moves, clicking the visible Hint button at window coordinate (1040, 587) initiated the unchanged production service and produced Hint: Left plus a left-facing yellow arrow. Read-only scene/status checks confirmed player world position (3, 0, 2), unchanged from before the click, and Moves == 0. No RequestHint call, reflection, service replacement, or synthetic Input System event initiated any of these hint requests. The shipped search completed too quickly to capture Calculating… during this native check; the earlier controlled pending-label capture remains separate evidence.

Short native keyboard presses did not produce movement in this Editor session. Neither did the tool's immediate press/down/up helper, so those attempts are not counted as movement verification. A separate raw InputSystem.QueueStateEvent for LeftArrow, left for the normal next-frame input update without a forced InputSystem.Update or manual controller tick, produced one real move through KeyboardMoveInput: player (2, 0, 2), Moves == 1, HintStatus.None, zero active arrows, and one inactive owned arrow. Its key-release state was queued afterward. This is explicitly simulated movement input, not native keyboard verification or a substitute for the native Hint click.

A subsequent native mouse click on Restart at (1040, 505) restored player (3, 0, 2), Moves == 0, HintStatus.None, and zero total arrow instances, with the countdown starting again near 60 seconds. All scene checks used normal runtime ticks and the original services. The worker result/visual were observed after the actual native clicks.

Frames were confirmed advancing (929 to 947) before the final scene check. The Editor was stopped afterward: isPlaying false, isPaused false, scene isDirty false, runInBackground false, and timeScale 1. runInBackground/timeScale/input settings and service bindings were not changed for the native checks. No saved scene, prefab, project setting, package, or UI framework change was needed for this follow-up.

Local ignored evidence under Sokoban/Logs/hints/: hintGapFocused.json, hintGapEdit.json, hintGapPlay.json, hint-native-before.png, hint-native-arrow.png, hint-queued-movement.png, and hint-native-restart.png. Temporary screenshot assets were removed from Assets after copying the evidence. Changes remain uncommitted; no push.

## Limits

The first solver searches enclosed rectangular application boards. The visited-state cap can exhaust before proving solvability, and this version adds no deadlock pruning, time deadline, web-worker backend, or progress percentage. Task.Run assumes an available .NET worker thread; WebGL/standalone platform behavior has not been built or verified. Cancellation is cooperative: a misbehaving backend may temporarily keep computing on copied data, while its eventual failure remains observed. There is no automatic suggested move, persistence, solver playback, penalty, or timer modification.
