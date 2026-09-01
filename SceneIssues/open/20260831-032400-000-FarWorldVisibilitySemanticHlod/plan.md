# Far-World Visibility Implementation Plan

## Acceptance

Deliver a reusable far-world system that keeps important never-visited features visible through 12 km without resident voxels, aggregates dense populations, culls ordinary scatter by projected significance, guarantees terrain coverage, preserves near/far continuity, and meets the device budgets. Required built-player evidence remains terrain views at ~0.5/1/3/6/10/12 km plus landmark views at 8/10/12 km across representative headings/snap phases.

## Architecture

Use one derived pipeline:

`canonical generation recipe -> generic far bake -> sparse visibility index -> significance/readiness -> generic renderer`

World generators remain authoritative. `FeaturePresentationBake` is derived presentation only. Normal generated castles, buildings, rocks/landforms, etc. require zero per-object far adapter/manifest/renderer registration. High-volume trees/rocks/shrubs stay with deterministic population queries; exceptional members promote automatically. Rendering is Game/WorldBuilder-agnostic; scene thresholds and named-content policy stay in composition.

## Validated state

- T002: canonical pre-residency generation representation established through `FeatureGeneration.EvaluateInstance` / `ShapeProgram.Evaluate`.
- T003: generic bake/catalogue lifecycle passed run `33473262150` on feature `303cb0b3e5e2b06405f23c1406676ee560b2344a`.
- T004: generic sparse `FeaturePresentationManifest` passed run `33475203893` on the same feature SHA.
- T005: never-visited planned Showcase castle enters the normal bake path before detailed residency; run `33490275502` passed on feature `c147864826f4a5e90b365548c526b4e2556f8a22`.
- T006: castle + independent production landform coexist through the same manifest with stable identity/revision/bounds and zero detailed-region generation; focused + automatic module validation passed on feature `50e0dab2a2e9740a8ce3c8440401f46f3f5812f4`.
- T007: ordinary scatter stays in population queries while exceptional members promote once into generic sparse presentation; run `33499579137` passed on CI child `ac18adb464d0bad07c1bba910f9ee7ae80e4de68`.
- T008: generic `FarFeatureInstance` / `IFarFeatureRenderer` contract and zero-persistent-object renderer are implemented; the stale `FarStructureInstance` test-surface migration was completed and its focused gate passed before T009 work.
- T009: `FarFeaturePresentationAdapter` plus projected-significance/hysteresis policy is wired generically. Exact run `33520315630` passed `FarFeaturePresentationSelectionTests` on CI child `c3d2c6c556df78f67d3f3480f72159efeec80a8e`, parent feature `39e03bc3f215a1d3d9eb70ef6504c6bce9ae7f19`.

## Current discriminator

T010 exposed a real architecture gap: `ProceduralFarFeatureRenderer` cached/batched correctly but collapsed every `GeometryKey` to one fallback cube, so unrelated baked silhouettes were lost. The selected fix keeps the Rendering API producer-neutral while adding immutable normalized `FarFeatureGeometry` primitives. `FarFeaturePresentationAdapter` converts the canonical bake primitive stream at the composition boundary; Rendering.Runtime generically builds cached conservative massing (including distinct cylinder massing) and retains the old cube only for legacy callers with no geometry payload. A regression sends unrelated structure-box and landform-cylinder bakes through the same adapter/renderer, asserts distinct meshes, cache reuse, stable batching inputs, and zero per-feature GameObjects.

Exact T010 request SHA `2fda448add86529f0d877ed29db7c9bc643935ff` (parent feature `7c3255bba7d6abbd680caa2336c2f020a279ee6d`) is queued as run `33528870152` for `VoxelEngine.Tests.EditMode.FarFeatureRenderingTests`. The self-hosted macOS job has not acquired a runner; do not replace it.

## Next independent work

If T010 passes, mark it validated and begin T011 by auditing the existing settlement cluster and forest-canopy aggregate owners for generic truth inputs, deterministic/hysteretic member handoff, landmark independence, and revision-scoped invalidation. If T010 fails, fix that proven cause before any new CI request.

## Remaining gates

T010 exact green -> T011 aggregate HLOD -> T012 readiness handoff -> remove rejected castle/parallel structure far authority -> terrain coverage/material/transition -> production-faithful module built-player validation -> visual/budget evidence -> final exact-head gates -> cleanup/docs/closure.
