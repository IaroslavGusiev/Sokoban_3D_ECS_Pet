# Async view tests and runtime skins

**Historical record.** Behavior, setup steps, pass counts, and pending proposals below belong to the recorded milestone. Use [current architecture](../ARCHITECTURE.md) and [current setup](../SETUP.md) for the implemented project.

Implemented and verified on 2026-10-23. The user clarified that this task should remain uncommitted. Existing uncommitted work was preserved; the manifest below lists this task's changes rather than the full working tree.

## Implemented behavior

Core is unchanged. SkinId, ViewKey, ViewSkin, and SkinChangeRequest belong to Sokoban.Presentation. ViewKey combines Kind and Skin with value equality, hashing, operators, and readable names. Missing ViewSkin means Default. ViewKeyMap retains the previous Wall, Box, Player, Goal, Floor priority.

ViewCatalog requires a Default entry for every kind, unique Kind/Skin pairs, and valid references for every entry. Resolve uses an exact entry if present and otherwise the Default entry for that kind. Existing serialized entries have Skin=Default automatically. A broken explicit Alternate reference fails; it does not silently fall back.

ViewAssets resolves before accessing its Dictionary<ViewKey, CachedLoad>. The resolved entry key owns the cache entry, so an absent Alternate and a Default request share one handle. Existing per-caller AttachExternalCancellation, shared handle lifetime, failure-log deduplication, failed-entry eviction, and owner release behavior remain.

The ordered presentation pipeline is SkinSwitchFeature, ViewBindingFeature, TransformSyncFeature. SkinSwitchSystem is buffered and consumes SkinChangeRequest. For a bound view it calls EntityView.Uninstall if available, destroys the GameObject, then removes TransformComponent. It writes ViewSkin and removes ViewFailed and the request. It leaves ViewPending in place while a request is in flight, preventing a second concurrent bind for that entity.

Binding captures the requested ViewKey. After awaiting, it checks the run token before accessing ECS and then compares the current desired key. A stale result removes ViewPending and returns without instantiating; the next tick requests the latest key. Failed obsolete requests follow the same rule, so they cannot mark the latest skin as failed. Provider failures still log once for their load attempt. No per-entity dictionary, generation counter, or retained task list was added.

EntityAdapter now declares Uninstall. ComponentAdapter<T> implements it with entity.Remove<T>(); EntityView forwards it to all adapters. SkinSwitchSystem is the only production caller of EntityView.Uninstall. There are no OnDisable/OnDestroy uninstall callbacks. ComponentAdapter<T> was the only direct adapter subclass needing an implementation. TransformAdapter inherits it without a source change; the new TestSkinAdapter also inherits it. Adapters remain presentation-owned and must not remove or mutate Core gameplay components.

ViewLayer owns and disposes a player EntitySet. Its constructor writes initial ViewSkin directly before binding; SetPlayerSkin writes a one-shot request to every player. LevelRun receives initial SkinId and forwards later changes. GameplayController owns the chosen _playerSkin across R and level changes. K cycles Default/Alternate in non-Loading states with a run, before layer ticking. The startup gate still evaluates readiness only in BindingViews: a skin load or failure during Playing does not pause gameplay. Existing Failed startup recovery still follows R/N; this task adds no new state transitions for startup failures.

The required-keyboard contract from the previous task remains. K directly reads Keyboard.current.kKey; no missing-keyboard fallback was reintroduced. N remains Editor/development-only. Default view names remain Kind (x, y); Alternate names use Kind/Alternate (x, y), including when the prefab fell back to Default.

## Files

Added production files under Sokoban/Assets/_Sokoban/Code/Unity:

- SkinId.cs
- ViewKey.cs
- SkinSwitchFeature.cs
- Ecs/Components/ViewSkin.cs
- Ecs/Components/SkinChangeRequest.cs
- Ecs/Systems/SkinSwitchSystem.cs

Renamed ViewKindMap.cs to ViewKeyMap.cs through Unity AssetDatabase. Its GUID was preserved: 0a10a7ed689045f4e8eb7d5f5060d764. The old filename is removed by this rename.

Changed production files under the same root:

