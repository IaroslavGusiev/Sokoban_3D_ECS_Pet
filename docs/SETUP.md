# Setup and content authoring

Current workflow, checked against source and saved configuration on 2026-10-28. Open the Unity project in `Sokoban/`, using the Editor version in `Sokoban/ProjectSettings/ProjectVersion.txt` (currently 6000.0.63f1). Resolve existing dependencies and wait for imports and compilation. Package versions belong to the project manifests and vendored packages; no additional package installation is required by this guide.

## Run the project

1. Open `Assets/_Sokoban/Scenes/GameplayScene.unity`.
2. On GameplayInstaller, confirm **Level Root**, **Gameplay Camera** (the saved Main Camera), **Background Volume** (the saved Background sky Volume) and **Background Shader** (`Art/Backgrounds/BackgroundProjection.shader`) are assigned. The hint arrow is loaded from the ViewCatalog Default `HintArrow` entry at `views/hint_arrow`; no Inspector prefab reference is needed. Config assets live in `Assets/Addressables/Config/`: `GameplaySettings.asset`, `LevelList.asset`, `LevelViewCatalog.asset` and `BackgroundCatalog.asset`. Each is Addressable and carries the **Configs** label. Keep exactly one asset of each of these four types under that label. Scene installers do not reference config assets directly. Keep both existing installer components in SceneContext (ServicesInstaller followed by GameplayInstaller). GameplayInstaller owns these four scene references and composes LevelRoot and the self-only BackgroundDisplay/BackgroundCoordinator; ServicesInstaller supplies supporting services. Camera, Volume and Shader are local display-constructor arguments, not global service bindings.
3. Run **Tools > Sokoban > Validate Addressable Addresses**. Validation throws on invalid data; successful validation returns without an error. It checks current Editor settings, not the freshness of an existing content build.
4. Open **Window > Asset Management > Addressables > Groups**. For the shipped **Use Existing Build** workflow, save content and build **Build > New Build > Default Build Script** before entering Play Mode. **Use Asset Database (fastest)** is also available for authoring checks without built bundles.
5. The saved scene's GameplayDebugOverlay object owns a UIDocument and the command/status bridge. Keep it in that SceneContext's scene, outside generated Level view roots. UIDocument references `Code/Unity/GameplayUI/GameplayOverlay.uxml` and the project-owned `OverlayPanelSettings.asset`. Zenject injects IGameplayStatus and IGameplayCommands into the bridge; no additional service binding is needed. Preserve these serialized UI references when copying the scene.
6. Enter Play Mode with the Game view focused. The board and boxes appear first; choose a character and activate Start before the player appears. A connected keyboard is required.

| Input | Behavior |
| --- | --- |
| Arrows / WASD | Move once per key press; push boxes when space permits. One additional move can wait for the current visual step; further presses are dropped. |
| R | Restart the current definition; after a level-load failure, retry the attempted level. In Finished, clear results and restart from level 1. |
| Character choices / Start | Choose Doctor, Farmer or Punk from the existing Player catalog before entering gameplay. Confirm with mouse or keyboard. |
| Z / Undo button | Reverse the latest successful walk/push during Playing, with ready views and no active slide. Repeated undo walks backward through this run only. |
| Hint button | Search asynchronously for the next move toward a solution while Playing, with ready views and no active slide. Shows an arrow without executing the move. |
| N | Cycle levels in Editor/development builds. |
| Debug > Next background | Preview the next catalog preset in Editor/development builds; wraps after preset 10. On failure it becomes Retry background and retries the failed selection. The selected name and loading/failure status are in this collapsed section. |
| Next level / Watch replay | Completed nonfinal levels offer Next; both completion screens offer Watch. Replay offers Pause/Resume and Back, including during loading or failure. |
| Restart / Play again | Same restart request as R. Play again clears results and opens character selection for a new session. |
| Tab / Shift+Tab | Navigate enabled UI controls. Enter or Space activates the focused control. While selecting, arrows navigate the choices; Enter/Space selects a choice or activates Start/Retry. During Playing, arrows/WASD still move with UI focus, R/Z retain their actions, and K has no cycling action. |

Solving a nonfinal level enters a stable completion screen. **Next level** is the primary action; it advances once on the next application tick. There is no automatic completion timer. **Watch replay** plays the just-completed attempt, with **Pause/Resume** and **Back to results**; the final replay board remains until Back. Final completion shows results with **Watch replay** and **Play again**, which clears the playthrough and asks for fresh character confirmation. Completion/replay reject movement, undo, hint, restart and development level/skin commands; final Play again still shares R. Watching neither changes totals nor chooses a new background.

