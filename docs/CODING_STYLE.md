# C# code style

These conventions apply to project-owned C# code. Follow them in new and changed code without reformatting unrelated code or vendored packages. Architectural boundaries and ownership decisions remain in [ARCHITECTURE.md](ARCHITECTURE.md).

## Layout and control flow

Use four spaces for indentation and opening braces on their own line. Use braces for control-flow bodies, including single-statement guards and loops.

Prefer explicit negative boolean conditions. Keep ordinary positive conditions concise.

```csharp
if (IsCurrent(run, phase) == false)
{
    Invalidate();
    return;
}

if (completion.IsCanceled)
{
    Invalidate();
    return;
}
```

Keep semicolons with their statements. Separate logical steps with blank lines, rather than placing several operations on one line. Group closely related assignments together; a blank line is not required after every statement.

Split long predicates at logical operators and align continuation lines consistently. Split fluent chains across lines when that makes their stages easier to read.

```csharp
return _disposed == false
       && phase == GameplayPhase.Playing
       && run is { CanHint: true }
       && _operation == null;
```

```csharp
return boxes
    .OrderBy(position => position.X)
    .ThenBy(position => position.Y)
    .ToArray();
```

Do not expand every short call chain or impose an arbitrary line-length limit. Preserve the surrounding style for method signatures and using directives.

## Members and naming

- Use `_camelCase` for private fields, `camelCase` for parameters, locals, and serialized fields, and `PascalCase` for types, methods, and properties.
- Mark fields readonly when their references or values do not change after initialization.
- Group injected dependencies, owned object references, and scalar state separately where those groups exist.
- Normally place fields first, then properties, constructors, public operations, private helpers, and nested implementation types. Keep interface implementations with the relevant operations even when their accessibility differs.
- Use expression-bodied members for simple forwarding or expressions, with the expression on the next line. Use block bodies for validation, orchestration, and multi-step logic.

```csharp
public void RequestHint() =>
    _hints.Request(_run, Phase);
```

Use `var` when the type is apparent from the expression; use an explicit type when it helps the reader. Do not perform broad conversions between the two styles.

## Responsibilities and comments

Name types and methods after what they actually do: factories construct, coordinators orchestrate, and async operation objects own or observe in-flight work. Introduce interfaces, wrappers, and composites only when they provide a concrete boundary, substitution point, or coherent operation.

Keep methods focused. Extract helpers when they clarify a distinct step, while keeping execution order and ownership visible. Avoid creating a separate type for every small helper.

Use comments to explain invariants, ordering requirements, ownership, or non-obvious choices. Do not narrate straightforward statements. Preserve correctness comments when reorganizing the code they describe.

## Scope of formatting changes

Match these conventions within the code being changed. Preserve Unity serialization identities and public contracts; a naming cleanup must not silently change serialized data or behavior. Treat unusual isolated formatting as something to assess, not automatically as a new convention.

There is no project-wide using-directive sort order or hard line-length limit established here. An `.editorconfig` can be added separately if mechanical formatting settings are agreed; this document does not require a repository-wide formatting pass.
