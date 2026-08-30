# Feature workflow

Use for acceptance-driven feature work. Common queue, branch, CI, and merge rules are in `SceneIssues/README.md`.

## Plan and tasks

Before implementation, maintain separate `plan.md` and `tasks.md` files.

`plan.md`: acceptance, ownership/architecture, chosen approach, blast radius/cost, current commit, remaining gates.

`tasks.md`: execution checklist. Work the next unchecked non-blocked item. Record blockers and continue independent work. Add tasks only when required by acceptance, correctness/regression, reuse boundaries, or a demonstrated quality defect; no opportunistic enhancements. Do not close with any required checkbox or acceptance criterion incomplete.

If the same acceptance symptom or assertion fails after two materially different fixes, stop speculative changes and isolate a minimal repro/root cause before another fix.

If an external prerequisite is unavailable, record the blocker and continue independent work; do not substitute something that changes acceptance.

## Architecture and reuse

Prefer the narrowest extension of the existing production path. Do not create parallel authority when a canonical subsystem exists.

Keep shared APIs semantic/config-driven. Scene, place, named-content, evidence, and material-ID policy belongs in composition/adapters. Avoid contracts based on indices, offsets, private-field reflection, magic IDs, or incidental ordering.

Identify affected consumers and prove reuse with an independent consumer/fixture when practical. Do not refactor adjacent systems unless acceptance or a demonstrated defect requires it. Showcase/gallery/evidence code demonstrates or measures; it must not become authoritative.

## Validation and quality

Add focused behavioral regressions through production computation. Use the real runtime/built application for scene, rendering, traversal, interaction, or other player-visible acceptance; editor/unit tests are supplemental.

Measure relevant blast radius/cost against repository budgets. Do not weaken global budgets, tolerances, or unrelated behavior to pass proof content.

For visual work, inspect durable built-player evidence directly and reject placeholder/blockout quality even when automation is green. After a visual rejection, identify the failed visual relationship before broad geometry/material changes.

## Completion

Keep the assignment in `open/` until all implementation, validation, and acceptance work is complete. After required exact-SHA gates pass, close it according to `SceneIssues/README.md`.