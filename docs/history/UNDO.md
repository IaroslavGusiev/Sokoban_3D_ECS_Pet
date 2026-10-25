# Run-owned ECS undo — 2026-10-25

Started from ee30469 with a clean working tree. Undo is implemented entirely within one Simulation's deterministic Core tick, with a Z shortcut and the existing IMGUI debug overlay. No commits or pushes were made.

## Decisions and ownership

The level-state entity holds MoveHistory, a stack of CommittedMove records containing the committed MovementPlan and previous move count. Records retain only one or two participants and their before/after positions. ApplyMovementSystem records after successful preflight and commit. UndoRequest is a small level-state request component, processed by UndoFeature before the unchanged forward movement pair.

Undo preflights every participant and reverse destination before writing any position, then restores positions through entity.Set, restores the count, and pops exactly one record. Failed preflight retains the entry. Every consumed request marks UndoProcessedTag so MoveCountSystem synchronizes its private snapshot without counting the reversal or a rejected reverse operation. Affected BoxGoalState snapshots are synchronized without publishing goal arrivals; subsequent forward arrivals still publish normally.

Simulation owns and clears history before World disposal, including when pipeline disposal throws. New runs receive new empty history. Run-scoped entity handles stay within that ownership boundary. There is no redo, persistence, persistent entity ID, generic command service, or undo after completion/timeout.

The controller coalesces UI and Z requests and accepts undo only during Playing with ready views, no interpolation, and available Core history. Unavailable requests are discarded. Restart/next-level retain priority. Accepted undo clears the buffered forward command and excludes forward input for that tick. Countdown advancement, final-tick victory order, public numeric validation, and overflow rejection remain intact.

## Checks actually run

| Project suite | Fresh baseline | Final |
| --- | --- | --- |
| EditMode | 213/213 passed | 236/236 passed |
| PlayMode | 144/144 passed | 150/150 passed |

All baseline test names remain and pass; no assertions were removed or weakened. Twenty-one focused Core undo cases and two history-disposal cases were added. Six PlayMode cases were added, with existing nonplaying/loading assertions extended. Dependency tests were not rerun in this task; these counts are project-only.

Coverage includes walk/push undo, repeated/empty requests, invalid/blocked/overflow moves, no simultaneous forward commit, count/snapshot restoration, continuing time, invalid tick preservation, goal-message suppression and later arrivals, preflight faults (moved/disabled/missing participant state and occupied destination), intact identity/view loads, UI/keyboard coalescing, discarded requests during interpolation/pending binding/nonplaying phases, buffered input clearing, restart/next-level history replacement, scene reverse interpolation, and active feedback cleanup.

A live GameplayScene check confirmed advancing frames, all three controls visible at the saved 24-point font, enabled Undo with history, disabled Undo during interpolation, reverse box motion, count 2 -> 1, immediate bump cancellation/restoration and zero remaining owned tweens. The reverse completed with CanUndo enabled again for the previous walk. The scene Z shortcut was exercised by the integration test; the live reverse used the controller's RequestUndo path. Synthetic Input System mouse events did not activate the IMGUI button, so a native mouse click is not claimed. The button's request callback and gating use the same tested controller API.

Local JSON results and inspected screenshots are under ignored Sokoban/Logs/undo. Temporary screenshot assets were removed, Play Mode exited, runInBackground restored, and the saved scene left clean. Core compiled references remain netstandard and DefaultEcs only. No Addressables or standalone player build was run; no dependency, config, prefab, or scene changes required a content rebuild.

## Limits

Undo is current-run-only and unavailable after victory or timeout, while views bind, during interpolation, and outside Playing. Observer exceptions have the same unsupported partial-commit behavior as forward movement; there is no rollback machinery. Damaged/malformed worlds remain outside the stable-identity contract; failed reverse preflight preserves history for retry rather than silently discarding it.
