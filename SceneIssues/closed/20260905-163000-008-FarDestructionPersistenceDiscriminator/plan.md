# R08 — Far destruction and handoff persistence

Investigation only; 45–60 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

VoxelFarTerrain states runtime destruction is omitted. Showcase filters removed features and tags ruined ones; the far renderer does not visibly consume ruin state, and Kentridge uses a different adapter path. A distant height surface cannot alone encode visible caves/overhangs.

## Hypotheses and next experiment

H1: another canonical coarse representation already carries the edit across distance. H2: switching representations restores an intact silhouette or loses the opening.

Make one conspicuous opening in a production-authored structure/terrain surface through the canonical edit path. Retreat past near residency/handoff and return. Trace the same authoritative revision into semantic/fallback/far presentation. Choose one affected consumer; audit the other adapter statically, rather than adding a second runtime test.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `Assets/Game/Composition/Showcase/SceneRuntime/VoxelFarTerrain.cs`
- `Assets/Game/Composition/Showcase/SceneRuntime/ShowcaseFarFeatureStateAdapter.cs`
- `Assets/Game/Composition/Kentridge/Playable/SceneRuntime/KentridgeFarFeatureRuntime.cs`
- `Assets/VoxelEngine/Rendering/Runtime/FarWorld/ProceduralFarFeatureRenderer.cs`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
