# View failure recovery and Tick cleanup

Implemented and verified on 2026-10-23, including the approved live-skin failure policy. The user requested that this task remain uncommitted. Existing unrelated working-tree changes were preserved.

## Approved live-skin failure policy

Pending rebinds keep Playing. After ticking a Playing run, a Failed view status transitions to ViewsFailed and takes priority over completion. Movement input is withheld on subsequent ticks while the layers continue ticking and K/R/N remain available.

K can request another skin in ViewsFailed. TickBinding then selects BindingViews while the replacement is pending or Playing when Ready, without requiring R. A win detected in the same tick as the failure remains latched in Core; it is processed after view recovery instead of advancing past the failure.

## Findings before changes

### Step 1: repeated logs were real

The installed versions are Unity 6000.0.63f1 and Addressables 4.1.1. AddressablesImpl.LoadAssetAsync returns a completed failed operation for an invalid key with no location. The prior ViewAssets implementation immediately evicted every cached Failed handle on the next caller, even in the same frame. Each replacement CachedLoad had its own failure-log flag, so one log per handle could still mean one log per entity.

A new regression first ran against the unmodified provider. After warming Addressables, repeated invalid Wall-key requests in one frame produced another InvalidKeyException log; the test failed on that extra log. The failure result is saved. After the fix, four requests in that same frame receive the same exception object and failed handle, with one expected error. Correcting the reference and advancing a frame then succeeds, with the old failed handle invalidated by release.

CachedLoad now records FailureFrame = Time.frameCount when OnCompleted observes Failed. The existing immediate-completion path invokes OnCompleted too. Eviction requires Failed status and FailureFrame < Time.frameCount. Same-frame failures remain shared. Eviction removes the entry and releases its valid handle once; owner disposal cannot release the removed entry again. Resolved-key caching, caller-only cancellation, and one error log per failed handle remain.

### Step 2: provider failures already had stale protection

Before this task, the successful-await path checked cancellation and the current/requested ViewKey. The provider-exception catch separately performed those same checks before calling a static Fail(entity). Thus the held stale-provider-failure race was already protected. The static helper itself always set ViewFailed and removed ViewPending. The instantiation/adapter-exception catch checked cancellation and logged the exception, but did not compare keys.

Both failure paths now call Fail(entity, requestedKey). This helper returns on cancellation before touching ECS, clears only ViewPending for a stale key, and otherwise sets ViewFailed and removes ViewPending. The active-run instantiation-exception log remains. The successful-await cancellation/stale checks remain in place.

The existing stale held-failure test now additionally proves exactly one Alternate view, Ready status, and no unexpected logs. A new held-failure case without a skin change proves ViewFailed, no ViewPending, and no retry.

## Controller and skin cycling

State.Failed was split into ViewsFailed and LevelFailed. TickBinding handles both BindingViews and ViewsFailed, ticking the run before mapping Ready to Playing, Failed to ViewsFailed, and Binding to BindingViews. A skin request can therefore clear a startup or live view failure and recover without R. TickPlaying checks Failed view status after the run tick and before win detection; Binding status does not pause it. LevelFailed represents a definition-load/build failure and does not use view readiness for recovery.

R in LevelFailed reloads the attempted index; R in every other non-Loading state rebuilds the current definition. N still takes command priority. A retained old run in LevelFailed ticks without movement input; it is not considered a successfully loaded replacement.

Tick now returns early for Loading, calls HandleSkinInput, dispatches TickState, and then calls HandleCommands when allowed. Private handlers are TickBinding, TickPlaying, TickCompleted, and TickLevelFailed. TickState returns false when TickCompleted initiates the automatic next load. This preserves the previous end-of-tick behavior even when a fake source completes synchronously and immediately changes Loading back to BindingViews. A regression with both N and R pressed confirms that only the automatic next load starts.

SkinCycle.Next caches Enum.GetValues(typeof(SkinId)) once and indexes the next value modulo its length. It assumes the specified contiguous values 0..n-1. No skin was added. Adding a third enum value needs no controller modification. The cycle test is in the existing Sokoban.PlayModeTests assembly, which already references Presentation; it checks every starting skin, no immediate repeats when N > 1, all values visited, and return to the start after exactly N calls.

