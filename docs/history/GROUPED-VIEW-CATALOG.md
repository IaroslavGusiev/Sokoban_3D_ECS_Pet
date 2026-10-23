# Grouped ViewCatalog

**Historical record.** Behavior, setup steps, pass counts, and pending proposals below belong to the recorded milestone. Use [current architecture](../ARCHITECTURE.md) and [current setup](../SETUP.md) for the implemented project.

This report records the preceding grouping change. [STRING-ADDRESSES.md](STRING-ADDRESSES.md) describes the current string fields and migration that supersede the AssetReference representation below.

Implemented and verified on 2026-10-23, uncommitted.

ViewCatalog now contains serializable KindEntry classes with Kind and Skins fields. Each nested SkinEntry class contains Skin and Prefab. The Inspector groups Default/Alternate choices under a single kind instead of repeating the kind for every skin.

Resolve selects one kind group and returns its exact skin when present, otherwise its Default. ViewAssets constructs the resolved cache key from the requested kind and returned skin. ViewKey, cache ownership, failure-frame sharing, cancellation, ECS binding, and controller behavior are unchanged. An explicitly configured broken Alternate still fails rather than falling back.

Validation requires exactly one group for every ViewKind, unique SkinId values within each group, a Default skin in every group, and valid prefab references. These are external authoring-data rules; no lifecycle guards or compatibility state were added.

## Migration

Before changing the serialized shape, all ViewCatalog assets were discovered and validated through Unity. One was found: Assets/_Sokoban/Resources/LevelViewCatalog.asset, GUID eba0a2047ae674441a6a83abed2ce8a4, with five Default entries.

The full Editor serialization and each (Kind, Skin, serialized prefab reference) were saved under ignored Sokoban/Logs/grouped-view-catalog/before.json. After compilation, the snapshot records were grouped by Kind, applied with EditorJsonUtility.FromJsonOverwrite, validated, and saved with AssetDatabase.SaveAssetIfDirty. The asset was not hand-edited.

The saved catalog has five kind groups and five Default skins. Before saving and again after the PlayMode suite, the catalog GUID and all serialized prefab-reference data were compared with the snapshot and matched exactly. Migration and post-test checks are saved as migration.json and after.json in the same Logs folder. No references need reassignment. No permanent legacy fields or migration machinery were added to runtime code.

## Changed files

- Code/Unity/ViewCatalog.cs: KindEntry/SkinEntry classes, grouped resolution and validation.
- Code/Unity/ViewAssets.cs: use the returned SkinEntry and requested kind for cache keys.
- Tests/PlayMode/ViewBindingTests.cs: grouped fixtures, exact/fallback and nested serialization assertions, existing cache regressions retained.
- Tests/PlayMode/PresentationPipelineTests.cs: grouped authoring-validation fixture.
- Tests/PlayMode/HardcodedLevelTests.cs: deep-copy nested groups for temporary catalog mutations so original class entries are restored intact.
- Resources/LevelViewCatalog.asset: migrated through Unity.
- docs/ARCHITECTURE.md and this report.

Code, tests, and Resources paths above are under Sokoban/Assets/_Sokoban. No Core, scene, prefab, package, asmdef, or metadata changes were made. No files were deleted.

## Verification

- Unity compilation succeeded.
- All 73 project PlayMode cases passed after migration and after extending nested-skin serialization coverage. This includes saved-scene startup/restart/unload cases, exact/default resolution, shared fallback handles, failure retry, cancellation, and skin recovery.
- The catalog test round-trips a kind containing both Default and Alternate through EditorJsonUtility and verifies both prefab GUIDs, the group count, and fallback resolution.
- Post-test migration audit confirms five preserved references and the unchanged catalog GUID.
- All 58 Core source/metadata hashes match the task-start snapshot. Core tests were not rerun because Core is unchanged.
- Focused source whitespace checks passed. No standalone player build or additional manual scene smoke check was run.

No commits were made.

## Authoring

Select LevelViewCatalog, expand the Player kind group, and add Alternate inside its Skins array. Assign the Addressable prefab there. Keep one Default skin in each kind group and avoid duplicate kind groups or skin IDs. Rebuild Addressables content when the selected play/build workflow requires it after changing prefab content or references.
