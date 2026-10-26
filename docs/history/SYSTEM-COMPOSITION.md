# Direct system composition

Implemented and verified on 2026-10-26, starting from the clean working tree at 20a4325. Changes remain uncommitted; no push.

Simulation and ViewLayer declare their actual systems in short CreateSystems iterators. SystemPipeline accepts IEnumerable<ISystem<float>>, acquires incrementally, executes in order, and owns reverse disposal. Lazy construction is deliberate: later constructor failure occurs inside acquisition, allowing earlier successfully acquired systems to be released before their owner releases its World or presentation resources. No new factory interface, registration framework, package, assembly, or friend declaration was introduced.

Core order: UndoSystem → MovementSystem → MoveCountSystem → GoalTrackingSystem → WinCheckSystem → LevelTimerSystem. MovementSystem owns validation → application and delegates enable/update/dispose to its sequence. Replay uses the same default composition with recording disabled.

Presentation order: SkinSwitchSystem → ViewBindingSystem → ActorMotionSystem → GridTransformSyncSystem → PlayerAnimationSystem → BoxGoalFeedbackCompositeSystem. ActorMotionSystem and all its existing tests are unchanged. The feedback composite owns lifecycle → message reaction; its name distinguishes it from the existing BoxGoalFeedbackSystem reaction class. Both new composites release acquired children on later construction failure.

Removed IFeature, RuleFeatures, ViewFeatures, every individual feature factory, both obsolete Features folders, and FeaturesInstaller. Feature parameters/fields were removed from LevelRunFactory, LevelRun, ViewLayer, and test call sites. The saved GameplayScene was updated through Unity Editor APIs before deleting FeaturesInstaller: both its component and SceneContext installer-list entry were removed. Only ServicesInstaller and GameplayInstaller remain in the installer list, with no missing scripts.

The internal Simulation constructor accepts Func<World, IEnumerable<ISystem<float>>> as the minimal composition-test seam. Tests no longer define TestFeature or inject factory arrays. Ordering and systems-before-world assertions remain; reverse-disposal order is now explicit. The new failing iterator test acquires real movement plus two probe systems, throws a planned later-construction exception, and verifies reverse probe disposal exactly once followed by World disposal/detachment. Existing world/history cleanup-on-disposal-failure checks remain.

| Check | Baseline | Final |
| --- | --- | --- |
| Project EditMode, Sokoban assemblies | 288 / 288 passed | 289 / 289 passed |
| Project PlayMode, Sokoban.PlayModeTests | 167 / 167 passed | 168 / 168 passed |
| Focused SimulationTests | — | 18 / 18 passed |
| Focused MovementTickTests | — | 9 / 9 passed |
| Focused ActorMotionTests | — | 27 / 27 passed |
| Focused BoxGoalFeedbackTests | — | 17 / 17 passed |

Compared actual FullName/Status values, not only totals. ComposedFeatures_RunInListOrderBeforeAndAfterMovement became ComposedSystems_RunInListOrderBeforeAndAfterMovement; Simulation_DisposesFeaturesExactlyOnceBeforeItsWorld became Simulation_DisposesSystemsExactlyOnceInReverseBeforeItsWorld; both cases of Disposal_ClearsHistoryEvenWhenAFeatureThrows became Disposal_ClearsHistoryEvenWhenASystemThrows. All other baseline names are unchanged; every mapped baseline case passes. One construction-cleanup EditMode case and one feedback-composite enable/pause PlayMode case were added. No behavioral assertions were removed or weakened.

Existing suites exercise movement, pushes/blocks/overflow, final-tick victory, timer freezing, move counts, undo/history, deterministic replay, hints, asynchronous binding, skin swaps, player animation, PrimeTween feedback, stale operations, restart/replacement, and scene teardown. The focused feedback helper now uses the production composite, including its existing active-tween disposal checks. A new case verifies arrivals buffer while disabled, active feedback pauses, and re-enabling resumes/restores it.

DefaultEcs runtime version was verified as 0.17.2.0. Core's compiled references remain netstandard and DefaultEcs only. No gameplay rule, data component, actor-motion implementation, config, Addressable asset, PrimeTween behavior, or UI framework was changed. The saved scene is clean and the Editor ends outside Play Mode with runInBackground false and timeScale 1.

Local ignored evidence is under Sokoban/Logs/composition/: compositionBaselineEdit.json, compositionBaselinePlay.json, compositionFocusedSimulationTests.json, compositionFocusedMovementTickTests.json, compositionFocusedActorMotionTests.json, compositionFocusedFeedback.json, compositionFinalEdit.json, and compositionFinalPlay.json. A focused command was interrupted by an Editor transport/reload; verified reruns and their actual case names were used above. git diff --check passed.

No dependency suite, standalone player build, new native-input smoke test, or Addressables rebuild was run. No Addressable content/dependency changed. Allocation failures inside the individual composites were not fault-injected; their acquisition cleanup paths were reviewed, while pipeline partial-construction cleanup was directly tested. Existing cleanup-exception propagation remains unchanged.
