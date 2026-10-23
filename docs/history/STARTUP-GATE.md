# Query-based startup view gate

**Historical record.** Behavior, setup steps, pass counts, and pending proposals below belong to the recorded milestone. Use [current architecture](../ARCHITECTURE.md) and [current setup](../SETUP.md) for the implemented project.

Implemented on 2026-10-22, uncommitted. This supersedes the immediate-Playing and permanently cached-failure behavior in ECS-VIEWS.md.

ViewQuery.Viewable defines GridPosition plus Wall/Box/Player/Goal/Floor tag eligibility once. ViewBindingSystem reuses it and adds its pending/failed/bound exclusions. ViewLayer owns and disposes two live EntitySets: eligible entities without TransformComponent, and all entities with ViewFailed. Status gives Failed priority, then Binding, then Ready. No readiness system, marker, asynchronous readiness API, or readiness token was introduced.

GameplayController uses Loading, BindingViews, Playing, and Failed. StartRun enters BindingViews and starts its usual initial view tick. Every frame ticks both existing layers, including while Loading or Failed; only Playing reads movement input. After ticking, BindingViews transitions according to ViewLayer.Status. Playing never re-enters the barrier for later view problems. R/N remain available in BindingViews and Failed, as well as Playing, and are withheld during Loading to avoid overlapping definition loads. A definition-load error enters Failed; R can retry when the initial load produced no definition/run.

ViewAssets uses only AttachExternalCancellation for caller waits. Its ToUniTask conversion has no caller token and auto-release remains disabled. Successful/pending handles stay shared; a later request releases and evicts a failed cached handle before starting a new attempt. A cache entry deduplicates its completion callback and all waiting callers' error paths. Synchronous catalogue/setup/conversion errors also log and propagate. Cancellation is propagated without logging an error. Logging is once per attempt: a later retry that fails can log again. ViewFailed still prevents automatic retries for the same entity; R/N creates fresh entities that can request again.

The existing IsValid guards remain at release boundaries for Addressables' Editor shutdown invalidation. The cache's failure-log flag records log delivery for one attempt; it is not a disposal/lifecycle guard. The existing per-run binding cancellation source remains unchanged.

Changed runtime files: GameplayController.cs, ViewLayer.cs, ViewAssets.cs, and Ecs/Systems/ViewBindingSystem.cs. Added runtime files: ViewQuery.cs and ViewStatus.cs. All paths are under Sokoban/Assets/_Sokoban/Code/Unity. Added Tests/PlayMode/StartupGateTests.cs and updated ViewBindingTests.cs and PresentationPipelineTests.cs. No Core files, packages, asmdefs, scenes, prefabs, or serialized assets were changed; Unity generated new-script metadata.

Verification:

- Compilation succeeded.
- All 164 EditMode and 46 PlayMode cases passed. Results are in Logs/startup-gate-final-editor.json and Logs/startup-gate-final-playmode.json.
- Tests cover missing/unrequested views, Failed priority, both layers ticking during startup/loading, movement input gating, R/N recovery from BindingViews and Failed, initial definition-load retry, and no continuous pause after Playing.
- Cache tests cover early synchronous errors, one log for a shared failure across completion/caller paths, caller-only cancellation, corrected-reference retry, and release of the evicted failed handle.
- Core source hashes match the task-start snapshot.
- The saved scene reached Playing/Ready with 76 entities and 76 views, advanced frames, and exited Play Mode without new errors. The background setting was restored and the scene remains clean outside Play Mode.

No manual Editor setup is required. No commits were made.
