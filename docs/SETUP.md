# Setup and content authoring

Current workflow, checked against source and saved configuration on 2026-10-23. Open the Unity project in `Sokoban/`, using the Editor version in `Sokoban/ProjectSettings/ProjectVersion.txt` (currently 6000.0.63f1). Resolve existing dependencies and wait for imports and compilation. Package versions belong to the project manifests and vendored packages; no additional package installation is required by this guide.

## Run the project

1. Open `Assets/_Sokoban/Scenes/GameplayScene.unity`.
2. On the ServicesInstaller component, confirm View Catalog, Level List, and Level Root are assigned. The shipped assets are `Assets/_Sokoban/Resources/LevelViewCatalog.asset` and `LevelList.asset`.
3. Run **Tools > Sokoban > Validate Addressable Addresses**. Validation throws on invalid data; successful validation returns without an error. It checks current Editor settings, not the freshness of an existing content build.
4. Open **Window > Asset Management > Addressables > Groups**. For the shipped **Use Existing Build** workflow, save content and build **Build > New Build > Default Build Script** before entering Play Mode. **Use Asset Database (fastest)** is also available for authoring checks without built bundles.
5. Enter Play Mode with the Game view focused. A connected keyboard is required.

| Input | Behavior |
| --- | --- |
| Arrows / WASD | Move once per key press; push boxes when space permits. |
| R | Restart the current definition; after a level-load failure, retry the attempted level. |
| K | Cycle player skins; the selection survives restart and level changes. |
| N | Cycle levels in Editor/development builds. |

Solving a level advances after 0.75 game seconds. Level selection currently wraps. While a definition loads, controls are ignored. Pending skin binding during Playing keeps gameplay active; a failed view pauses movement in ViewsFailed. K can recover a failed skin, and R/N remain recovery controls outside Loading. See [architecture](ARCHITECTURE.md) for state and ownership details.

## Configure content

Assign addresses in the Addressables Groups window. Use those exact strings in the catalog/list; they are addresses, not automatically derived file paths. Keep them unique for these single-asset loads. Groups must be included in builds and bundled groups must include addresses in their catalogs.

| Catalog/list entry | Address | Asset type |
| --- | --- | --- |
| Floor / Default | `views/floor` | GameObject prefab |
| Wall / Default | `views/wall` | GameObject prefab |
| Box / Default | `views/box` | GameObject prefab |
| Player / Default | `views/player` | GameObject prefab |
| Player / Alternate | `views/player_alt` | GameObject prefab |
| Goal / Default | `views/goal` | GameObject prefab |
| Level index 0 | `levels/level-01` | TextAsset |
| Level index 1 | `levels/level-02` | TextAsset |
| Level index 2 | `levels/level-03` | TextAsset |

ViewCatalog has exactly one group per ViewKind. Each group contains Skins entries with Skin and Address, including exactly one Default and no duplicate skins. A missing requested skin falls back to Default. A configured skin whose address cannot load fails explicitly. To add a skin choice to cycling, also extend the contiguous SkinId enum; adding a catalog entry alone does not change the enum cycle.

LevelList.Levels is an ordered string array. To add a level, follow the [level format](../Sokoban/Assets/Levels/LEVEL_FORMAT.md), mark its TextAsset Addressable, and append its address. The first list element starts the session. File-based tests discover all `Assets/Levels/*.txt`; the runtime only visits addresses in LevelList.

Prefab meshes, materials, scale, rotation, and child offsets belong to the prefabs. The binder instantiates the root at the grid world position and installs EntityView adapters when present; the existing EntityView/TransformAdapter setup can remain. Save prefab changes before rebuilding Addressables. Renaming an address requires updating its catalog/list string.

## Verification

Run project EditMode assemblies, including Core parser/solver tests and Sokoban.Editor.Tests, then PlayMode assembly Sokoban.PlayModeTests in **Window > General > Test Runner**. These cover deterministic rules, saved-scene behavior, cache/fallback, cancellation, stale results, and failure recovery. Test counts and results must come from the run performed, not an old report.

For a content change, rebuild and check startup, movement/pushing, K in both directions, R preserving the skin, N progression, and Play Mode exit. Confirm frames advance and inspect the Console. A still image alone does not prove that an unfocused Editor is ticking; see [Unity tooling](UNITY_TOOLING.md).

## Troubleshooting and profiling

- **InvalidKeyException:** compare the exact configured address with the Groups entry, run validation, and rebuild content for Use Existing Build. An asset existing on disk does not prove it exists in the loaded catalog.
- **Old skin appearance:** save the prefab first, rebuild content, and restart Play Mode. A content build contains the saved state from build time.
- **Profiler:** the installed Addressables package uses the **Addressable Assets** Profiler module rather than the legacy Event Viewer. Enable Debug Build Layout before building; use built content and the matching build report. Earlier observed UI limitations are recorded in the [historical content report](history/ASYNC-CONTENT.md#diagnostics-in-the-installed-package), and should be rechecked if encountered again.

Completed migration and verification records are in [history](history/README.md). They are not additional setup steps.
