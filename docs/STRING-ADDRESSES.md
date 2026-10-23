# String Addressables addresses

Implemented on 2026-10-23. All work remains uncommitted.

ViewCatalog.SkinEntry now stores Skin and string Address. KindEntry still groups skins by ViewKind. LevelList stores an ordered string array. ViewAssets loads GameObject by the resolved entry's Address; AddressableLevelSource loads TextAsset by the indexed level address. No address wrapper, generic loader, or legacy compatibility fields were added.

## Preserved behavior

ViewKey remains the presentation identity and resolved cache key. Missing Alternate entries resolve to Default and share its handle. A configured but broken Alternate fails explicitly. Cache sharing, failure-frame deduplication/retry, waiter cancellation, stale results, handle ownership/release, controller states, and Core behavior are unchanged. Successful cached views still survive K/R/N.

Runtime validation retains kind/skin/default checks and rejects null, empty, or whitespace addresses. It does not consult Editor APIs or perform extra asynchronous preflight loads.

Tools > Sokoban > Validate Addressable Addresses checks every ViewCatalog and LevelList against the Editor's Addressables settings. Each configured address must match exactly one asset, with the expected GameObject/TextAsset type, in a group included in builds. Bundled groups must include addresses in their catalogs. Folder entries are expanded through Addressables GetAllAssets. This is an explicit menu validator, not an automatic build/play hook; it does not verify the freshness of an existing content build. Editor code and its tests live in two new Editor-only assemblies; runtime assemblies have no new Editor dependencies.

## Migration

Before changing types, Unity saved the complete old serialization and each GUID-to-address mapping to ignored Logs/string-addresses/before.json. One ViewCatalog and one LevelList were found. After compilation, SerializedObject populated the new fields from that snapshot, followed by validation and AssetDatabase.SaveAssetIfDirty. Asset YAML and metadata were not hand-edited.

Both authoring asset GUIDs, all six view mappings, all existing Addressables names, and the three-level order were preserved. The player alternate already had the address views/player_alt at task start; that spelling was retained. No prefab or scene changes were needed. A post-test audit compared every mapping and both asset GUIDs with the snapshot.

| Content | Preserved address |
| --- | --- |
| Floor | views/floor |
| Wall | views/wall |
| Box | views/box |
| Player Default | views/player |
| Player Alternate | views/player_alt |
| Goal | views/goal |
| Levels in order | levels/level-01, levels/level-02, levels/level-03 |

## Verification

- Unity 6000.0.63f1 compilation succeeded with Addressables 4.1.1.
- Four new Editor tests passed: saved assets resolve, missing address diagnostics, ambiguous addresses, and wrong asset types.
- All 74 project PlayMode cases passed against Use Existing Build, including string serialization/order, catalog fallback, shared handles, cancellation, failure sharing/retry, stale binding, controller recovery, and saved-scene restart/unload. Expected failure logs are asserted by the tests.
- Addressables content rebuilt successfully. This is a content build, not a standalone player build.
- A separate running-scene check confirmed advancing frames and Playing. Synthetic K selected the orange Alternate; R retained Alternate with 76 views; N loaded level index 1 with 78 views and retained Alternate; K returned to the blue Default. No new errors occurred during this smoke check or exit.
- The temporary keyboard was removed and input/background settings were restored. Play Mode exited; GameplayScene remained clean.
- All 58 Core source/metadata hashes match task start. Core tests were not rerun because Core is unchanged.
- Focused whitespace checks passed. No commits or standalone player build were made.

Evidence is under ignored Sokoban/Logs/string-addresses: before.json, after.json, core-before.json, build.json, playmode.xml, and smoke-*.json.

## Authoring

Use exact Addressables addresses in SkinEntry.Address and LevelList.Levels. They are independent of asset paths. Keep addresses unique for these single-asset loads; renaming an address requires updating its configured string. Run the validation menu after editing, then rebuild Addressables content before testing with Use Existing Build. Saving prefab changes after a build still requires another content build.