## Current controller transitions

- Initialize, N, or completion timeout -> Loading.
- Loading -> BindingViews after successful definition load and run construction; a load/build exception -> LevelFailed.
- BindingViews or ViewsFailed -> Playing on Ready, ViewsFailed on Failed, BindingViews while pending.
- Playing -> ViewsFailed when views fail after the run tick; pending rebinds retain Playing.
- Playing -> Completed after win detection only when view status is not Failed. Failure wins if both occur in one tick.
- Completed -> Loading after 0.75 seconds, with temporary level-index wraparound.
- R in LevelFailed -> Loading for the attempted index.
- R in BindingViews, ViewsFailed, Playing, or Completed -> a new run in BindingViews from the current definition.
- K in any non-Loading state with a run -> the next desired skin request. During ViewsFailed, successful binding returns to Playing through TickBinding.
- Loading ignores controls. LevelFailed cannot recover merely because the old run's views become Ready.

## Files

Added:

- Sokoban/Assets/_Sokoban/Code/Unity/SkinCycle.cs, plus its Unity-generated metadata.
- docs/VIEW-FAILURE-RECOVERY.md.

Changed:

- Sokoban/Assets/_Sokoban/Code/Unity/ViewAssets.cs: failure-frame sharing and later-frame eviction.
- Sokoban/Assets/_Sokoban/Code/Unity/Ecs/Systems/ViewBindingSystem.cs: shared token/key-aware Fail helper.
- Sokoban/Assets/_Sokoban/Code/Unity/GameplayController.cs: failure states, recovery, Tick handlers, and SkinCycle use.
- Sokoban/Assets/_Sokoban/Tests/PlayMode/ViewBindingTests.cs: real synchronous-failure sharing/retry regression.
- Sokoban/Assets/_Sokoban/Tests/PlayMode/ViewLayerFlowTests.cs: stronger stale-failure assertions and held current-key failure coverage.
- Sokoban/Assets/_Sokoban/Tests/PlayMode/StartupGateTests.cs: state/restart expectations, persisted-skin recovery test, enum cycle test, synchronous completion-load ordering test, live failure/recovery coverage replacing the old startup-only assertion, and a failure-before-win regression.
- Sokoban/Assets/_Sokoban/Tests/PlayMode/PresentationPipelineTests.cs: LevelFailed retry assertions and portable synthetic-keyboard setup/restore for headless execution.
- docs/ARCHITECTURE.md: implemented behavior and the approved live-skin failure policy.

No files were deleted. No Core file, assembly definition, package, scene, prefab, authoring asset, or project setting was changed. No .meta file was hand-edited. Existing feature-list markers were untouched, and no code comments were added.

## Guard review

Removed or replaced:

| Location | Prior check/branch | Change and reason |
| --- | --- | --- |
| ViewBindingSystem provider catch | Inline token and stale-key checks | Moved into Fail(entity, key), shared by both failure paths. Their protections remain. |
| GameplayController.Tick | Inline state-specific branches and broad optional run tick | Moved into named handlers. Binding/Playing/Completed require an owned run and tick it directly; only LevelFailed may tick an absent old run through ?. |
| GameplayController.Restart | Single Failed branch | Replaced with LevelFailed reload. ViewsFailed now rebuilds the existing definition as requested. |
| GameplayController skin input | Hard-coded Default/Alternate conditional | Replaced with enum-driven SkinCycle.Next. No defensive fallback for unknown values was added. |

Kept or introduced:

