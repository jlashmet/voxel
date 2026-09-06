# R10 — Far-world validation fidelity and closure review

Investigation only; 30–45 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

The closed FarWorldVisibilitySemanticHlod issue cites a module tableau with bespoke sinusoidal terrain, handmade feature shapes and one-time submission. Its reported whole-frame timing lacks CPU/GPU samples; it cannot by itself prove production query, handoff, destruction or scale behavior.

## Hypotheses and next experiment

H1: other existing exact-player evidence covers the real production paths and supports closure. H2: the claimed gates passed through substitutes and require a production-faithful validation consumer.

Trace the closed issue’s acceptance claims to actual scene composition, scenario execution and captures. Compare the module scene with production VoxelShowcase/Kentridge systems. Locate an existing compliant scene or the narrow reusable composition boundary needed; do not implement the replacement scene in this audit.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `SceneIssues/closed/20260831-032400-000-FarWorldVisibilitySemanticHlod`
- `Assets/VoxelEngine/Rendering/Validation/FarWorld/FarWorldRenderingValidationShowcase.cs`
- `Assets/VoxelEngine/Rendering/Validation/FarWorld/FarWorldBudgetProbe.cs`
- `Assets/VoxelEngine/Rendering/Validation/FarWorld/FarWorldVisibilityDemo.player-scenario.json`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
