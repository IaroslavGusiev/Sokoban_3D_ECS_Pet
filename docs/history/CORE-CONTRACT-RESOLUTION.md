# Core contract resolution — 2026-10-25

Started from `94fc593` with a clean index and working tree. The user approved both proposed contract groups after a read-only investigation. This record documents that approval and the implemented result; current authoring instructions remain in [setup](../SETUP.md) and current contracts in [architecture](../ARCHITECTURE.md).

## Approved contracts

- Simulation requires a finite positive time limit and a finite nonnegative delta. Invalid values throw ArgumentOutOfRangeException at the entry point, before World allocation or system execution respectively. Zero delta can still process queued movement. ScriptableObject configs retain their existing trusted-authoring policy without Validate methods.
- Movement planning supports Int32 coordinates and rejects a destination that cannot be represented, for both walks and pushes. One shared calculation uses long intermediates. No raw GridPosition operator, board-bounds rule, entity map, or movement application/preflight algorithm was replaced.
- Every Simulation owns one stable player and one nonvisual level-state entity. Malformed multi-player worlds and dynamic player creation/destruction/retagging are outside the supported contract; no per-tick count checks were added.
- Supported movement executes validation once immediately followed by application once. Plans are transient and consumed by application. Pause/resume applies to the whole pair between ticks. Repeated validation, skipped stages, and mid-stage disabling are unsupported. No abandoned-plan cleanup machinery was added, and useful preflight/atomic-push assertions remain.

## Verification

| Suite | Fresh baseline before implementation | After |
| --- | --- | --- |
| Project EditMode | 194/215 passed; 21 failed | 213/213 passed; 0 failed |
| Project PlayMode | 144/144 passed | 144/144 passed |
| Dependency EditMode | 419/419 passed during Stage 1 | 419/419 passed |

The four approved removals account for four fewer EditMode cases. Two public Simulation boundary cases were added (walk/push): reaching Int32.MaxValue remains valid, a following overflow step moves neither participant, does not add a move, still consumes gameplay time, and does not replay later. All 194 previously passing project EditMode cases remain present and pass. All 17 retained original failing cases now pass. No new failures, Ignore/Explicit markers, weakened expectations, or expected-failure exceptions conceal disagreements.

Numeric tests retain all five invalid-duration and four invalid-delta inputs. They now check the parameter name and verify invalid ticks preserve player/box positions, completion/timeout state, result values, and the pending request; a subsequent zero-delta tick successfully processes that request. Existing stable-player, idle/no-replay, and whole-pair pause/resume assertions were strengthened rather than duplicated.

## Every original failure

