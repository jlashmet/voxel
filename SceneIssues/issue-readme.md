# Scene issue workflow

Use for capture-driven defects. Common queue, branch, CI, and merge rules are in `SceneIssues/README.md`.

## Investigate

1. Inspect every capture, frame, annotation, and note. Treat marked regions as separate defects until evidence proves a shared owner.
2. Replay every pose and identify the responsible runtime object/profile/material/coordinate/geometry/ownership decision. Synthetic repro proves possibility, not causality.
3. Record at least two plausible hypotheses and run the smallest discriminating experiment; state what falsifies the leader.
4. If the same acceptance symptom or assertion fails after two materially different fixes, stop speculative changes and isolate a minimal repro/root cause before another fix.
5. Add a focused behavioral regression through production computation. For a player-visible/runtime owner, create or update a focused test/validation scene inside that owning module's `<Module>/Validation/` directory and exercise the real production path there. A top-level showcase, gallery, `VoxelShowcase`, `KentridgePlayableSlice`, or another module's scene is integration evidence and does not satisfy the module-local regression requirement. Pure headless/domain modules with no meaningful scene behavior may use module-local EditMode/unit coverage instead, with the reason recorded in `plan.md`. Source-string checks are supplemental only.
6. Implement the smallest proven fix. For shared systems, check affected consumers, likely regressions, and cost against budget.

If an external prerequisite is unavailable, record the blocker and continue independent work; do not substitute something that changes acceptance.

## Built-scene validation

Validate production changes in the actual built application/player for the affected scene; editor/unit/synthetic evidence is supplemental.

For player-visible/runtime changes, also validate the owning module's focused scene under `<Module>/Validation/`. Keep it isolated and deterministic while using the same production authoring, composition, rendering, materials, interaction, and runtime realization as the shipped game. Pair it with a module-local `*.player-scenario.json` when actions, captures, timing, or runtime assertions are required. CI discovers module-local validation from repository structure; do not manually register scene ownership.

The module-local scene is the focused regression target; the actual affected production scene remains the acceptance target. Passing one does not replace the other.

Replay every original pose and marked region. The scene must render and run without startup/runtime exceptions.

Visual fixes must meet the production art/layout bar: construction detail, proportions, material readability, support, placement, circulation, clearance, intersections, and framing. Reject placeholder quality, missing parts, floating props, unintended overlaps, or evidence that does not show the defect fixed. After visual rejection, identify the failed visual relationship before broad geometry/material changes.

Fix reusable generation at the semantic/constraint level. Keep scene-specific placement/presentation in composition. Do not refactor adjacent systems unless acceptance or a demonstrated defect requires it.

## Evidence

Keep `plan.md` concise: defect/acceptance, hypotheses, next discriminator, material results, selected fix, current commit, remaining gates.

Record substantial experiments as `experiment-NNN-<slug>.md`: hypothesis, action/source SHA, result, verdict, next step. Keep CI polling in `ci-operations.md` and durable textual evidence beside the issue.