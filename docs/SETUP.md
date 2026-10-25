# Setup and content authoring

Current workflow, checked against source and saved configuration on 2026-10-24. Open the Unity project in `Sokoban/`, using the Editor version in `Sokoban/ProjectSettings/ProjectVersion.txt` (currently 6000.0.63f1). Resolve existing dependencies and wait for imports and compilation. Package versions belong to the project manifests and vendored packages; no additional package installation is required by this guide.

## Run the project

1. Open `Assets/_Sokoban/Scenes/GameplayScene.unity`.
2. On ServicesInstaller, confirm **Level Root** is assigned. Config assets live in `Assets/Addressables/Config/`: `GameplaySettings.asset`, `LevelList.asset`, and `LevelViewCatalog.asset`. Each is Addressable and carries the **Configs** label. Keep exactly one asset of each type under that label. Scene installers do not reference config assets directly.
3. Run **Tools > Sokoban > Validate Addressable Addresses**. Validation throws on invalid data; successful validation returns without an error. It checks current Editor settings, not the freshness of an existing content build.
4. Open **Window > Asset Management > Addressables > Groups**. For the shipped **Use Existing Build** workflow, save content and build **Build > New Build > Default Build Script** before entering Play Mode. **Use Asset Database (fastest)** is also available for authoring checks without built bundles.
5. To show the temporary debug status/results, create an active GameObject in the gameplay scene and add GameplayDebugOverlay. Keep it in that SceneContext's scene, outside generated Level view roots. Zenject injects IGameplayStatus and IGameplayCommands; no additional service binding or Inspector reference is required. Save the scene.
6. Enter Play Mode with the Game view focused. A connected keyboard is required.

| Input | Behavior |
| --- | --- |
| Arrows / WASD | Move once per key press; push boxes when space permits. One additional move can wait for the current visual step; further presses are dropped. |
| R | Restart the current definition; after a level-load failure, retry the attempted level. In Finished, clear results and restart from level 1. |
| K | Cycle player skins; the selection survives restart and level changes. |
| N | Cycle levels in Editor/development builds. |
| Top-right Restart / Switch skin buttons | Same controller actions as R / K; loading disables both. Finished shows Play again. |

Solving a level advances after the settings asset's **Completion Delay Seconds**, initially 0.75 game seconds; zero is valid. After the last level's completion pause the phase becomes Finished, retaining the last board and showing recorded results in the debug overlay. Automatic progression does not wrap; the development N shortcut still does. The same asset's **Level Time Limit Seconds** sets one limit for all levels, initially 60 seconds. Both values must be finite, the limit positive, and the delay nonnegative. Move Duration Seconds is a separate finite positive value, initially 0.16 seconds; it controls player/box interpolation while simulation and the countdown continue. An unsolved level automatically restarts when its budget expires; every restart or new level starts with a full budget. A winning move succeeds on the final tick and freezes the timer before that tick's decrement. Simulation runs only during Playing and uses scaled game time. Startup binding, view-failure recovery, and completion presentation tick views only; pending skin binding during Playing still consumes time. IGameplayStatus exposes phase, level, remaining time, moves, and completed results for the optional GameplayDebugOverlay; no production UI is installed. Per-level limits are deferred; their future location is LevelList entries with fallback to GameplaySettings.

Startup loads the config batch and requires exactly one instance of each expected config type before requesting the first level. Configs themselves contain no Validate methods; author their values and collections in the Inspector. Separately, the public Simulation constructor rejects a nonfinite/nonpositive time limit, and Tick rejects a nonfinite/negative delta before changing state. Zero delta is valid. This Core API policy does not add config Validate methods or validate completion-delay/movement tuning. An invalid level time limit fails run creation and follows the existing LevelFailed/restart path. Controls are ignored during config or level loading. A failed config load enters LevelFailed; R retries config loading and then the attempted level. Configs remain loaded across R/N and automatic restarts. A failed view pauses movement in ViewsFailed. K can recover a failed skin, and R/N remain recovery controls outside Loading; Finished specifically ignores N and treats R as replay. See [architecture](ARCHITECTURE.md) for state and ownership details.

## Configure content