GameplaySettings retains **Level Time Limit Seconds** (finite positive, initially 60) and **Move Duration Seconds** (finite positive, initially 0.16). **Completion Delay Seconds** and its saved data were removed; it has no replacement setting. Rebuild Addressables after pulling this config migration. An unsolved timeout still restarts with the confirmed skin/full time budget. A winning move freezes time before that tick's decrement. Normal simulation runs only in Playing; replay supplies its own original recorded deltas.

Startup loads the config batch and requires exactly one instance of each expected config type before requesting the first level. Configs themselves contain no Validate methods; author their values and collections in the Inspector. Separately, the public Simulation constructor rejects a nonfinite/nonpositive time limit, and Tick rejects a nonfinite/negative delta before changing state. Zero delta is valid. This Core API policy does not add config Validate methods or validate movement tuning. An invalid level time limit fails run creation and follows the existing LevelFailed/restart path. Controls are ignored during config or level loading. A failed config load enters LevelFailed; R retries config loading and then the attempted level. Configs remain loaded across R/N and automatic restarts. A failed view pauses movement in ViewsFailed. Initial selected-character failure allows Retry/another choice; later character-load failure offers same-skin Retry with choices disabled. A missing confirmed catalog entry also waits for same-skin retry instead of using the view cache's Default fallback. Board failures retain R/N recovery outside Loading/selection; Completed/Finished ignore N; R in Finished means Play again, not Watch replay. See [architecture](ARCHITECTURE.md) for state and ownership details.

Undo restores affected positions and the previous move count, but never restores time. It reuses reverse interpolation and cancels box feedback when the box moves. Blocked/invalid/overflow moves create no history; undo itself creates no move/history entry and no goal-arrival bump. The next forward arrival still animates normally. Unavailable requests are discarded; Loading, Completed, Finished, failure, timeout, pending view binding, and active movement reject undo. Restart/next-level commands keep priority, and accepting undo drops any buffered forward movement. Restart and level progression discard history. No redo, undo after victory, persistence, or cross-level history is provided. The gameplay controls have Restart, Undo (Z), and Hint; Undo and Hint use their respective availability properties.

Hints use a copied current board and a background breadth-first search, capped at 100,000 visited states. While the button reads **Calculating…**, movement and the countdown continue. A found result shows **Hint: direction** and a yellow arrow above/near the player; it recommends a move on a discovered solution, with no penalty or automatic input. **No solution found**, **Search budget exhausted**, and **Calculation failed** are separate outcomes. A large board can exceed this first version's budget. The solver supports the rectangular, enclosed boards loaded by this application; it does not promise solutions for arbitrary out-of-bounds programmatic Core definitions.

Successful movement or undo hides the arrow and cancels pending work, even if undo returns to identical positions. Restart, level replacement, completion, timeout, disposal, or unavailable views invalidate the result. Idle/blocked input preserves it; skin changes do not change the board revision, though a pending rebind invalidates an unavailable view. Requests made while unavailable are dropped, and a valid pending calculation disables the button. Old results/failures cannot replace newer hints. The arrow is a lazy child of the run's presentation root with no collider or ECS entity; its prefab, mesh, and material are Addressable content dependencies. Rebuild Addressables after changing its content or catalog entry. The button stays pending while the visual loads; an asset failure reports hint failure with retry available while gameplay continues.

## Character entry and retry

Initial entry and every newly entered level load/display the board and boxes first. The normal Core player entity exists, but its presentation view is intentionally deferred until confirmation. The chooser preselects the previously confirmed character when present in the loaded Player catalog. The supported list comes from `LevelViewCatalog.asset` Player skins; there is no separate supported-choice list or runtime preview service. Static portraits are serialized UI dependencies keyed by SkinId. Display names for current IDs are ordinary presentation strings.

SelectingCharacter, BindingCharacter and CharacterFailed are explicit application phases. No simulation, move input, timer consumption or recording tick runs during them. Start requests only the selected player prefab through the existing cache/binding pipeline, with no transient default character. Duplicate confirmation is ignored while binding. The application waits for required view readiness and released keyboard/primary mouse input before Playing, so confirmation cannot leak into gameplay. Retry preserves the selected character; choosing another clears the previous failure. Other genuine board-view failures remain ViewsFailed.