- IViewProvider.cs: ViewKey request signature.
- ViewAssets.cs: resolved-key cache; existing load ownership and logging retained.
- ViewCatalog.cs: Skin field, exact/fallback resolution, pair validation.
- Ecs/Systems/ViewBindingSystem.cs: captured keys, stale success/failure handling, key-aware names, redundant failure-helper guard removed.
- Ecs/Model/EntityAdapter.cs: abstract Uninstall method.
- Ecs/Model/ComponentAdapter.cs: generic component removal.
- Ecs/Model/EntityView.cs: adapter uninstall forwarding.
- ViewFeatures.cs: skin feature first and renumbered markers.
- ViewLayer.cs: player query, initial desired skin, request API, cleanup.
- LevelRun.cs: initial skin parameter and forwarding API.
- GameplayController.cs: persistent chosen skin and K handling.
- IPlayerInput.cs: NextSkinPressed.
- KeyboardMoveInput.cs: K mapping.

Tests under Sokoban/Assets/_Sokoban/Tests/PlayMode:

- Added ViewLayerFlowTests.cs with six requested flow tests, plus bound switch, pending stale success, pending stale failure, failed-skin recovery, and simulation-state independence.
- Added TestSkinAdapter.cs with a serializable test component and a generic adapter subclass.
- Changed ViewBindingTests.cs for ViewKey migration, mapping/equality assertions, exact/fallback catalog validation, and resolved Default handle sharing.
- Changed PresentationPipelineTests.cs for the provider/input signatures and K keyboard assertion.
- Changed StartupGateTests.cs for provider/input signatures and a controller case proving that an in-flight switch stays Playing and survives R/N.

The existing Sokoban.PlayModeTests asmdef was reused unchanged. It already references Sokoban.Unity, Sokoban.Core, UniTask, Unity input/Addressables dependencies, NUnit, and Unity's TestAssemblies support. No new test assembly or assembly references were needed. The new catalog tests use ScriptableObjects and Addressables assets, so they live in that Unity test assembly rather than changing a pure Core EditMode assembly.

Documentation: changed ARCHITECTURE.md and added this report. No Core files, packages, asmdefs, scenes, prefabs, authoring assets, or project settings were changed by this task. Unity generated metadata for new scripts and moved metadata with the renamed script. No .meta or serialized asset file was hand-edited. No other files were deleted.

## Guard review

Removed:

| Location | Check | Reason |
| --- | --- | --- |
| ViewBindingSystem.Fail | Repeated run-token check | Both call sites now check cancellation in their async failure continuation before calling this synchronous helper. There is no intervening await. Removing the duplicate retains the required checks before ECS access. |

Kept or introduced:

