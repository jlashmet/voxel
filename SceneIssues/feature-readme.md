# Feature workflow

Use for acceptance-driven feature work. Common queue, branch, CI, and merge rules are in `SceneIssues/README.md`.

## Plan and tasks

Before implementation, maintain separate `plan.md` and `tasks.md` files.

`plan.md`: acceptance, ownership/architecture, chosen approach, blast radius/cost, current commit, remaining gates.

`tasks.md`: execution checklist. Work the next unchecked non-blocked item. Record blockers and continue independent work. Add tasks only when required by acceptance, correctness/regression, reuse boundaries, or a demonstrated quality defect; no opportunistic enhancements. Do not close with any required checkbox or acceptance criterion incomplete.

For every affected module/assembly, identify its module root and owned validation surface before implementation. If the feature adds or changes player-visible/runtime behavior, `tasks.md` must include creation or update of a focused module-local test/validation scene under that module's own `<Module>/Validation/` directory. A top-level showcase, gallery, `VoxelShowcase`, `KentridgePlayableSlice`, or another module's scene does not satisfy this requirement; those are integration consumers only. Pure headless/domain modules with no meaningful scene behavior may use module-local EditMode/unit coverage instead, and `plan.md` must state why no validation scene applies.

If the same acceptance symptom or assertion fails after two materially different fixes, stop speculative changes and isolate a minimal repro/root cause before another fix.

If an external prerequisite is unavailable, record the blocker and continue independent work; do not substitute something that changes acceptance.

## Architecture and reuse

Prefer the narrowest extension of the existing production path. Do not create parallel authority when a canonical subsystem exists.

Keep shared APIs semantic/config-driven. Scene, place, named-content, evidence, and material-ID policy belongs in composition/adapters. Avoid contracts based on indices, offsets, private-field reflection, magic IDs, or incidental ordering.

Identify affected consumers and prove reuse with an independent consumer/fixture when practical. Do not refactor adjacent systems unless acceptance or a demonstrated defect requires it. Showcase/gallery/evidence code demonstrates or measures; it must not become authoritative.

## Validation and quality

Add focused behavioral regressions through production computation. Use the real runtime/built application for scene, rendering, traversal, interaction, or other player-visible acceptance; editor/unit tests are supplemental.

For player-visible/runtime modules, the first focused validation target is the owning module's scene under `<Module>/Validation/`. Keep that scene small and deterministic, but invoke the same production authoring, composition, rendering, materials, interaction, and runtime paths used by the shipped game. Pair the scene with a module-local `*.player-scenario.json` when actions, captures, timing, or runtime assertions are needed. CI discovers module-owned validation targets from repository structure; do not register them manually.

The module-local scene proves the module in isolation; it does not replace the repository-wide built-player integration gate or any feature-specific production-scene acceptance. Conversely, passing `KentridgePlayableSlice` or a showcase scene does not excuse a missing module-local scene when the changed module has player-visible/runtime behavior.

Measure relevant blast radius/cost against repository budgets. Do not weaken global budgets, tolerances, or unrelated behavior to pass proof content.

For visual work, inspect durable built-player evidence directly and reject placeholder/blockout quality even when automation is green. After a visual rejection, identify the failed visual relationship before broad geometry/material changes.

## Completion

Keep the assignment in `open/` until all implementation, validation, and acceptance work is complete. After required exact-SHA gates pass, close it according to `SceneIssues/README.md`.