Manual and timeout restarts reuse the confirmed character and skip selection. Play again opens it for a new playthrough. Progression and development next-level entry reuse the confirmed skin without reopening selection. Only the initial/new-playthrough chooser refreshes catalog choices; confirmation is not persisted to disk. During selection, R/Z/N/K do not trigger hidden gameplay commands. Tab/Shift+Tab and arrows navigate UI, Enter/Space activates a choice or Start/Retry, and mouse buttons work normally. The normal Switch skin control and K badge/path are removed. Internal view replacement remains supported and tested independently. The centered chooser hides the ordinary HUD/controls and Debug during selection, loading and failure. Its dimmed full-screen backdrop blocks pointer input; normal transparent gameplay areas pass through again after it closes.

Selection adds no asset owner or Addressables registrations. Per-run cancellation, per-entity request identity, stale completion checks and shared-cache release remain with existing owners. New UI templates are scene-owned dependencies; UI/code-only changes need no Addressables rebuild. Changing a Player catalog address or prefab still requires validation and a content rebuild.

### Static character portrait authoring

Use **Sokoban > UI > Regenerate character portraits** outside Play Mode. `CharacterPortraitAuthoring` resolves the Player entries from `LevelViewCatalog.asset` through the Editor Addressables settings and renders the actual prefab models in an isolated `PreviewRenderUtility` scene. It samples the first ordinally named idle clip at 0.4 seconds, keeps authored model scale, and uses a fixed three-quarter orthographic camera and two preview lights. It changes no source prefabs/materials or gameplay lighting/settings. RGBA readback preserves transparency; linear targets are converted to PNG sRGB. The preview scene, instances, lights, camera, target and temporary texture are cleaned in finally, with the previous active render target restored even on failure.

Generated 512×640 PNGs live in `Assets/_Sokoban/Art/UI/Portraits/`, named by SkinId. Existing PNGs are overwritten through Unity import without changing their GUIDs. `CharacterSelection.uxml` contains hidden `CharacterPortrait` elements with explicit `skin` and `portrait` attributes; add the corresponding association when authoring a new catalog character. These are presentation metadata, not another available-character list. The cloned `CharacterChoice.uxml` receives the referenced Texture2D; it never requests or instantiates a gameplay prefab. Keep transparent import, NPOT scaling None, no mipmaps, clamp wrapping and sRGB. Verify head/feet margins after model or pose changes. UI-only PNG/template changes need no Addressables content rebuild; they are dependencies of the scene-owned visual tree.

Check 1920×1080, 1280×720 and 1440×1080. The centered modal intentionally overlaps the board, keeps Start/Retry outside the scrollable choice area, and contains loading/failure feedback in the same window. Tab/Shift+Tab traverse cards and Start/Retry; Left/Right browse the horizontal cards, Down reaches the action, and Enter/Space activates the focused control. A gold border indicates focus; a sage border plus check/Selected label indicates the chosen character. The gameplay HUD must be clear of the board after selection closes.

## Runtime UI authoring

The main player UI assets live beside its bridge in `Assets/_Sokoban/Code/Unity/GameplayUI/`. The scene object/component retains its public `GameplayDebugOverlay` identity; the folder name no longer implies a debug-only interface. Edit `GameplayOverlay.uxml` for structure and `GameplayOverlay.uss` for styling; `GameplayOverlay.tss` provides the project-owned panel theme. `GameplayResultRow.uxml` defines the repeated Level/Moves/Time row. `CharacterSelection.uxml` and `CharacterChoice.uxml` define the centered selection window and repeated portrait card. CharacterSelectionView only requests application commands; its buttons and scheduled focus are owned by the root view. Validate imports and rendering in Unity. `OverlayPanelSettings.asset` retains 1280×720 height matching. The HUD and controls use measured gutters around the current board bounds; below aspect 1.55, both use the wider left gutter. Recheck 1920×1080, 1280×720 and 1440×1080 against all shipped boards after layout changes rather than assuming a centered top strip is clear. Keep transparent/decorative containers on Ignore picking, while result rows retain wheel input.

Completed keeps its success panel visible with Next level and Watch replay while movement/box feedback settle; it never advances automatically. Finished uses a dedicated result panel with shared column widths, nonwrapping time values, a bounded table ScrollView and a fixed totals/Play again footer. Reserve the same scrollbar gutter in the header and totals to retain alignment for long lists. Play again uses the existing restart semantics; Next level is required after a nonfinal completion. GameplayReplayView owns the replay-only status and Pause/Resume/Back controls; GameplayResultsView owns Next/Watch/Play again. Camera, font selection and scene composition remain unchanged. The selection mode has its own input policy; gameplay retains its existing input behavior after the confirmation input is released.