Assign addresses in the Addressables Groups window. Use those exact strings in the catalog/list; they are addresses, not automatically derived file paths. Keep them unique for these single-asset loads. Groups must be included in builds and bundled groups must include addresses in their catalogs.

| Catalog/list entry | Address | Asset type |
| --- | --- | --- |
| Gameplay settings | `configs/gameplay` | GameplaySettings |
| Level list | `configs/levels` | LevelList |
| View catalog | `configs/views` | ViewCatalog |
| Floor / Default | `views/floor` | GameObject prefab |
| Wall / Default | `views/wall` | GameObject prefab |
| Box / Default | `views/box` | GameObject prefab |
| Player / Default (Doctor) | `views/player` | GameObject prefab |
| Player / Alternate (Farmer) | `views/player_alt` | GameObject prefab |
| Player / Punk | `views/player_punk` | GameObject prefab |
| Goal / Default | `views/goal` | GameObject prefab |
| Level index 0 | `levels/level-01` | TextAsset |
| Level index 1 | `levels/level-02` | TextAsset |
| Level index 2 | `levels/level-03` | TextAsset |

Author ViewCatalog with exactly one group per ViewKind. Entries and each group's Skins are lists. Each group contains Skins entries with Skin and Address, including exactly one Default and no duplicate skins. A missing requested skin falls back to Default. A configured skin whose address cannot load fails explicitly. To add a skin choice to cycling, also extend the contiguous SkinId enum; adding a catalog entry alone does not change the enum cycle.

LevelList.Levels is an ordered collection of serializable LevelConfig entries, each containing a string Address. Config assets use current serialized auto-property backing fields without migration attributes; save changes through the Editor. To add a level, follow the [level format](../Sokoban/Assets/Levels/LEVEL_FORMAT.md), mark its TextAsset Addressable, and append an entry with that address. StaticDataProvider.GetLevelConfig(index) returns the entry at a zero-based index; LevelCount exposes the count. An invalid index fails loudly. The first entry starts the session. File-based tests discover all `Assets/Levels/*.txt`; the runtime only visits entries in LevelList.

Config addresses identify the assets, while the **Configs** label supplies runtime discovery. Keep their group included in builds with both address and label inclusion enabled. Save config changes and rebuild content. No Resources fallback is used. GameplaySettings retains the default 60-second limit and 0.75-second completion delay; per-level overrides remain deferred.

Prefab meshes, materials, scale, rotation, and child offsets belong to the prefabs. The binder instantiates the root at the grid world position and installs EntityView adapters when present; the existing EntityView/TransformAdapter setup can remain. Save prefab changes before rebuilding Addressables. Renaming an address requires updating its catalog/list string.

The three animated player prefabs live under Assets/Addressables/Player and inherit PlayerBase. Keep PlayerAnimator and PlayerAnimatorAdapter on the wrapper root, assign the nested Animator to the facade, and include both TransformAdapter and PlayerAnimatorAdapter in EntityView's adapter list. All variants share the controller, push pose, and arm mask under Assets/AddressablesDependencies/Player/Animation. Disable root motion on the nested Animator. Idle and walk clips come from the imported zombie pack; author changes in the shared controller or project-owned push pose rather than modifying the source model prefabs.

## Verification

Run project EditMode assemblies, including Core parser/solver tests and Sokoban.Editor.Tests, then PlayMode assembly Sokoban.PlayModeTests in **Window > General > Test Runner**. These cover deterministic rules, saved-scene behavior, cache/fallback, cancellation, stale results, and failure recovery. Test counts and results must come from the run performed, not an old report.

Saved-scene tests use the real Addressables config batch and production installer. Provider lifecycle tests additionally exercise pending operations, cancellation, missing/duplicate configs, invalid values, retry, and handle release.

Addressables service tests cover cancellation of one shared waiter, failed-operation propagation, owner release during loading, independent acquired references, built-content address/label loading, and level-text cleanup after parse failure or cancellation. The shared service does not cache or automatically release assets; the existing consumer owners retain those responsibilities.

After config loading completes, check the countdown through GameplayDebugOverlay or the debugger's GameplayController.RemainingSeconds. Config access before loading completes fails loudly:

