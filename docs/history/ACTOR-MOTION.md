# Actor-motion split

Implemented on 2026-10-25 over the existing uncommitted hints/replay/undo work. The staged, unstaged, and untracked files were inspected first and preserved. This refactor remains uncommitted; no push.

ActorMotionSystem retains its public (World, float duration) constructor. Its SequentialSystem owns ActorStateInitSystem, ActorMoveDetectionSystem, and ViewMoveAdvanceSystem in that order, with delegated enable/update/dispose. Failed construction disposes acquired children in reverse. Detection additionally cleans up its acquired player/box queries and base set if its construction fails.

ActorQuery centralizes the existing actor predicate beside ViewQuery. All three children use buffered AEntitySetSystem iteration, extending the actor predicate for missing state, existing state, or ViewMove respectively. Initialization no longer owns a manual array buffer. Detection computes push status once in PreUpdate before any detection state write, retaining widened long coordinate differences. Advancement runs after detection so a newly created move receives the current delta immediately. Completed moves are removed; other values are replaced through entity.Set.

ViewMove is readonly data with constructor, Advance, IsDone, and Position. Advance retains the original elapsed upper clamp and interpolation formula, with no new validation or Unity-object effects. ActorViewState remains data in its existing form. Retargeting, facing, lazy initialization, skin binding, and downstream feature ordering are preserved.

The installed package nuspec and runtime assembly both identify DefaultEcs 0.17.2. Its bundled XML and loaded Update method were inspected: PreUpdate runs before buffered entity processing, and useBuffer uses the library's own pooled buffer. No package, assembly, friend declaration, asset, scene, Core rule, or UI change was required. Existing feature/facade call sites remain unchanged. Existing animation/feedback fixture edits only replace ViewMove object initializers with constructor syntax.

| Project check | Fresh baseline | Final |
| --- | --- | --- |
| EditMode, Sokoban assemblies | 269 / 269 passed | 288 / 288 passed |
| PlayMode, Sokoban.PlayModeTests | 167 / 167 passed | 167 / 167 passed |
| Focused ActorMotionTests | 8 original cases | 27 / 27 passed |

Compared FullName and Status: all original baseline cases remain present and pass; 19 meaningful EditMode cases were added. No assertions were deleted or weakened. New coverage checks both actor creation orders for push detection, identical player/box progress during the first update, wrapped-coordinate false-push rejection on both axes/directions, immutable values at zero/partial/completion/overshoot/negative delta, buffered multi-actor completion, unchanged nonpositive-duration behavior, nonzero-delta retargeting, authored initial pose, disabling/re-enabling all stages, and world/motion ownership after disposal and replacement. Existing full-suite coverage verifies blocked input, real player animation, mid-push skin swaps, reverse undo interpolation, input buffering/countdown, hint availability, box feedback, and run teardown.

The initial helper compile revealed that DefaultEcs's final Or call returns EitherBuilder rather than EntityQueryBuilder. The helper now closes the either group with With<GridPosition>, following the existing ViewQuery convention and preserving the same predicate. The final compilation and suites passed.

Local ignored raw reports are in Sokoban/Logs/actor-motion/: motionBaselineEdit.json, motionBaselinePlay.json, motionFocused.json, motionFinalEdit.json, and motionFinalPlay.json. git diff --check passed.

Construction rollback was reviewed through its explicit acquisition/disposal paths; allocation/query-acquisition failures were not fault-injected, and no test-only constructor or broader production visibility was added. Dependency suites, standalone builds, a new manual playthrough, and Addressables rebuilds were not run. No Addressable content/dependency changed. The Editor finished outside Play Mode with a clean saved scene and unchanged background-running policy.
