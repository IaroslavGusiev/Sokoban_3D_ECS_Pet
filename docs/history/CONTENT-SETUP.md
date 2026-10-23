# Previous synchronous content setup

**Historical record.** Behavior, setup steps, pass counts, and pending proposals below belong to the recorded milestone. Use [current architecture](../ARCHITECTURE.md) and [current setup](../SETUP.md) for the implemented project.

These instructions describe the preceding synchronous stage. The Addressables migration supersedes its catalog/list field types and loading behavior. Use [ASYNC-CONTENT.md](ASYNC-CONTENT.md) for current setup, ownership, diagnostics, and verification. The prefab-variant authoring details below still apply.

The synchronous content refactor is implemented in C#. Scene, prefab, and ScriptableObject asset files were intentionally left for manual editor setup. Unity generated metadata for new code/text files; the removed hardcoded scripts and their metadata were deleted through AssetDatabase.

## 1. Create the two prefab variants

1. In the Project window, create a **Goal prefab variant** of `Assets/Addressables/Level/Floor.prefab` (for example, `Goal.prefab` beside Floor). Use the Prefab Variant action or drag an instance into the Project window and choose Prefab Variant.
2. Open Goal in Prefab Mode. Assign `Assets/AddressablesDependencies/GameBoard/Materials/Green_Cell_mat.mat` to the visual renderer. Preserve the root, visual offset, EntityView, and TransformAdapter.
3. Create a **Player prefab variant** of `Assets/Addressables/Level/Wall.prefab` (for example, `Player.prefab` beside Wall).
4. Open Player in Prefab Mode. Set the root scale to **(0.55, 1, 0.55)** and assign `Assets/AddressablesDependencies/GameBoard/Materials/Blue_Cell_mat.mat` to the visual renderer. Preserve the visual offset and both adapter components.
5. Save the variants in the Editor. Materials and scale now belong to prefabs; LevelView does not assign them.

## 2. Populate the view catalog

1. Select the existing `Assets/_Sokoban/Resources/LevelViewCatalog.asset` (or create one using **Create > Sokoban > View Catalog**).
2. Set **Entries** to five elements and fill:

   | Kind | Prefab |
   | --- | --- |
   | Floor | Existing Floor |
   | Wall | Existing Wall |
   | Box | `Assets/AddressablesDependencies/Box/Prefabs/Box.prefab` |
   | Player | New Player variant |
   | Goal | New Goal variant |

3. Open `Assets/_Sokoban/Scenes/GameplayScene.unity`. Select the GameObject carrying ServicesInstaller. Assign the completed catalog to **View Catalog** and confirm **Level Root** still points to the intended scene transform. C# migration attributes preserve the old `_viewCatalog` and `_levelRoot` field names.

The old five catalog fields are removed. Existing assets do not automatically convert their prefab/material fields into keyed entries. Until setup is complete, ServicesInstaller calls Validate and startup fails with a message listing every missing ViewKind. This failure is expected. Get also fails loudly for an unassigned kind.

## 3. Create and assign the level list

1. Choose **Create > Sokoban > Level List**. Save the asset in a project folder of your choice (for example, beside the existing catalog).
2. Set its **Levels** array to three elements. Drag in these imported TextAssets, in order:
   1. `Assets/Levels/level-01.txt`
   2. `Assets/Levels/level-02.txt`
   3. `Assets/Levels/level-03.txt`
3. Assign the new asset to ServicesInstaller's **Level List** field. Save the scene and assets in the Editor.

GameplayController loads index 0 exactly once in Initialize. R disposes the run (view first, simulation second) and creates a new run from the same LevelContent. It does not reload text or assets. Controller teardown releases the run before content. LevelContent.Dispose is currently a no-op and is the future owner of asset handles; this task introduces no async, Addressables, or progression.

## 4. Verify after setup

1. In **Window > General > Test Runner > EditMode**, run the Sokoban test assemblies, including `Sokoban.Core.Tests`. The three file cases automatically discover `Assets/Levels/*.txt`, print shortest solution lengths, and replay each solution through Simulation.
2. Under **PlayMode**, run `Sokoban.PlayModeTests`, including the existing `HardcodedLevelTests` scene fixture. Its historical name is retained; production hardcoded-level code is gone.
3. Play GameplayScene. Check arrows/WASD, wall blocking, box pushing, and R. The original level is unchanged; Left, Up, Down, Right, Right, Up solves it.

Before manual setup, all 152 EditMode cases and the 14 standalone PresentationPipelineTests passed. The temporary direct comparison with HardcodedLevel.Create passed before that class was deleted. The ten saved-scene cases remain pending the manual setup above; their previous pass results do not validate this new scene configuration.

See [LEVEL_FORMAT.md](../../Sokoban/Assets/Levels/LEVEL_FORMAT.md) for the exact parser contract and instructions for adding automatically tested levels. Shortest solutions for the supplied levels are 6, 12, and 11 moves respectively.

## Adapter finding and pending proposal

The current Floor, Wall, and Box prefab roots carry EntityView and a TransformAdapter. EntityView loops its serialized adapters and calls Install. ComponentAdapter rejects a duplicate component and calls entity.Set with its serialized value; TransformAdapter supplies only a TransformComponent referencing the view root. Reset initializes this serialized data in the Editor. No adapter creates/disposes a gameplay entity, retains an entity handle, or removes components when destroyed.

For these prefabs, `entity.Set(new TransformComponent(view))` in LevelView could replace the adapter installation path. That simplification remains a proposal awaiting confirmation. No adapter code or prefab components were removed, and the new variants should retain the existing setup for this task.
