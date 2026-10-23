# Unity Editor connection

Current tooling reference, checked against project configuration on 2026-10-23. Sokoban uses the official Unity CLI bridge, `com.unity.pipeline` (`0.8.0-exp.1`), with Unity `6000.0.63f1`. This is Editor tooling; gameplay assemblies do not depend on it. Start with [setup](SETUP.md) for gameplay and content authoring.

The local Codex MCP entry named `unity` launches the installed Unity CLI and is pinned to `D:/Unity/Sokoban_3D_ECS_Pet/Sokoban`. It is stored in the user's Codex configuration, outside this repository. Existing MCP entries are preserved. A client reload may be needed after adding the entry.

With the Sokoban Editor open, check the bridge from PowerShell:

```powershell
$unityCli = "$env:LOCALAPPDATA\Unity\bin\unity.exe"
& $unityCli status --project-path 'D:/Unity/Sokoban_3D_ECS_Pet/Sokoban' --format json
```

If installation is pending, focus the Unity Editor and allow package import and compilation to finish. Always specify the project path when more than one Editor may be running.

Check the installed CLI against the latest release before relying on version-specific commands:

```powershell
& $unityCli self-update --check --format json
```

Discover the connected Editor's command names and parameters instead of assuming that a command from an old report still exists. For automated Play Mode checks, enable the bridge's auto-tick support as needed, preserve and restore any temporary input/background settings, and confirm Time.frameCount advances. Then exercise the controls and inspect the Console before claiming success.

The [original connection report](history/UNITY-TOOLING-SETUP.md) records historical verification, machine configuration, and local evidence paths; it does not verify the present Editor session.