| Location | Check | Reason |
| --- | --- | --- |
| ViewAssets.LoadAsync | Initial ct.ThrowIfCancellationRequested | Preserves caller-only cancellation and avoids beginning work for an already cancelled caller. |
| ViewAssets.LoadAsync | Cache hit plus Failed status plus earlier FailureFrame | Required retry rule: share failures within a frame and retry only later. |
| ViewAssets eviction/disposal | Handle.IsValid | Previously approved Editor-exit cleanup and explicitly preserved release semantics; Addressables can invalidate outstanding handles before owner teardown. |
| ViewAssets.LoadAsync | `load == null` cache-miss branch | Starts one handle only when no shared entry exists. |
| ViewAssets.LoadAsync | Handle.IsDone | Preserves immediate versus pending completion handling; both record failed completion frames. |
| ViewAssets async catches | Cancellation rethrow; absent/present CachedLoad error path | Observed cancellation/failure and one log per failed handle, including synchronous setup failure. |
| CachedLoad.OnCompleted | Failed status | Only real operation failures record FailureFrame and log an error. |
| CachedLoad.LogFailure | _failureLogged | Required existing error deduplication across callback/callers; not a disposal or initialization flag. |
| ViewBindingSystem successful await | Run-token check before ECS access | Late results from a cancelled run cannot access obsolete/disposed entities. |
| ViewBindingSystem successful await | Current/requested key comparison | Required stale-success rule. |
| ViewBindingSystem.Fail | Run-token check before key mapping | Required cancellation/failure cleanup for both failure paths. |
| ViewBindingSystem.Fail | Current/requested key comparison | Required stale-failure rule; stale failures clear pending without poisoning the new key. |
| ViewBindingSystem | Optional EntityView TryGetComponent | Existing support for prefabs without adapters. |
| ViewBindingSystem instantiation catch | Unity view null comparison | Destroy only a Unity object that was actually acquired and still exists. |
| ViewBindingSystem instantiation catch | Active run-token check around log/Fail | Preserves existing quiet cancellation behavior and the requested instantiation-exception log. The helper's own token check protects its independent contract. |
| GameplayController.Tick | Loading early return | Existing state policy blocks input, ticks, and commands while loading. |
| GameplayController.Tick | TickState command-result and non-Loading condition | Prevent a second control after an automatic load, including synchronous completion. |
| HandleSkinInput | Nonnull plain-C# run and skin-key press | Requested state/command availability; level construction failure may have no run. |
| TickState | State dispatch | Requested separation of supported lifecycle phases. |
| TickBinding | Ready/Failed/Binding status mapping | Required recovery rules for BindingViews and ViewsFailed only. |
| TickPlaying | Failed view status before IsCompleted | Approved state policy: pause on failure while pending rebinds keep Playing; view failure takes priority over completion. |
| TickPlaying | IsCompleted | Existing domain transition to the completion pause, after the failure check. |
| TickCompleted | Timer threshold | Existing automatic progression rule. |
| TickLevelFailed | `_run?.Tick` | LevelFailed can retain an old run or have none; LevelRun is plain C#. |
| HandleCommands | N before else-if R | Existing single-command priority. |
| Restart | LevelFailed branch | Required reload-vs-rebuild policy. |
| LoadLevelAsync | Post-await cancellation check and cancellation/failure catches | Existing protection against late definitions and one observed error per failed load. |
| StartRun | Construction exception catch | Existing recoverable construction failure with one log and no published partial run. |
| ReleaseRun | Optional plain-C# run disposal and finally clearing the field | Existing ownership/cleanup when no run was published or disposal throws. |
| SkinCycle | No new guards | The specified contiguous valid-enum assumption is explicit; no argument validation, fallback, or lifecycle flags were added. |

Test assertions, fake-provider mode branches, scenario loops, and teardown checks for Unity objects already destroyed by a test remain. The synthetic-keyboard fixture now saves/restores InputSystem.runInBackground, explicitly enables it and its keyboard, and retains the existing settings restore. This changes test environment setup only. No production input guards changed.

No _disposed, ThrowIfDisposed, initialized flags, entity.IsAlive checks, local system IsEnabled guards, or ?. / ?? on UnityEngine.Object were introduced. Core public-boundary/domain guards were untouched because Core was untouched.

## Verification and limits