| Original failing test | Resolution |
| --- | --- |
| `Sokoban.Tests.App.LevelTimerTests.InvalidDuration_IsRejected(0.0f)` | Retained; passes. Simulation rejects invalid timeLimitSeconds before World allocation. |
| `Sokoban.Tests.App.LevelTimerTests.InvalidDuration_IsRejected(-1.0f)` | Retained; passes. Simulation rejects invalid timeLimitSeconds before World allocation. |
| `Sokoban.Tests.App.LevelTimerTests.InvalidDuration_IsRejected(float.NaN)` | Retained; passes. Simulation rejects invalid timeLimitSeconds before World allocation. |
| `Sokoban.Tests.App.LevelTimerTests.InvalidDuration_IsRejected(float.PositiveInfinity)` | Retained; passes. Simulation rejects invalid timeLimitSeconds before World allocation. |
| `Sokoban.Tests.App.LevelTimerTests.InvalidDuration_IsRejected(float.NegativeInfinity)` | Retained; passes. Simulation rejects invalid timeLimitSeconds before World allocation. |
| `Sokoban.Tests.App.LevelTimerTests.InvalidTick_DoesNotChangeTimer(-1.0f)` | Retained and strengthened; passes. Simulation rejects invalid deltaTime before mutation; the pending move remains usable. |
| `Sokoban.Tests.App.LevelTimerTests.InvalidTick_DoesNotChangeTimer(float.NaN)` | Retained and strengthened; passes. Simulation rejects invalid deltaTime before mutation; the pending move remains usable. |
| `Sokoban.Tests.App.LevelTimerTests.InvalidTick_DoesNotChangeTimer(float.PositiveInfinity)` | Retained and strengthened; passes. Simulation rejects invalid deltaTime before mutation; the pending move remains usable. |
| `Sokoban.Tests.App.LevelTimerTests.InvalidTick_DoesNotChangeTimer(float.NegativeInfinity)` | Retained and strengthened; passes. Simulation rejects invalid deltaTime before mutation; the pending move remains usable. |
| `Sokoban.Tests.EditMode.MovementTests.BoxDestinationOutsideIntegerRange_PushIsRejectedWithoutPositionChanges(Up,0,2147483646,0,int.MaxValue)` | Retained; passes. Movement planning rejects an unrepresentable destination without partial movement. |
| `Sokoban.Tests.EditMode.MovementTests.BoxDestinationOutsideIntegerRange_PushIsRejectedWithoutPositionChanges(Down,0,-2147483647,0,int.MinValue)` | Retained; passes. Movement planning rejects an unrepresentable destination without partial movement. |
| `Sokoban.Tests.EditMode.MovementTests.BoxDestinationOutsideIntegerRange_PushIsRejectedWithoutPositionChanges(Left,-2147483647,0,int.MinValue,0)` | Retained; passes. Movement planning rejects an unrepresentable destination without partial movement. |
| `Sokoban.Tests.EditMode.MovementTests.BoxDestinationOutsideIntegerRange_PushIsRejectedWithoutPositionChanges(Right,2147483646,0,int.MaxValue,0)` | Retained; passes. Movement planning rejects an unrepresentable destination without partial movement. |
| `Sokoban.Tests.EditMode.MovementTests.MultiplePlayers_MoveIsRejectedWithoutPositionChanges` | Removed with explicit approval: malformed multi-player worlds are unsupported. Parser rejection and stable-player Simulation coverage remain. |
| `Sokoban.Tests.EditMode.MovementTests.PlayerDestinationOutsideIntegerRange_MoveIsRejectedWithoutPositionChanges(Up,0,int.MaxValue)` | Retained; passes. Movement planning rejects an unrepresentable destination without partial movement. |
| `Sokoban.Tests.EditMode.MovementTests.PlayerDestinationOutsideIntegerRange_MoveIsRejectedWithoutPositionChanges(Down,0,int.MinValue)` | Retained; passes. Movement planning rejects an unrepresentable destination without partial movement. |
| `Sokoban.Tests.EditMode.MovementTests.PlayerDestinationOutsideIntegerRange_MoveIsRejectedWithoutPositionChanges(Left,int.MinValue,0)` | Retained; passes. Movement planning rejects an unrepresentable destination without partial movement. |
| `Sokoban.Tests.EditMode.MovementTests.PlayerDestinationOutsideIntegerRange_MoveIsRejectedWithoutPositionChanges(Right,int.MaxValue,0)` | Retained; passes. Movement planning rejects an unrepresentable destination without partial movement. |
| `Sokoban.Tests.EditMode.MovementTickTests.MultiplePlayers_ConsumeAllRequestsWithoutMoving` | Removed with explicit approval: malformed multi-player worlds are unsupported. Parser rejection and stable-player Simulation coverage remain. |
| `Sokoban.Tests.EditMode.MovementValidationTests.AbandonedPlanOnDisabledOwner_IsAlsoRemoved` | Removed with explicit approval: repeated/partial validation scheduling is unsupported. Existing ordered-stage, idle/no-replay, whole-pair pause/resume, and application preflight coverage remain. |
| `Sokoban.Tests.EditMode.MovementValidationTests.MissingRequestAndAbandonedPlan_DoNotMoveInALaterValidationPass` | Removed with explicit approval: repeated/partial validation scheduling is unsupported. Existing ordered-stage, idle/no-replay, whole-pair pause/resume, and application preflight coverage remain. |

## Scope and remaining limits

Application preflight, level-state ownership, timer/win/count system order, presentation, config assets, and dependencies remain unchanged. Components remain data-focused; compiled Core references are only netstandard and DefaultEcs. No new Addressables content or standalone player build was needed or run.

Low-level malformed worlds and interrupted partial ticks intentionally have no recovery guarantee. The raw GridPosition addition operator is not an independently checked public numeric API; overflow rejection is a movement-planning guarantee.

Baseline, final results, and the name-by-name comparison are saved locally under ignored `Sokoban/Logs/core-contract-resolution/`. Those artifacts may be absent from a fresh clone. All task changes were left uncommitted; nothing was pushed.