| Location | Check | Reason |
| --- | --- | --- |
| GameplayController.Tick | Early Loading return | Existing state policy; K/R/N/input/ticking remain blocked during loading. |
| GameplayController.Tick | `_run != null && NextSkinPressed` | Requested availability/command rule in Tick; failed construction can leave no run. |
| GameplayController.Tick | Default/Alternate choice | Requested cycling and wrapping over the two SkinId values. |
| GameplayController.Tick | Playing input check; BindingViews status switch; IsCompleted check; completion timer threshold; N/else-if-R | Existing state machine and command policies remain unchanged. |
| GameplayController.Tick/ReleaseRun | `_run?.Tick` and `_run?.Dispose` | Existing optional plain-C# run; no UnityEngine.Object operators are involved. |
| GameplayController.Restart | Failed branch | Existing required reload-vs-rebuild behavior. |
| GameplayController.LoadLevelAsync | Post-await token check and cancellation/failure catches | Existing protection against late completion and observed failure, with one log. |
| GameplayController.StartRun | Construction catch | Existing failed-construction recovery and single error log. |
| LevelRun construction/disposal | Partial-resource ?. cleanup and try/finally | Existing ownership contract: release acquired resources and never skip simulation disposal after view disposal failure. |
| ViewLayer.Status | Failed before missing-view checks | Existing readiness semantics and failure priority. |
| ViewLayer constructor/disposal | Acquisition catch, query ?. cleanup, cancellation-first teardown, finally | Existing ownership plus the new owned player query; constructors can fail before queries are assigned. EntitySet is plain C#. |
| ViewLayer.DestroyRoot | Unity root null comparison | Unity can destroy the root independently; also covers incomplete construction. |
| ViewKeyMap | Tag-priority cases and absent-ViewSkin fallback | Required kind selection and default skin rule. Missing all view tags still fails loudly. |
| ViewKey.Equals/ToString | Object type match; Default formatting branch | Value equality and readable stable naming, not lifecycle defenses. |
| ViewCatalog.Resolve | Exact pair match and Default selection | Required resolution rule. Missing/duplicate fallback entries fail through Single. Validation rejects duplicate exact entries before gameplay composition. |
| ViewCatalog.Validate | Missing Default, duplicate pair, null/invalid reference, nonempty problems checks | Validation of external authoring data, preserving actionable diagnostics. |
| ViewAssets.LoadAsync | Initial ct.ThrowIfCancellationRequested | Existing caller-cancellation behavior; no shared cache load starts for an already cancelled caller. |
| ViewAssets.LoadAsync | Cache lookup with Failed status | Existing eviction/retry rule, now applied to the resolved key. |
| ViewAssets.LoadAsync | `load == null` cache miss | Existing cache state machine; a new handle is acquired only on a miss. |
| ViewAssets.LoadAsync | Handle.IsDone branch | Existing completion logging for already-finished versus pending operations. |
| ViewAssets async catches | Cancellation rethrow; absent/present CachedLoad log path | Existing observed cancellation and one failure log per attempt, including synchronous setup failures. |
| ViewAssets release boundaries | Handle.IsValid checks | Previously approved Editor-exit fix and explicitly preserved handle behavior. Addressables may invalidate handles before owner teardown. |
| CachedLoad.OnCompleted | Failed status check | Log real operation failures only. |
| CachedLoad.LogFailure | _failureLogged | Existing required per-load log deduplication across completion and callers; this is not a disposal/initialization flag. |
| ViewBindingSystem async continuations | Token checks before ECS access, including failure paths | Cancelled runs never access obsolete/disposed worlds. |
| ViewBindingSystem async continuations | Requested/current ViewKey comparisons | Required stale-success protection, also applied to stale failures. No old skin is instantiated or marked failed for a new key. |
| ViewBindingSystem | Optional EntityView TryGetComponent | Prefabs may omit adapters by design. |
| ViewBindingSystem instantiation catch | Unity view null check and active-token check before log/failure | Release any acquired Unity view and skip ECS writes for a cancelled run. |
| SkinSwitchSystem | Has<TransformComponent> | Domain branch between bound views and in-flight/failed requests. |
| SkinSwitchSystem | Unity Transform null comparison | Unity can destroy it independently; missing/destroyed GameObjects have no adapter to uninstall. |
| SkinSwitchSystem | Optional EntityView TryGetComponent | Only adapter-bearing views need Uninstall. |
| EntityView.Install | Null adapter check | Existing external authoring validation and Unity-object null semantics. Uninstall relies on the same validated adapter list. |
| KeyboardMoveInput.NextLevelPressed | UNITY_EDITOR/DEVELOPMENT_BUILD branch | Existing shortcut availability requirement. K/R/movement retain direct required-keyboard access. |

SkinSwitchSystem removes TransformComponent and ViewFailed directly; no redundant component-existence check was added around Remove. Tests retain their scenario branches, fake-provider mode selection, and cleanup checks for Unity objects already destroyed by a scenario. No _disposed, ThrowIfDisposed, initialized flags, entity.IsAlive, local IsEnabled guards, or lifecycle callbacks were added. No ?. or ?? is used on UnityEngine.Object. No code comments were added except the requested feature markers.

## Verification