- Installed project versions checked: Unity 6000.0.63f1, DefaultEcs 0.17.2, Addressables 4.1.1, UniTask 2.5.11, Zenject 9.2.0, Unity Test Framework 1.6.0. Unity CLI 1.0.0-beta.14 was confirmed current.
- The Editor was initially closed. The installed project Editor was launched headlessly, without upgrading the project, for this task's checks.
- The task-owned headless Editor was closed after verification. Its Pipeline quit command hit an Editor-only scheduling error; that temporary scheduler object was removed and the Editor was closed through EditorApplication.Exit instead. This happened after the test runs and required no project-code change.
- Step 1: compilation passed; all 11 binding/provider tests passed after reproducing the regression on unmodified production code.
- Step 2: compilation passed; all 12 view-flow tests passed.
- Step 3: compilation passed; all 17 startup/controller tests passed. Existing fakes supported the requested persisted-Alternate startup failure/recovery scenario in fewer than 60 lines of new test code. The case also observes intermediate BindingViews while Default is held.
- Step 4: compilation passed; all 19 controller/cycle/ordering tests passed.
- Final full project checks: 167 EditMode cases and 72 PlayMode cases passed. The PlayMode total includes the five new cases from this task.
- After approval of the live failure policy, compilation and all 73 project PlayMode cases passed. The replaced startup-only test now proves pending Playing, failure, movement pause, K recovery through BindingViews, and resumed movement in the same simulation without R or another definition load. One additional case proves failure priority over a simultaneous win and completion after recovery. All 58 Core source/metadata hashes still match; the unchanged Core EditMode suite was not rerun for this follow-up.
- An initial headless full run failed two pre-existing synthetic-keyboard cases. Installed Input System source confirmed focus/background event filtering; explicit InputSystem background processing and device enablement in the fixture made both pass. No production input code was changed to accommodate tests.
- All 58 Core source/metadata SHA-256 hashes match task start. Compiled Core references remain netstandard and DefaultEcs only.
- Focused source whitespace checks passed. No standalone player build or new manual scene smoke check was performed for this task. The failure scenarios are exercised by real Addressables/fake-provider PlayMode tests and existing saved-scene integration cases.

Results are saved under ignored Sokoban/Logs/view-recovery-task/: before-step1.json, step1.json, step2.json, step3.json, step4.json, editmode.json, playmode.json, and core-baseline.json. The initial regression failure is intentional evidence; final suites pass. The approved follow-up's results and fresh Core hash snapshot are in Sokoban/Logs/live-view-failure-policy/.

## Manual Editor checklist

1. No authoring migration or reassignment is required. Open GameplayScene using Unity 6000.0.63f1. Use a valid assigned reference whose Addressables lookup is deliberately broken, such as a prefab excluded from the built content, for the following runtime checks. Clearing a catalog field or using a syntactically invalid reference is instead rejected by the existing authoring validator before gameplay; that is a different failure path. Restore/rebuild content afterward, particularly with Use Existing Build.
2. Temporarily break the Wall prefab's runtime lookup and enter Play Mode. Expect one error for the Wall resolved key, all affected walls marked ViewFailed, and controller ViewsFailed. Let several frames pass: there should be no automatic retry or further spam. Restore the lookup, then R or re-enter Play Mode to retry.
3. With Alternate selected and persisted through R, make only Alternate player's runtime lookup fail. Startup should enter ViewsFailed. Press K to Default: expect BindingViews while pending, then a visible player and Playing without R. This path is implemented and tested.
4. While already Playing, select a broken Alternate through K. Expect one error and an invisible player with ViewFailed; the next Playing tick enters ViewsFailed and subsequent movement input is paused. K back to Default should clear failure, use BindingViews if loading is pending, restore the player, and return to Playing without R. A pending rebind before failure must continue Playing. If the final goal is satisfied in the failure tick, the game must wait for view recovery before entering Completed.
5. Break a level's runtime lookup, then press N. Expect one error, LevelFailed, and the old level remaining on screen with movement input blocked. Press R: expect a retry of the same attempted index and exactly one additional error. Restore the reference and R to recover.
6. Run the project EditMode assemblies and Sokoban.PlayModeTests in Window > General > Test Runner: expect 167 and 73 cases. Fault-injection errors are asserted by their tests.
