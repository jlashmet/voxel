# R06 — Far-feature cache lifetime and stationary cost

Investigation only; 30–45 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

Production far-feature runtimes query and submit each frame. GeometryFor creates fresh geometry objects; RegisterGeometry invalidates meshes by reference identity. Cache dictionaries retain historical keys until renderer destruction.

## Hypotheses and next experiment

H1: unchanged production frames repeatedly rebuild identical meshes. H2: an effective cadence/cache elsewhere prevents that, leaving query allocations or retained historical keys as the dominant cost.

Trace one unchanged production far-feature query/submission for 300 frames, then a bounded move-away/return sequence. Record mesh identity/creation/destruction, geometry revisions, GC and retained key counts. Use actual production source and renderer; the tableau’s single SetInstances call cannot discriminate this.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `Assets/VoxelEngine/Composition/FarFeaturePresentationSelection.cs`
- `Assets/VoxelEngine/Rendering/Runtime/FarWorld/ProceduralFarFeatureRenderer.cs`
- `Assets/VoxelEngine/Structures/Runtime/FeaturePresentationManifest.cs`
- `Assets/Game/Composition/Showcase/SceneRuntime/ShowcaseFarFeatureRuntime.cs`
- `Assets/Game/Composition/Kentridge/Playable/SceneRuntime/KentridgeFarFeatureRuntime.cs`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
