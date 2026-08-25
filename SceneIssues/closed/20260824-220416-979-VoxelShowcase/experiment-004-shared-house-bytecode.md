# Experiment 004 — identify the live shared-house bytecode path

## Hypothesis

After the regression stopped requiring a concrete canopy `EmitBox`, the previous entrance-window reflow should pass because its reserved-span logic would keep both Medrare frontage windows clear of the entrance treatment.

## What was performed

Ran the focused Unity EditMode regression against `a064591e9a34602a40827906248c47eeee7a2d6e` on the self-hosted macOS runner. GitHub Actions run `32813218387`, job `97696461104`.

The test executed normally in Unity and reached the frontage-window count assertion.

## Result

**Failed.** The focused regression reported:

`Expected: 2`  
`But was: 0`

The runner completed the Unity invocation in about 63 seconds, so this was a deterministic assertion failure rather than a timeout or runner failure.

## What was learned

The failure exposed two incorrect assumptions in the preceding attempt:

1. The active VoxelShowcase generated-house path is `KentridgeSharedStructureVoxelCatalogue` → `KentridgeSharedHouseProgram` → `HouseProgramCompiler`, not the legacy `KentridgeGrammarVoxelCatalogue` path changed by production attempt 1.
2. `HouseProgramCompiler` represents facade doors and windows as front-wall `EmitBox` **carve** operations. The regression was still trying to identify frontage windows as filled glazing/detail boxes, so its zero-window result was measuring the wrong bytecode representation.

The earlier production reflow therefore could not affect the live house, and the test detector could not correctly observe the live window openings.

## Next

Move the invariant into the live `KentridgeSharedHouseProgram` adapter. Preserve the generic preset's window opening dimensions, translate Kentridge's architecture-owned `FrontageRhythm` into explicit window offsets, and choose deterministic legal offsets that keep at least 3 dm from the physical public door and neighboring frontage windows.

Update the regression to identify the actual front-wall carve operations, verify the door carve against the published door anchor, and rerun it using the repository-standard `ci-test/fixes` targeted-test mechanism.
