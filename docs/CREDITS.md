# Credits and provenance

This records provenance available in the repository and installed package metadata. It is not a repository license or a complete dependency/license inventory. Third-party assets and code retain their own terms.

## Code and tools

| Dependency | Verified attribution and terms | Local evidence |
| --- | --- | --- |
| DefaultEcs 0.17.2 | Paillat Laszlo; MIT-0. | [NuGet metadata](../Sokoban/Assets/Packages/DefaultEcs.0.17.2/DefaultEcs.nuspec) |
| Zenject / Extenject 9.2.0 | Zenject license credits Modest Tree Media; MIT. The installed package identifies itself as Extenject. | [License](../Sokoban/Assets/Plugins/Zenject/LICENSE.txt), [package metadata](../Sokoban/Assets/Plugins/Zenject/package.json) |
| UniTask 2.5.11 | Cysharp, Inc.; MIT. | [Package metadata](../Sokoban/Assets/Plugins/UniTask/package.json) |
| PrimeTween 1.5.1 | Kyrylo Kuzyk; its own license terms, not an assumed MIT license. | Bundled `Packages/com.kyrylokuzyk.primetween.tgz`; resolved package `package.json` and `license.md`. [Manifest](../Sokoban/Packages/manifest.json) |
| NuGetForUnity | GlitchEnzo/NuGetForUnity; installed package metadata declares MIT. | Git dependency in the [manifest](../Sokoban/Packages/manifest.json) |
| Unity and Unity packages | Unity Editor, HDRP, Addressables, Input System, UI Toolkit and Unity Test Framework use their respective included terms. Tests use NUnit through the test framework. | [Editor version](../Sokoban/ProjectSettings/ProjectVersion.txt), [manifest](../Sokoban/Packages/manifest.json), [package lock](../Sokoban/Packages/packages-lock.json) |

The resolved PrimeTween license distinguishes binary/private use from redistribution of its source or tarball in derivative projects. This repository currently references a local tarball. This documentation pass does not change that packaging or establish permission for public redistribution; that requires a separate review before publishing the repository.

## Visual content

- **Characters and animation:** the existing Doctor, Farmer and Punk wrappers reuse zombie models/prefabs and idle/walk clips under `Assets/AddressablesDependencies/Player/`. The model importer metadata records `licenseType: Store`, but the available files do not identify a publisher, original listing or complete license. Attribution remains unresolved.
- **Wooden crate:** the mesh/material under `Assets/AddressablesDependencies/Box/` are reused content. The OBJ identifies a Blender export, not its author or license; attribution remains unresolved.
- **Backgrounds:** the ten `SimpleCumulus_Gradient` cubemaps were converted from legacy sky assets supplied through the separate BattlerPet project. That source-project path is recorded in [Setup](SETUP.md#background-presets-and-lighting-separation); it is provenance, not an author or license attribution. No original creator/license information was found in the supplied sky folder.
- **Project-specific work:** gameplay/application code, UI composition, board/goal/hint styling and the background conversion/projection implementation live in this project. Character portraits are Editor-rendered images of the reused models; converted skies and portraits do not establish ownership of their source artwork.

Do not treat these unresolved visual credits as permission to redistribute the source assets.

## README media

[Gameplay and replay GIF](media/level-one-gameplay-replay.gif) was captured on 2026-11-03 from the saved `GameplayScene` with the production controller ticking normally. Temporary automation outside Assets sent simulated Input System keyboard presses for the first level's six-move solution and a UI Toolkit `NavigationSubmitEvent` to the real Watch replay button. It did not write ECS state, tick the controller manually or change recording contents. Character confirmation/loading happened before capture; the continuous sequence shows gameplay, the winning push, about two seconds of completion controls, Watch replay and the full repeated solution.

The 1920×1080 moving-frame capture was resized to 1280×720 and encoded with a shared 256-color palette and light, frame-stable ordered dithering. The GIF contains 227 frames over 12.76 seconds (about 17.8 fps; variable 50–80 ms frame delays) and is 2,111,453 bytes. Captured timing is preserved with cumulative delays rounded to GIF centiseconds; there are no cuts or playback-speed changes. The repeat returns directly to the opening board rather than attempting a seamless loop.

Raw PNG frames, per-frame timing/phase data, temporary capture/encoding scripts and Editor restoration evidence remain under ignored `Sokoban/Logs/readme-replay-gif/`. Play Mode was stopped, temporary devices/input/background settings restored, and the saved scene left clean. The former static README image was removed from `docs/media/`; its original historical capture remains at `Sokoban/Logs/finishing-cleanup/smoke/02-playing.png`.