- During initial view binding it stays at the full limit; it begins decreasing after Playing starts.
- During completion presentation it stays frozen; the next level receives the full limit.
- Solving on the last simulation tick wins; an unsolved timeout restarts the same run immediately with a full timer.
- K preserves the current budget and keeps counting during a pending rebind.
- A view failure pauses simulation; recovering with K resumes from the preserved budget.
- R and N start fresh budgets and preserve the selected skin.

For a content change, rebuild and check startup, movement/pushing, K through Doctor, Farmer, and Punk, R preserving the skin, N progression, and Play Mode exit. Confirm frames advance and inspect the Console. A still image alone does not prove that an unfocused Editor is ticking; see [Unity tooling](UNITY_TOOLING.md).

## Results and finish checklist

- A successful walk or push adds one move; a wall or blocked push adds none. The winning move is included.
- Timeout and ordinary R restart do not append results. They reset the current run count and budget.
- Finishing level 1 shows Completed for the configured delay, then loads level 2.
- Finishing the last level shows Finished, keeps the last board visible, and lists one row per recorded completion plus total moves/time. Time is the configured limit minus the frozen remaining time.
- In Finished, movement and N are ignored; K still switches skins. R clears results and loads level 1.
- Outside Finished, Editor/development N still cycles levels and wraps, without recording a result.
- A rotated player prefab retains its initial rotation until its first move; subsequent facing survives skin changes.
- Push animation, mid-move skin replacement, visual duration, and chained key presses behave as before.
- Run all project EditMode and PlayMode tests. Actor-motion EditMode tests are in the existing Sokoban.Editor.Tests assembly; no new test assembly or friend access is needed.

## Troubleshooting and profiling

- **InvalidKeyException:** compare the exact configured address with the Groups entry, run validation, and rebuild content for Use Existing Build. An asset existing on disk does not prove it exists in the loaded catalog.
- **Old skin appearance:** save the prefab first, rebuild content, and restart Play Mode. A content build contains the saved state from build time.
- **Profiler:** the installed Addressables package uses the **Addressable Assets** Profiler module rather than the legacy Event Viewer. Enable Debug Build Layout before building; use built content and the matching build report. Earlier observed UI limitations are recorded in the [historical content report](history/ASYNC-CONTENT.md#diagnostics-in-the-installed-package), and should be rechecked if encountered again.

Completed migration and verification records are in [history](history/README.md). They are not additional setup steps.

## Box goal feedback

Tune BoxGoalAnimator on Assets/AddressablesDependencies/Box/Prefabs/Box.prefab: Duration Seconds (0.22), Height (0.12), and Squash Amount (0.12). These values belong to the visual prefab, not GameplaySettings. Use a finite positive duration, finite nonnegative height, and a squash amount between zero and one; these are trusted Inspector values. Box arrivals publish synchronous DefaultEcs messages after movement commits. Presentation waits for the slide to finish, then animates only the box prefab's Visual child. Moving the box again or restarting cancels/restores the effect. Progression retains Completion Delay Seconds, so a deliberately shorter delay can cut off the cosmetic effect. Save the box prefab and rebuild Addressables after changing its feedback tuning.
Box feedback uses the existing PrimeTween **1.5.1** package, resolved from `Packages/com.kyrylokuzyk.primetween.tgz` by the local-tarball manifest entry. Retain that archive with the project; no DOTween dependency or PrimeTween experimental define is needed. The saved box prefab has BoxGoalAnimator referencing its Visual child and BoxGoalFeedbackAdapter referencing the facade. The adapter remains in EntityView's adapter list. The facade captures the authored child pose and restores it exactly on completion, cancellation, disable, or destruction.

PrimeTween playback is intentionally presentation-driven: its sequence is paused in the automatic Unity loop, and BoxGoalFeedbackLifecycleSystem supplies scaled presentation delta through BoxGoalAnimator.Tick. Loading that skips presentation freezes the effect; binding/view-failure/Completed/Finished keep their existing presentation ticking policy. Do not add a MonoBehaviour Update or a global manual tween update for this effect. Stopping/uninstalling a view releases the owned sequence immediately, with no additional tick required.