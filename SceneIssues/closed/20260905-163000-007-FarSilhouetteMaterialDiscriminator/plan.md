# R07 — Far proxy silhouette, openings and materials

Investigation only; 45–60 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

Far geometry omits carve primitives, maps annulus/arc-wedge to cylinders and other shapes to boxes; style keys name default Lit materials. bridge.webp requires readable arch openings and material continuity, not only conservative bounds.

## Hypotheses and next experiment

H1: these approximations are subpixel at actual handoff distances. H2: negative space, curvature or material loss remains conspicuous at production handoff.

Choose one production-generated arched structure. Inspect near and far realization immediately on each side of its actual handoff at identical FOV/resolution and comparable framing. Measure projected opening/silhouette loss and inspect materials. Do not author a substitute bridge, redesign all primitives or select an impostor system yet.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `Assets/VoxelEngine/Composition/FarFeaturePresentationSelection.cs`
- `Assets/VoxelEngine/Structures/Runtime/FeaturePresentationBaker.cs`
- `Assets/VoxelEngine/Rendering/Runtime/FarWorld/ProceduralFarFeatureRenderer.cs`
- `SceneIssues/rendering-review/references/bridge.webp`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
