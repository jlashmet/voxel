# Scene issue workflow

Use this workflow for capture-driven defects in an existing scene. Common queue, branch, CI, and promotion rules live in `SceneIssues/README.md`.

## Investigate the captured defect

1. Inspect every screenshot, frame, annotation, and note directly. Treat marked regions as separate defects until evidence proves a shared owner.
2. Replay every recorded pose and identify the responsible runtime object, profile/material, coordinates, triangle/voxel, or ownership decision. A synthetic repro proves possibility, not causality.
3. Record at least two plausible hypotheses and run the smallest discriminating experiment. State what would falsify the leading hypothesis.
4. If the same acceptance gate fails twice, stop speculative production changes and isolate the behavior in a minimal reproduction/root-cause discriminator before another fix attempt. Remove temporary wiring before promotion.
5. Add a focused behavioral regression through the production computation. Source-string checks may supplement it but cannot be the sole rendering, geometry, or performance regression.
6. Implement the smallest proven fix. For shared systems, identify affected consumers, test likely negative regressions, and quantify cost against an existing budget.

## Built-scene validation

Validate every production change with the repository scene harness that builds the actual application/player and launches the exact affected scene. EditMode tests, editor-only PlayMode tests, unit tests, and synthetic repros are supplemental evidence only.

Replay every original pose and marked region in the built application after the fix. The scene must reach a usable rendered state without startup/runtime exceptions.

Visual fixes must meet the repository's production art/layout bar, not merely add the named primitive types. Inspect construction detail, proportions, material readability, physical support, useful placement, circulation, clearance, intersections, and framing. Reject placeholder-quality assemblies, missing structural parts, floating props, unintended overlaps, or evidence that does not actually show the defect fixed.

Fix reusable generation at the semantic/constraint level rather than hard-coding capture coordinates. Scene-specific placement and presentation belong in composition; shared systems should express reusable intent and constraints.

## Evidence discipline

Keep `plan.md` concise: observed defect/acceptance, competing hypotheses, next discriminator, material results, selected fix, current commit, and remaining gates. Replace stale detail with a short conclusion rather than growing a diary.

Record substantial experiments as `experiment-NNN-<slug>.md` with hypothesis, action/source SHA, result, verdict, and next step. Keep CI polling/runner notes in `ci-operations.md`; store durable textual evidence beside the issue.