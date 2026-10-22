# Unity Editor connection

Sokoban uses the official Unity CLI bridge, `com.unity.pipeline` (`0.8.0-exp.1`), with Unity `6000.0.63f1`. This is Editor tooling; gameplay assemblies do not depend on it.

The local Codex MCP entry named `unity` launches the installed Unity CLI and is pinned to `D:/Unity/Sokoban_3D_ECS_Pet/Sokoban`. It is stored in the user's Codex configuration, outside this repository. Existing MCP entries are preserved. A client reload may be needed after adding the entry.

With the Sokoban Editor open, check the bridge from PowerShell:

```powershell
$unityCli = "$env:LOCALAPPDATA\Unity\bin\unity.exe"
& $unityCli status --project-path 'D:/Unity/Sokoban_3D_ECS_Pet/Sokoban' --format json
```

If installation is pending, focus the Unity Editor and allow package import and compilation to finish. Always specify the project path when more than one Editor may be running.

Verified on 2026-10-20 with CLI `1.0.0-beta.13`: the Editor reported `ready`; a read-only command returned the Sokoban project and `HardcodedTestScene`; an MCP initialization and tool-list request succeeded with 160 tools. The verification report is in the ignored `Sokoban/Logs/unity-mcp-verification.json`. No gameplay tests were rerun for this tooling-only change.
