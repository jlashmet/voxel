# Feature workflow

Use this workflow for acceptance-driven feature work that is tracked through `SceneIssues` but is not primarily a captured defect. Common queue, branch, CI, and promotion rules live in `SceneIssues/README.md`.

## Plan and task discipline

Before implementation, create and maintain separate `plan.md` and `tasks.md` files in the assigned folder.

`plan.md` should stay concise: acceptance criteria, current architecture/ownership, competing implementation hypotheses where useful, selected approach, blast-radius/cost expectations, current commit, and remaining gates.

`tasks.md` is the execution checklist. Work the next unchecked non-blocked item. If one item is blocked, record the blocker and continue independent work. Add discovered tasks only when required by acceptance, correctness/regression, reuse boundaries, or a demonstrated quality defect; do not expand the feature with opportunistic enhancements. Do not close while any required checkbox or acceptance criterion is incomplete.

If the same acceptance gate fails twice, stop speculative production changes and isolate a minimal reproduction/root-cause discriminator before another fix attempt.

## Architecture and reuse

Prefer the narrowest extension of the existing production path. Do not introduce a parallel authority when an existing canonical subsystem can own the behavior.

Keep shared APIs semantic and configuration-driven. Scene, place, named-content, evidence, or material-ID-specific policy belongs in composition/adapters rather than generic engine/runtime APIs. Avoid implementation contracts based on definition indices, instruction offsets, private-field reflection, magic IDs, or current ordering when a semantic contract can express the requirement.

For reusable work, identify affected consumers and prove reuse with at least one independent consumer/fixture when practical. Showcase/gallery/evidence code may demonstrate and measure the feature, but it must not become the authoritative implementation.

## Validation and cost

Add focused behavioral regressions through the production computation. Validate the real runtime/built application whenever the acceptance criteria involve scene behavior, rendering, traversal, interaction, or other player-visible behavior; editor/unit tests alone are supplemental.

Measure relevant blast radius and cost against repository budgets. Do not weaken global budgets, production tolerances, or unrelated behavior merely to make proof content pass.

For visual features, inspect durable built-player evidence directly and reject placeholder/blockout quality even when automated assertions are green. Reuse and mechanical correctness do not substitute for visual acceptance.

## Completion

Keep the assignment in `open` while implementation or required validation remains. Move to `pending` only when the implementation/checklist is complete and the workflow's pending metadata is ready. After green exact-SHA validation and all acceptance gates, complete final metadata and move `pending` to `closed` according to `SceneIssues/README.md`.