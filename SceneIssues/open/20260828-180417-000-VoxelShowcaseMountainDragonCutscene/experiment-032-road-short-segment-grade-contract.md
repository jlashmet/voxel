# Experiment 032 — exact short-segment road grade contract

## Trigger

Exact-SHA run `33898431594` failed every Mountain Dragon route-dependent editor test and the standalone player with the same deterministic exception: the resolved ascent was rejected as `GradeExceeded` between route points 84 and 85 after the mountain presentation correction.

## Competing hypotheses

1. The revised mountain profile made the authored route intrinsically steeper than the CharacterMotor budget.
2. The road resolver's grading and final grade validator disagree at short integer-grid segments.
3. Standalone replay was exercising a different route or terrain representation than editor tests.

## Discriminator

Inspect the generic `WorldRoadResolver` contract and reproduce the integer boundary independently of Mountain Dragon composition.

At 280 permille, a 3dm horizontal run permits `floor(3 * 280 / 1000) = 0dm` integer rise. The resolver's `ClampGrade` and route-search recoverability calculation instead used `Math.Max(1, ...)`, allowing a 1dm rise. The final validator uses the exact inequality `rise * 1000 <= gradePermille * run`, so it correctly rejects that same 1dm/3dm segment as 333 permille. A 4dm run is the positive control: one decimeter is 250 permille and is valid.

## Required correction

Use one exact integer allowed-rise calculation for route-search recoverability and grading. Do not weaken the final validator, raise the Mountain Dragon grade budget, or add scene-specific exceptions. Add a reusable WorldBuilder regression covering both the 3dm/0dm and 4dm/1dm boundaries.

## Acceptance signal

- Generic 3dm/280-permille seam resolves by bounded cut/fill to zero rise.
- Generic 4dm/280-permille seam preserves a valid one-decimeter rise.
- Mountain Dragon route-dependent tests and standalone replay no longer fail with `GradeExceeded` at the short seam.