```text
[VisualElement] name="overlayRoot" class="overlay"
├── [VisualElement] name="statusPanel" class="hud status-side" — level, moves, time
├── [VisualElement] name="completed" class="completion-toast" — Solved, statistics, Next level and Watch replay
├── [VisualElement] name="replayPanel" class="replay-panel controls-panel" — playback status, Pause/Resume, Back
├── [ScrollView] name="controlsScroll" class="side controls-side" — actions, hint, Debug
├── [VisualElement] name="characterSelection" class="selection-layer" — centered portrait window, feedback, Start/Retry
└── [VisualElement] name="results" class="finished-layer"
    └── [VisualElement] name="resultsPanel" class="result-panel" — header, table, totals, Watch replay, Play again
```

`OverlayTextSettings.asset` references the project-owned static `OverlayFont.asset`, generated from Unity's built-in LegacyRuntime font. Its material and SDF atlas are saved subassets, so runtime text needs no OS font or Addressables load. It includes printable ASCII plus ellipsis/middle dot; extend the atlas before adding other character sets. The scene references the UXML and PanelSettings directly; those bring in USS/theme/font dependencies in a player build. UI-only edits require no Addressables rebuild. Keep the existing project-wide `InputSystem_Actions.inputactions` UI map, including Enter and the added Space submit binding. No new EventSystem is required for Unity 6000.0.63f1 / Input System 1.20.1.

The `selection-host` template is a sibling of `overlayRoot` in the current saved UXML. Both roots explicitly receive the same font definition, text size and base color from the shared `.overlay, .selection-host` USS rule. Keep that rule when reorganizing template placement: a PanelTextSettings default alone does not supply the resolved font for these labels. Validate ordinary Play Mode startup after a domain reload and fresh Editor launch, before any capture fixture or font-inspector warming. `GameplayOverlayTests.SelectionAndGameplayTextProduceVisibleGlyphs` compares rendered glyph pixels with text hidden in selection/loading/retry/gameplay, in addition to checking resolved font and bounds. The PanelSettings dynamic atlas retains all five defined texture filters (mask 31); its 64px texture threshold is unrelated to the saved 1024px font atlas.

Playback keeps one in-memory recording for the just-completed attempt. Its original presentation is hidden during Watch and restored on Back; it is not another game run. Replay waits for views, preserves recorded input ordering/deltas and catches up with at most 256 entries per update, retaining the remainder. Pause freezes presentation time and Animator speed. No seeking, speed selection, replay browser or saved/cross-session history is provided. Back after a replay-load failure permits retry through Watch. Shared view assets remain owned by the existing cache; stopping playback cancels its waiters without releasing shared prefab handles.

## Configure content

Assign addresses in the Addressables Groups window. Use those exact strings in the catalog/list; they are addresses, not automatically derived file paths. Keep them unique for these single-asset loads. Groups must be included in builds and bundled groups must include addresses in their catalogs.

| Catalog/list entry | Address | Asset type |
| --- | --- | --- |
| Gameplay settings | `configs/gameplay` | GameplaySettings |
| Level list | `configs/levels` | LevelList |
| View catalog | `configs/views` | ViewCatalog |
| Background catalog | `configs/backgrounds` | BackgroundCatalog |
| Cumulus presets 01–10 | `backgrounds/cumulus-01` through `backgrounds/cumulus-10` | Cubemap |
| Floor / Default | `views/floor` | GameObject prefab |
| Wall / Default | `views/wall` | GameObject prefab |
| Box / Default | `views/box` | GameObject prefab |
| Player / Default (Doctor) | `views/player` | GameObject prefab |
| Player / Alternate (Farmer) | `views/player_alt` | GameObject prefab |
| Player / Punk | `views/player_punk` | GameObject prefab |
| Goal / Default | `views/goal` | GameObject prefab |
| HintArrow / Default | `views/hint_arrow` | HintArrow GameObject prefab |
| Level index 0 | `levels/level-01` | TextAsset |
| Level index 1 | `levels/level-02` | TextAsset |
| Level index 2 | `levels/level-03` | TextAsset |

Author ViewCatalog with exactly one group per ViewKind. Entries and each group's Skins are lists. Each group contains Skins entries with Skin and Address, including exactly one Default and no duplicate skins. A missing requested skin falls back to Default. A configured skin whose address cannot load fails explicitly. To add a skin choice to cycling, also extend the contiguous SkinId enum; adding a catalog entry alone does not change the enum cycle.

