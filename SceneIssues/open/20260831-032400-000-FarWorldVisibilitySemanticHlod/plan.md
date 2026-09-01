# Far-World Visibility Implementation Plan

## Acceptance

Deliver a reusable far-world system that keeps important never-visited features visible through 12 km without resident voxels, aggregates dense populations, culls ordinary scatter by projected significance, guarantees terrain coverage, preserves near/far continuity, and meets the device budgets. Required built-player evidence remains terrain views at ~0.5/1/3/6/10/12 km plus landmark views at 8/10/12 km across representative headings/snap phases.

## Architecture

Use one derived pipeline:

`canonical generation recipe -> generic far bake -> sparse visibility index -> significance/readiness -> generic renderer`

World generators remain authoritative. `FeaturePresentationBake` is derived presentation only. Normal generated castles, buildings, rocks/landforms, etc. require zero per-object far adapter/manifest/renderer registration. High-volume trees/rocks/shrubs stay with deterministic population queries; exceptional members promote automatically. Rendering is Game/WorldBuilder-agnostic; scene thresholds and named-content policy stay in composition.

## Validated state

- T002: `FeatureGeneration.EvaluateInstance` / `ShapeProgram.Evaluate` is the canonical pre-residency representation used by unrelated production structure and landform generators.
- T003: generic bake/catalogue lifecycle validated by run `33473262150` on feature `303cb0b3e5e2b06405f23c1406676ee560b2344a`.
- T004: generic sparse `FeaturePresentationManifest` validated by run `33475203893` on the same feature SHA.
- T005: planned Showcase castle enters the normal bake path before detailed residency; RNG zero-seed production defect fixed. Run `33490275502` passed on feature `c147864826f4a5e90b365548c526b4e2556f8a22`.
- T006: castle + independent production mountain/landform coexist through the same manifest with stable identities/revisions/bounds and zero detailed-region generation. Focused + automatic module validation passed on feature `50e0dab2a2e9740a8ce3c8440401f46f3f5812f4`.
- T007: ordinary scatter stays in deterministic population queries while exceptional members promote once into the generic sparse presentation path. The corrected request with empty `scene_issue` passed exactly in run `33499579137` on CI child `ac18adb464d0bad07c1bba910f9ee7ae80e4de68`.

## Current discriminator / blocker

T008 is implemented narrowly on the feature branch: Rendering API exposes `FarFeatureTier`, `FarFeatureVisualFlags`, `FarFeatureInstance`, and `IFarFeatureRenderer`; Showcase adapts its game-owned structure selection into that generic contract; `ProceduralFarFeatureRenderer` treats geometry/style keys as opaque and keeps zero persistent per-feature GameObjects.

Two materially different T008 compile fixes exposed the same acceptance symptom: the generic render contract replaced/deleted `FarStructureInstance`, but branch-local regression files created earlier in this assignment were not migrated atomically. Run `33508370189` failed on three stale `ShowcaseFarStructureSourceTests` helper signatures; feature `4d8e02128f3c8ce804da941edd3ec2bb20572818` migrated that file. Exact rerun `33509860849` then failed only on `SettlementFarHlodTests` lines 38/50/54 with the same missing `FarStructureInstance` symbol. The compiler reported no remaining `FarStructureInstance` errors in other files. This is now the isolated root cause/minimal repro required by the two-failure rule: incomplete test-surface migration after deleting the legacy render type, not a renderer behavior defect.

## Next independent work

Migrate `SettlementFarHlodTests` output-side assertions from `FarStructureInstance`/`ProxyKey`/render-tier expectations to `FarFeatureInstance`/`GeometryKey`/`FarFeatureTier`, while preserving `FarStructureTier` only where it remains the scene-owned selection-policy input. Then rerun the same T008 focused exact-SHA gate. Do not pull T009 projected-significance policy or T010 baked-geometry implementation forward until T008 is exact green.

## Remaining gates

T008 generic render contract -> generic selection/rendering/HLOD/readiness -> delete rejected structure/castle-specific paths -> terrain coverage/material/transition -> production-faithful module built-player validation -> visual/budget evidence -> final exact-head gates -> cleanup/docs/closure.
