# Historical milestones

These records preserve earlier decisions, migrations, test counts, and known limitations. Statements such as "current", "pending", "unchanged", or "uncommitted" refer to the recorded milestone. They do not describe the latest checkout or authorize new changes.

For implementation use [current architecture](../ARCHITECTURE.md); for running and authoring content use [current setup](../SETUP.md).

| Record | Scope |
| --- | --- |
| [Level results and finish](LEVEL-RESULTS-FINISH.md) | Animation cleanup, Core move/time results, public gameplay status, finish/replay, guard audit, and manual overlay setup. |
| [Early architecture](iterations/ARCHITECTURE-HISTORY.md) | Working design before the layer readability refactor. |
| [Movement example](iterations/iteration-1/MovementSystem.EntitySet.cs.txt) | Earlier EntitySet implementation sample, not compiled runtime code. |
| [Unity tooling setup](UNITY-TOOLING-SETUP.md) | Original machine-specific connection verification. |
| [Guard review](GUARD-REVIEW.md) | Earlier guard-policy decisions and review. |
| [Synchronous content setup](CONTENT-SETUP.md) | Former prefab and text authoring workflow. |
| [Async content](ASYNC-CONTENT.md) | Initial Addressables migration, profiling, and per-level preload ownership. |
| [ECS views](ECS-VIEWS.md) | Migration to per-entity binding and a shared prefab cache. |
| [Startup gate](STARTUP-GATE.md) | View readiness and startup failure behavior. |
| [Level runs and completion](LEVEL-RUN-COMPLETION.md) | Run ownership, win detection, and progression. |
| [Skin switching](SKIN-SWITCHING.md) | Requested skins, stale results, and view replacement. |
| [View failure recovery](VIEW-FAILURE-RECOVERY.md) | Shared failures and recoverable controller states. |
| [Grouped catalog](GROUPED-VIEW-CATALOG.md) | Kind/skin authoring groups with the former AssetReference fields. |
| [String addresses](STRING-ADDRESSES.md) | Migration to string addresses and Editor validation. |
| [Architecture milestone snapshot](ARCHITECTURE-MILESTONE-SNAPSHOT.md) | Full pre-cleanup architecture document, including the accumulated verification narrative. |

Report bodies are retained apart from historical labels and repaired links. Their source paths, proposals, and results may be superseded. Ignored Logs artifacts mentioned in reports are local evidence and may not be present in a fresh clone.