LevelList.Levels is an ordered collection of serializable LevelConfig entries, each containing a string Address. Config assets use current serialized auto-property backing fields without migration attributes; save changes through the Editor. To add a level, follow the [level format](../Sokoban/Assets/Levels/LEVEL_FORMAT.md), mark its TextAsset Addressable, and append an entry with that address. StaticDataProvider.GetLevelConfig(index) returns the entry at a zero-based index; LevelCount exposes the count. An invalid index fails loudly. The first entry starts the session. File-based tests discover all `Assets/Levels/*.txt`; the runtime only visits entries in LevelList.

Config addresses identify the assets, while the **Configs** label supplies runtime discovery. Keep their group included in builds with both address and label inclusion enabled. Save config changes and rebuild content. No Resources fallback is used. GameplaySettings retains the default 60-second limit and 0.75-second completion delay; per-level overrides remain deferred.

Prefab meshes, materials, scale, rotation, and child offsets belong to the prefabs. The binder instantiates the root at the grid world position and installs EntityView adapters when present; the existing EntityView/TransformAdapter setup can remain. Save prefab changes before rebuilding Addressables. Renaming an address requires updating its catalog/list string.

The three animated player prefabs live under Assets/Addressables/Player and inherit PlayerBase. Keep PlayerAnimator and PlayerAnimatorAdapter on the wrapper root, assign the nested Animator to the facade, and include both TransformAdapter and PlayerAnimatorAdapter in EntityView's adapter list. All variants share the controller, push pose, and arm mask under Assets/AddressablesDependencies/Player/Animation. Disable root motion on the nested Animator. Idle and walk clips come from the imported zombie pack; author changes in the shared controller or project-owned push pose rather than modifying the source model prefabs.

### Board floor and wall surfaces

The shipped Floor and Wall prefabs use project-owned materials under `Assets/_Sokoban/Art/Board/Materials/`: `Floor_Slate.mat` (cool slate, base tint `#61777D`, smoothness 0.16) and `Wall_PuttyStone.mat` (warm putty stone, base tint `#A8A18B`, smoothness 0.14). Both use the installed **HDRP/Lit** shader, metallic 0, and no clear coat. Edit these saved materials rather than changing shared legacy materials under AddressablesDependencies/GameBoard.

Their shared `Textures/BoardTileSurface.png` is a neutral 256×256 color texture with a soft perimeter joint and very slight surface variation. It is imported as sRGB color with mipmaps, Repeat wrap, trilinear filtering and anisotropy 4. Both prefabs use the built-in unit Cube with 0–1 UVs on each face and unit Visual scale. Keep Base Color Map tiling at (1, 1), offset (0, 0): one texture tile corresponds to one face/cell, including the wall sides. Recheck the mapping before reusing the material on another mesh or scale. The joint is a painted surface detail; no bevel geometry, normal map or gameplay cell-size change is involved.

Only the `Visual` MeshRenderer material slot is assigned in each prefab. Preserve transforms, meshes, colliders and adapters when changing a surface. Goal remains a Floor prefab variant with one material override, using project-owned `Goal_Sage.mat`: HDRP/Lit, base tint `#78BDA4`, smoothness 0.16, metallic 0, no coat or emission. Its `Textures/GoalTargetSurface.png` retains the shared cell surface and perimeter joint, with a dark inset square painted into the color map (same 0–1 UV mapping and import settings). This gives empty goals a shape cue without additional geometry or coplanar overlays. Keep the target inside the joint and check it at gameplay distance when editing. The imported goal material is unchanged. Players and the hint arrow retain their existing artwork/materials. No BattlerPet material or sky asset is used for these surfaces.

The box prefab at `Assets/AddressablesDependencies/Box/Prefabs/Box.prefab` keeps its original wooden mesh/material and feedback tuning. Its `Visual/OccupiedGoalMarker` is an initially disabled square badge, referenced by `BoxGoalAnimator.occupiedMarker`. `Art/Board/BoxGoalBadge.asset` is a 16-vertex, eight-triangle ring, 0.34 m wide with a 0.045 m stroke at the existing Visual scale. Its center is mesh-local (0.0091, 2.214, -0.0002), about 13 mm above the central lid; it has no collider and casts no shadow. `Materials/BoxGoalBadge.mat` uses the same sage tint/roughness without a texture. The lifecycle system projects current goal occupancy onto this child; the arrival animation does not own its visibility. Keep the badge beneath Visual so it follows the hop/squash, and preserve the lid clearance if the artwork changes. These assets are ordinary dependencies of `views/box` and `views/goal`; save and rebuild Addressables, then verify **Use Existing Build**, after editing them. Do not register duplicate asset handles or change shared materials at runtime.