- Installed versions checked: Unity 6000.0.63f1, DefaultEcs 0.17.2, Zenject 9.2.0, UniTask 2.5.11, Addressables 4.1.1. CLI 1.0.0-beta.14 was confirmed current.
- Step 1 compiled and passed all six new flow cases before any production edits. Immediate binding, partial pending completion, repeated ticks, provider failure/no retry, cancellation with late completions, and one instantiation error are covered. FakeViewProvider uses per-request UniTaskCompletionSource and AttachExternalCancellation, counts calls, and supports immediate/held/failure modes.
- Step 2 compiled and passed all 67 project PlayMode cases and 167 project EditMode cases.
- Step 3 compiled and passed all 67 PlayMode cases again after removing the duplicate failure-helper guard. Existing cancellation-after-world-disposal coverage remains passing.
- Skin tests verify old GameObject destruction after a frame, one replacement view, removal of the old adapter component, no stale prefab instantiation, stale failure discard, failure clearing, unchanged simulation positions/completion, exact/fallback catalog resolution, and one shared Default handle with settled reference count one.
- Runtime startup reached Playing with 76 bound views, and frame advancement was confirmed. Injected keyboard K switched the desired skin to Alternate while staying Playing, with the same Simulation and player grid position. With no Alternate authoring entry, the cache retained five Default handles and no Alternate handle. R retained Alternate. Loading level 2 retained Alternate and reached Playing with 78 bound views.
- A temporary cloned catalog supplied an invalid Alternate reference. K produced exactly one captured InvalidKeyException, no bound player transform, and ViewFailed while the controller stayed Playing. K back to Default cleared ViewFailed and rebound the player. The clone and temporary input devices were destroyed and all input/background settings restored.
- Play Mode exit produced no additional errors; GameplayScene stayed clean.
- Every Core source/metadata SHA-256 hash matches task start. Compiled Core references remain netstandard and DefaultEcs only.
- Focused changed-source whitespace checks passed.

Saved results are under ignored Sokoban/Logs/skin-switch-task/: step1-playmode.json, final-editmode.json, final-playmode.json, guard-playmode.json, controls-smoke.json, next-level-smoke.json, failure-smoke.json, and core-baseline.json. The full worktree still includes pre-existing work; no commits were made.

No standalone player build was run. Visible alternate artwork was deliberately left for the following Editor steps. Default fallback and failure recovery were checked in the real scene; distinct fake prefab content was checked in tests. Individual entity destruction/reuse and external view replacement remain unsupported.

## Manual Editor setup

1. Open the default Player prefab referenced by Assets/_Sokoban/Resources/LevelViewCatalog.asset. Create a Prefab Variant through the Project window and name it PlayerAlternate. Keep its EntityView and adapters inherited/copied from the default. Change its material or appearance so the two skins are visibly distinct.
2. Mark PlayerAlternate Addressable in the Inspector and place it in the appropriate existing Addressables group. Keep the default Player reference unchanged.
3. Select LevelViewCatalog. Existing entries should show Skin=Default. Add one entry with Kind=Player, Skin=Alternate, and Prefab=PlayerAlternate. Keep exactly one Default entry for every kind; do not duplicate a Kind/Skin pair.
4. No references should need reassignment for this change: ViewKeyMap was renamed through AssetDatabase with its GUID preserved, adapter script identities are unchanged, and the catalog's added field defaults to zero/Default. If Unity reports a missing reference, reassign that specific catalog/prefab field in the Inspector rather than editing YAML.
5. When using Use Existing Build, rebuild Addressables content after adding or changing the Alternate prefab/reference. For Editor-only iteration, use the project's appropriate Asset Database play-mode script. Save any intended authoring changes through Unity.

## Manual checklist

- Enter GameplayScene Play Mode and press K while moving. The player switches skin without pausing movement. Other entities retain their default skin.
- Choose Alternate, press R, then N or solve the level. The next run starts with Alternate already selected, without first spawning a Default player.
- Remove only the Alternate catalog entry temporarily and press K. The player uses Default appearance, with no errors and no extra cached handle for the missing skin.
- With a valid Alternate entry, deliberately make its Addressables runtime lookup fail. K should log one error, leave an invisible player with ViewFailed, and keep the controller Playing. K back to Default should restore the player. Correct the reference and rebuild content as needed afterward. A syntactically invalid Inspector reference may instead be rejected by catalog validation at startup, which is intentional.
- Press K while the old request is still loading. No obsolete Default view should appear after switching to Alternate; the latest request binds after the old one completes. Rapid K presses leave one eventual view for the final desired key.
- Open Window > General > Test Runner. Run the project's EditMode assemblies (167 cases) and Sokoban.PlayModeTests (67 cases). Fault-injection exceptions are explicitly expected by their tests.
