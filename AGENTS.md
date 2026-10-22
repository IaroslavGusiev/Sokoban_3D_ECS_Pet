# Project purpose

This is an experimental Unity 3D Sokoban pet project for learning and validating software engineering architecture, not for maximizing gameplay features or visual polish.

Primary goals:
1. Build and test clean OOP application infrastructure around a deterministic, Unity-independent DefaultEcs gameplay core.
2. Design and test asynchronous Unity Addressables workflows with UniTask, including cancellation, ownership, stale results, and safe synchronization with ECS.

Tech stack (verify installed versions before using version-specific APIs): Unity, C#, DefaultEcs, Zenject, UniTask, Addressables, NUnit/Unity Test Framework.

Read `docs/ARCHITECTURE.md` when planning changes that touch architecture, ownership, ECS, or asynchronous lifecycles. Keep that document aligned with approved, implemented decisions; don't silently treat proposals as existing code.

# Architectural boundaries

- Gameplay domain is pure C#: no UnityEngine, MonoBehaviour, Addressables, Zenject, or UniTask dependencies inside gameplay components and systems.
- DefaultEcs owns simulation entities, components, and synchronous deterministic rules. Keep components small and data-focused; use value-type tags and positions where appropriate.
- OOP application/infrastructure owns dependency composition, session lifecycle, input orchestration, async services, presentation bridges, and resource cleanup.
- Async operations never directly mutate an obsolete or disposed ECS world. Route completed results through an explicitly owned application boundary, with session validity checks.
- Make cancellation, Addressables handle release, and failure behavior explicit. Do not use unobserved fire-and-forget tasks (`.Forget()`) as a default.
- Favor simple designs that demonstrate the learning goal; avoid speculative abstractions, premature optimization, and game features unrelated to the experiment.
- Design for isolated pure C# tests of movement, pushing, blocking, and victory conditions, plus targeted integration/lifecycle tests for async behavior.

# Working workflow

1. Read relevant code, configuration, docs, and available logs before proposing edits. Trace behavior, distinguish facts from assumptions, and state uncertainties.
2. Explain the cause or design problem, proposed approach, expected behavior, material risks, and verification plan. Wait for approval unless the user explicitly authorized implementation. If a materially different approach becomes necessary, seek approval again.
3. Keep implementation within the agreed scope. Run focused checks and report changes, verification results, and remaining limitations. Do not claim Unity tests/builds passed unless they actually ran.
4. Prefer focused edits and concise reporting. Use project docs for lasting decisions; avoid repeatedly expanding global instructions.

Investigation, analysis, review, and discussion are read-only unless the user authorizes changes. They do not authorize modifying code, scenes, prefabs, assets, project settings, or runtime state.

# Investigation routing

Use one agent by default for implementation and ordinary fixes. For direct investigation, research, diagnosis, problem-analysis, root-cause, or parallel-research requests, follow `.agents/skills/unity-investigation/SKILL.md`.

"One agent only" or "no subagents" overrides delegation. A quoted example or discussion of investigation rules does not authorize investigating that example. Code reading as part of an ordinary fix does not automatically trigger delegation.