Save materials, texture import settings and prefab assignments, then rebuild Addressables with the Default Build Script before checking **Use Existing Build**. The materials and texture are ordinary prefab dependencies; do not add duplicate catalog entries or runtime material assignment code. Compare all three levels with the saved camera, lighting and resolution, including cell edges, wall tops/sides, goals, moving/pushing actors, undo, goal feedback and the hint arrow. Existing address validation checks catalog resolution; visual inspection still checks UVs and surface readability.

### Authored gameplay rendering

The 2026-10-28 visual pass is saved in GameplayScene, its existing `GameplaySceneVolume.asset`, and the project-owned board materials. The camera remains orthographic at size 5.6, position (11, 13, -5), rotation (48.96743, 315, 0); framing, geometry and cell dimensions are unchanged. Main Camera's HDAdditionalCameraData now enables **SMAA / High**. No runtime material/effect controller was added.

| Control | Authored value |
| --- | --- |
| Exposure | Fixed, EV 10.25, compensation 0; no automatic adaptation |
| Directional light | 20,000 lux, color `#FFF4E6`, rotation (55, 325, 0) |
| Directional shadows | Soft/PCSS through existing High filtering; angular diameter 1.5°, light resolution override 1024, normal bias 0.45, slope bias 0.4 |
| Scene shadow range | 35 m, one cascade for the fixed miniature-board camera |
| Ambient occlusion | Intensity 0.45, radius 0.35 m, direct-lighting strength 0.1; existing Medium quality, half resolution, six steps and temporal accumulation retained |
| Tonemapping | Explicit ACES scene override; existing full-ACES option remains unchanged |
| Bloom | Threshold 1.1, intensity 0.08, scatter 0.55, anamorphic off |
| Motion blur | Explicit intensity 0 and camera blur off |
| Depth of field | Explicit focus mode Off |
| Chromatic aberration / film grain / vignette | Explicit intensity 0 |
| Contact shadows | Explicitly disabled in the scene Volume and directional light |

The active quality is **High Fidelity**, using `Assets/Settings/HDRP High Fidelity.asset` (HDRP 17.0.4). Its SSAO support and High directional-shadow filter are already enabled. The camera inherits the default frame settings: AO, shadow maps, post-processing and custom passes are available. A frame-setting permission does not mean an effect runs: zero intensity or Off mode disables the corresponding volume effect. ACES, AO and bloom were already present through `Assets/Settings/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset`; this pass tunes explicit scene overrides and leaves that project-wide default profile and the HDRP quality assets unchanged.

Short eight-sample contact shadows (0.08 m, opacity 0.5) were compared on/off. The difference was negligible with the tuned shadow map and AO, so they remain disabled rather than adding an effect without a useful visual gain. The authored disabled Volume retains the comparison values. The single 1024 shadow cascade uses the same nominal texel count as the previous four 512 cascades, with a much shorter range. Recheck its coverage if the fixed camera or board sizes change in a later task.

Camera EV and background intensity are separate authored controls. Background presets retain Exposure 10; the slight backdrop dimming from camera EV 10.25 is intentional. The independent environmental sky remains DefaultHDRISky.exr at sky exposure 11/rotation 0. Changing presets does not change board illumination or reflections.

Compare saved content at 1920×1080 and 1440×1080 after rebuilding Addressables. The visual pass captured all three boards, all ten presets, Doctor/Farmer/Punk, movement/pushing, undo, goal feedback and the hint arrow. Matching before/after captures, evaluated settings, AA/contact-shadow comparisons and actual warmed Editor CPU/GPU timing JSON are under ignored `Sokoban/Logs/visual-tuning/`. Editor timing ranges overlap; these measurements do not establish a standalone-player performance gain. The validation used Windows/D3D12 on an RTX 4070, with no package or runtime C# changes.

### Background presets and lighting separation

The ten SimpleCumulus_Gradient variants are project-local cubemaps under `Assets/_Sokoban/Art/Backgrounds/Cubemaps/`. They were adapted from the read-only source `D:/Unity/BattlerPet/BattlerPet/Assets/Prefabs/Battlefield/Skybox`. Runtime and built content require no BattlerPet files. The source materials use legacy six-sided sky shaders and must not be assigned directly to HDRP.

