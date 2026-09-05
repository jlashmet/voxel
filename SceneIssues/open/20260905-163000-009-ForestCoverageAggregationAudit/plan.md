# R09 — Forest coverage and production aggregation

Investigation only; 30–45 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

forest.webp shows dense continuous canopy across layered terrain. AggregatingFarFeaturePresentationAdapter exists but no production consumer was found in the initial audit; the previous 66-instance tableau does not establish production canopy coverage.

## Hypotheses and next experiment

H1: production supplies suitable vegetation tiers but handoff/selection creates sparse bands. H2: aggregation or far vegetation is absent/inadequate in the actual composition.

Trace one elevated production forest view from deterministic vegetation source through selection/tiering/batching. Inspect one ground-to-elevated or near-to-far capture sequence and identify the first distance band that loses canopy continuity. Inventory aggregation wiring; do not generate a new forest or increase density.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `Assets/VoxelEngine/Composition/FarFeaturePresentationSelection.cs`
- `Assets/VoxelEngine/Rendering/Runtime/Vegetation/ProceduralTreeRenderer.cs`
- `Assets/VoxelEngine/Rendering/Runtime/Vegetation/ProceduralVegetationBatchRenderer.cs`
- `Assets/Game/Composition/Showcase/SceneRuntime/ShowcaseFarFeatureRuntime.cs`
- `SceneIssues/rendering-review/references/forest.webp`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
