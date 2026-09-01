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

## Current discriminator / blocker

T007 focused `NaturalScatterVisibilityIndexTests` and automatic module validation passed in run `33496553811` on feature `50e0dab2a2e9740a8ce3c8440401f46f3f5812f4`, but the run failed afterward because the CI request incorrectly set `scene_issue: 20260831-032400-000-FarWorldVisibilitySemanticHlod`. `tests-single.yml` interprets any non-empty `scene_issue` as a standalone player replay; this feature assignment has no standalone SceneIssue replay contract. This is the same proven request-shape mistake previously isolated during T005, not a T007 product failure. Retry T007 from the exact feature SHA with `scene_issue` empty, using only `ci-test/fixes/agent-7`.

## Next independent work

After T007 exact green, mark T005-T007 evidence in `tasks.md`. T008 is a narrow renderer-contract migration: rename/generalize `FarStructureTier`, `FarStructureVisualFlags`, `FarStructureInstance`, and `IFarStructureRenderer` to generic feature vocabulary, migrate the existing procedural renderer/tests, and prove both T005 structure and T006 landform data fit the same render contract. Do not pull T009 selection policy or T010 generic geometry work forward.

## Remaining gates

T007 promotion -> T008 generic render contract -> generic selection/rendering/HLOD/readiness -> delete rejected structure/castle-specific paths -> terrain coverage/material/transition -> production-faithful module built-player validation -> visual/budget evidence -> final exact-head gates -> cleanup/docs/closure.