Use **Tools > Sokoban > Convert legacy background presets** and choose the read-only source Skybox folder. The small Editor-only `LegacySkyboxConverter` resolves each material's actual PNG assignments by GUID and regenerates all ten existing project-local cubemaps in place. It preserves their GUIDs and Addressables registrations. For one preset, its public Editor authoring operation is `LegacySkyboxConverter.ConvertPreset(sourceMaterialPath, destinationAssetPath)`. No external path is compiled into runtime code. The old exploratory `Logs/sky-backgrounds/SkyAuthoring.cs` conversion delegates to this utility; use the menu for authoring.

| Source material property | Explicit CubemapFace | Texel correction |
| --- | --- | --- |
| Left / `_LeftTex` | PositiveX | Vertical flip |
| Right / `_RightTex` | NegativeX | Vertical flip |
| Up / `_UpTex` | PositiveY | Vertical flip |
| Down / `_DownTex` | NegativeY | Vertical flip |
| Front / `_FrontTex` | PositiveZ | Vertical flip |
| Back / `_BackTex` | NegativeZ | Vertical flip |

No face needs a horizontal flip or rotation. The material names describe interior skybox faces; do not map them by numeric enum ordering. The 2048×2048 PNGs are averaged in linear color in 2×2 blocks to 1024×1024, then stored as **sRGB** BC7 cubemaps with mipmaps, Clamp wrap and Trilinear filtering. Encoding already-sRGB texture values as linear data makes the sky too pale. The source materials' neutral tint, unit exposure and zero rotation are not baked into the texture conversion; the existing catalog controls background exposure/sample orientation. Native assets contain all imported data, with no external source dependencies. This utility targets the supplied legacy material serialization/PNG layout, rather than acting as a general material importer.

Run `LegacySkyboxConversionTests` to compare face identity and both UV axes against an isolated render of Unity's legacy six-sided shader. Its asymmetric face/UV ramps expose all rotations/flips, and the temporary reference-render pipeline settings are restored on failure as well as success. For new source content, inspect all twelve neighboring edges and compare source/converted views at equivalent directions before judging the gameplay composition. Current source-edge residuals occur at antialiased cloud silhouettes; no orientation seam was introduced. The conversion-only cubemap remains a full sky, independent of the gradient display decision.

Author the ordered **Presets** list on `BackgroundCatalog.asset`: **Name** is the debug label, **Address** resolves one Cubemap, **Rotation Degrees** rotates the palette sampling directions around Y, and **Exposure** is background intensity in EV (currently 10). Keep orientation and exposure finite. These values do not change camera exposure or environmental lighting. Catalog entries use strings, so loading Configs does not load all skies. Keep cubemaps in the included **Sky Backgrounds** Addressables group with **Pack Separately**, local build/load paths and catalog addresses enabled. Do not label cubemaps Configs or pack all ten into one bundle. Run address validation, save assets and rebuild content after changes.

The existing orthographic HDRP sky projection enlarged clouds near the widescreen edges even after conversion was corrected. The gameplay composition now intentionally shows a quiet gradient without individual cloud silhouettes. `BackgroundProjection.shader` samples the selected cubemap at four azimuths and two elevations using coarse mip 7, then blends the two averaged palette colors vertically. The normalized viewport gradient does not stretch cloud geometry when the aspect changes. Full HDRP sky/environment imagery is retained in the converted assets for separate conversion inspection.

BackgroundDisplay owns a runtime HDRP **FullScreenCustomPass**, material and camera-targeted **CustomPassVolume** on the assigned Background sky object. The pass runs at **BeforePreRefraction**, discards every non-background depth pixel and writes no depth. It does not redraw board geometry, allocate an offscreen texture or change the camera. Transparent content and post-processing render afterward. The scene's regular Background sky Volume (Default layer, priority 10) still has no saved profile; its owned runtime VisualEnvironment profile disables visible HDRISky. GameplayInstaller references the shader explicitly for build inclusion. Keep HDRP custom passes available in the existing pipeline/camera frame settings. The initial fallback is a neutral solid clear until a preset succeeds; cleanup disables drawing and clears the texture before its handle can be released.

The separate **Background lighting** global Volume (priority 100) uses the reserved **BackgroundLighting** layer 30 and saved `Art/Backgrounds/BackgroundLighting.asset`. All three HDRP quality assets select that layer in **Lighting > Sky > Sky Lighting Override Mask**. Keep the camera's visual Volume mask on Default, excluding this layer. This is HDRP's supported separation of visible sky from lighting/reflections. The lighting profile preserves the camera-evaluated baseline: package DefaultHDRISky.exr, Exposure intensity mode, sky exposure 11, rotation 0 and Dynamic ambient mode. The gameplay scene's fixed camera exposure is now 10.25, with the board rendering values listed above. Do not tune the lighting override when editing visible presets.

