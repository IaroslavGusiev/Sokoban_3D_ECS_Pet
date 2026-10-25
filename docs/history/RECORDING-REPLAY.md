# Deterministic Core recording and replay — 2026-10-25

Started from f0f4dc8. There were no staged or untracked changes; the existing unstaged ProjectSettings runInBackground change (1 -> 0) was preserved. All milestone changes remain uncommitted, with no push.

## Implemented boundary

The normal public Simulation constructor starts recording immediately. The ordered stream stores every Send and RequestUndo attempt, including invalid commands, rejected/terminal requests, and coalesced duplicates. It stores each successful Tick afterward, including idle, zero, and terminal ticks, with the original float bits. Numeric validation remains unchanged; invalid ticks leave state/pending work untouched and add no input. Arbitrary external World mutation, custom feature schedules, subscriber reentry, and observer/partial-tick failures remain outside reproducible input execution.

ExportRecording copies the initial level, initial time limit, and the current input list into an immutable SimulationRecording. Its inputs contain only a kind, command value, and float delta; the export has no entity handles, Unity objects, Addressables handles, or async references. Previous exports cannot change when a source continues or disposes. Export before disposal; the source buffer is cleared at disposal and export is unavailable afterward.

Endpoints may include pending movement and undo, including movement followed by undo before a tick. Replay never adds a flush tick. SimulationReplay owns a fresh recording-disabled Simulation and uses real APIs and rules to reconstruct movement and undo history. Step executes one input; Run executes the remainder. A using scope releases the replay; a failed Step disposes it immediately before rethrowing, and repeated owner cleanup is safe. The exposed Simulation is borrowed only while its replay owner is alive. Replay cannot export a recording of itself.

CaptureSnapshot copies player position, boxes sorted by X then Y, move count, remaining/elapsed time, completion, timeout, and undo availability into an immutable value-comparable GameplaySnapshot. It excludes entity identities, query order, presentation/skin state, and private pending/history components. Pending work and history depth are additionally verified by continuing source and replay through subsequent ticks/undo.

## Checks actually run

| Project suite | Fresh baseline | Final |
| --- | --- | --- |
| EditMode | 236/236 passed | 257/257 passed |
| PlayMode | 149/150 passed | 150/150 passed |

The baseline PlayMode failure was AddressablesServiceTests.AddressAndLabelLoads_UseBuiltContentAndRetainIndependentOwnership. Its assertion was interrupted by an unrelated Editor Package Manager log: access-token HTTP 400. No log suppression or assertion changes were made; that test passed in the final full suite and the Editor log did not recur. No previously passing test regressed or disappeared. Dependency tests were not rerun; the counts above are project-only.

Twenty-one new recording/replay cases cover exact deltas (including signed zero), walk/push/block/idle behavior, completion/timeout/final-tick victory, undo with new branches/repetition, rejected/coalesced request ordering, pending endpoints, four invalid-delta cases, immutable copies after continuing/disposal, independent replays, rejected-move history depth, sorted immutable snapshots, empty recordings with no implicit tick, and fault-injected cleanup during both request and tick execution. The three shipped-level solver tests retain every existing assertion and now compare snapshots after each recorded tick in a fresh replay. Existing invalid-duration, undo, ownership, movement, and presentation tests remain present and pass.

Pure Core compiled references remain netstandard and DefaultEcs. Export/snapshot/input types have no DefaultEcs entity fields or Unity/async dependencies. Presentation, ECS rules/order, Addressables, PrimeTween, assets, dependencies, and the existing ProjectSettings edit were not changed. No scene edits, manual playback UI, Addressables content build, or standalone player build was needed or performed. Local JSON reports and name-by-name comparison are under ignored Sokoban/Logs/recording-replay.

## Limits

The guarantee is identical Core execution for identical initial data, ordered API inputs, float deltas, rule version, and runtime. Cross-platform floating-point equivalence is not promised. There is no persistence/JSON, transport, seeking, rewind, playback-speed control, UI integration, fixed timestep, or fixed-point redesign. Snapshot equality alone does not assert equality of private pending requests/history; continuation tests verify those behaviors. Normal observer restrictions still apply, with no rollback for arbitrary observer exceptions.
