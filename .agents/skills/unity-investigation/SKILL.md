---
name: unity-investigation
description: Read-only investigation of this Unity project's systems or root causes with adaptive delegation and evidence-based validation. Use for direct investigation or parallel research requests; honor single-agent overrides and exclude ordinary fixes and discussion of example prompts.
---

# Unity investigation

Follow the project's AGENTS.md approval and scope rules. Interpret the whole request: a direct request to understand why something happens counts as investigation; a quoted example or discussion of these rules does not authorize investigating the example. User instructions override this procedure. If the user requests one agent or no subagents, investigate in the main agent without spawning researchers or a validator.

## Scope and delegate

1. Define the investigation question, relevant boundaries, and what evidence would answer it. Identify missing information and separate confirmed facts from assumptions.
2. Choose the lowest sufficient investigation depth. Explicit Deep requests use Deep; explicit parallel requests use at least Standard, unless the user forbids subagents:
   - **Light — main agent only:** localized issues, straightforward flows, and questions that can be verified directly. Do not spawn subagents.
   - **Standard — main agent + one research subagent:** a clearly bounded, distinct research assignment would materially improve coverage, including across components. The main agent handles complementary work without duplicating the delegate's searches.
   - **Deep — two research subagents + independent validator:** complex cross-system issues, unresolved competing causes, conflicting evidence, difficult runtime behavior, or consequential uncertainty. Preserve complementary research assignments: one traces code, execution flow, and dependencies; the other examines logs, configuration, and alternative causes. Adapt assignments to the actual question.
3. Give each researcher a concise brief: objective, assigned question, relevant constraints and versions, starting references, scope, stopping condition, and expected output. Supply only necessary context rather than the entire conversation. Researchers form their initial conclusions independently.

Escalate when evidence is insufficient, contradictory, or important assumptions remain unresolved. Delegate only distinct objectives that address those gaps; file count alone does not justify escalation. Reuse established evidence and existing researchers when escalating instead of restarting completed research. If available evidence cannot resolve a gap, report it rather than spawning agents without an answerable assignment. Stop once sufficient evidence supports the conclusion; never lower evidence standards merely to save tokens.

## Research constraints and output

- Investigation is read-only: do not change code, scenes, prefabs, assets, project settings, or runtime state. Include this constraint in every delegated brief. Research does not authorize implementation.
- Keep searches focused. Avoid duplicate broad scans; overlap only when a specific claim needs independent checking.
- Return findings with file/line references or other evidence. Separate direct observations from causal inferences and hypotheses; identify contradictions, missing evidence, and what remains unverified. Check installed versions when relevant to API or package claims.
- Target 200–400 words per research report. Include excerpts only when necessary; expand for essential evidence or consequential uncertainty.
- Stop when the assigned question is answered or missing evidence prevents a conclusion. Return gaps to the main agent. Subagents must not launch additional agents; the main agent coordinates follow-up.

## Validate and report

For Light and Standard, the main agent checks important claims, the proposed cause, contradictions, and consequential uncertainties against original evidence, including delegated findings. Reading a researcher's summary alone is not validation.

For Deep, after research finishes, launch one separate validation subagent in a fresh context. Supply the question, applicable constraints, findings, and evidence references. It checks important claims, the proposed cause, contradictions, and consequential uncertainties against original evidence regardless of confidence labels. Agreement between researchers is not proof. Expand research only when these checks reveal a material gap.

The main agent presents one consolidated report: confirmed findings, cause or leading hypotheses, uncertainties, proposed fix or next step, material risks, and a verification plan. Distinguish static evidence from runtime verification. Continue through AGENTS.md's proposal and approval steps before implementation.