Initial entry, progression and development N choose randomly once per entered level; the same preset may be selected again. Manual restart, timeout restart, undo and skin changes retain the current selection, including a selection whose load is still pending. Finished's Play again enters level 1 anew. **Next background** uses the same application loading path and makes the preview the current selection; restart preserves it. Preview is disabled during level loading. Selection and randomness are presentation-only and do not affect recordings/replays.

Only the selected cubemap is requested. The last successful background stays visible while its replacement loads; obsolete handles are released after they stop being displayed. There is no cache of every visited preset. Superseded requests are cancelled/observed and cannot overwrite a newer selection or a new level. GameplayController owns BackgroundCoordinator, which owns pending/active resource operations and BackgroundDisplay; neither is registered for duplicate automatic disposal. Shutdown clears display references before releasing the active handle.

A background failure logs to the Console and reports Failed in the overlay, retaining the last valid sky (or the neutral fallback). It does not put the board in ViewsFailed or stop movement, hints or the countdown. Use **Retry background** to try that preset again; after success Next background resumes cycling. If the required background catalog itself is missing/duplicated, startup follows the existing config-failure/restart path.

## Verification

Run project EditMode assemblies, including Core parser/solver tests and Sokoban.Editor.Tests, then PlayMode assembly Sokoban.PlayModeTests in **Window > General > Test Runner**. These cover deterministic rules, saved-scene behavior, cache/fallback, cancellation, stale results, and failure recovery. Test counts and results must come from the run performed, not an old report.

Saved-scene tests use the real Addressables config batch and production installer. Provider lifecycle tests additionally exercise pending operations, cancellation, missing/duplicate configs, invalid values, retry, and handle release.

BackgroundLifecycleTests use controlled operations for persistence, level-entry invalidation, overlapping late success/failure, retry, cancellation-ignoring shutdown, release ordering, rendering-resource teardown and independent owners sharing a service. AddressablesServiceTests load every configured cubemap using actual built content. After changing backgrounds or rendering configuration, also preview all ten with Next background on each shipped level at 1920×1080 and another aspect ratio, then check movement, pushing, undo, goal feedback and hint display. Compare the same camera, resolution and board state with a fresh baseline; confirm camera exposure stays fixed at its authored value and the lighting override keeps the baseline HDRI across presets. Current visual-tuning evidence is in `Sokoban/Logs/visual-tuning/`; source/edge diagnostics remain under `sky-correction/`, and `sky-backgrounds/` contains the preceding infrastructure baseline.

Addressables service tests cover cancellation of one shared waiter, failed-operation propagation, owner release during loading, independent acquired references, built-content address/label loading, and level-text cleanup after parse failure or cancellation. The shared service does not cache or automatically release assets; the existing consumer owners retain those responsibilities.

After config loading completes, check the countdown through GameplayDebugOverlay or the debugger's GameplayController.RemainingSeconds. Config access before loading completes fails loudly:

- During initial view binding it stays at the full limit; it begins decreasing after Playing starts.
- During completion presentation it stays frozen; the next level receives the full limit.
- Solving on the last simulation tick wins; an unsolved timeout restarts the same run immediately with a full timer.
- Character selection and initial selected-player binding leave the full budget untouched; only Playing ticks the countdown.
- A view failure pauses simulation. Character failure keeps Retry/another choice available; board failure retains restart recovery.
- R and N start fresh budgets and preserve the selected skin.

For a content change, rebuild and check board-first startup with no player, selection of Doctor/Farmer/Punk, confirmed gameplay and movement/pushing, R preserving the confirmed character, N/progression reopening selection, and Play Mode exit. Confirm frames advance and inspect the Console. A still image alone does not prove that an unfocused Editor is ticking; see [Unity tooling](UNITY_TOOLING.md).

## Results and finish checklist

- A successful walk or push adds one move; a wall or blocked push adds none. The winning move is included.
- Timeout and ordinary R restart do not append results. They reset the current run count and budget.
- Finishing level 1 shows Completed for the configured delay, then loads level 2.
- Finishing the last level shows Finished, keeps the last board visible, and lists one row per recorded completion plus total moves/time. Time is the configured limit minus the frozen remaining time.
- In Finished, movement and N are ignored. R/Play again clears results, loads level 1 and opens character selection.
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
