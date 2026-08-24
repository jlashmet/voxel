# Experiment 003 — named-structure occupancy overlaps

## Hypothesis

The Rebecca-house conflict is one symptom of a systematic missing reservation invariant between
stable named plots and later anonymous/secondary Kentridge urban stages.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus a temporary catalogue diagnostic,
evaluated final within-instance occupancy for all 17 named role structures and 73 anonymous fabric,
block access, vertical-frontage, gallery, terrace-dwelling, and retaining-gallery instances. For
every intersecting pair, iterated the integer intersection volume and counted cells occupied by both
instances after each instance's own fill/carve ordering. Ran
`VoxelEngine.Tests.EditMode.KentridgeGenerationTests.DiagnosticNamedStructureUrbanStageOccupancyOverlaps`
through `tools/unity-run.sh`.

## Result

The test passed 1/1 and found 28 real occupied-cell overlap pairs. Rebecca House alone overlaps:

- anonymous fabric 19 by 5,966 cells;
- anonymous fabric 20 by 8,222 cells;
- anonymous fabric 21 by 5,337 cells;
- upper-east block access by 1,040 cells; and
- a retaining gallery by 16,844 cells.

The same missing reservation affects the Inn, Pub, Church, Logan House, Sarah House, Katie House,
and Medrare House. Evidence is `verification-occupancy-overlaps-results.xml` and
`verification-occupancy-overlaps-unity.log`.

## What was learned

The hypothesis is confirmed. The defect is not one bad building form or renderer sample: secondary
urban lowering ignores the stable named plot envelopes and authors occupied structures through
them. Rebecca's higher-precedence shell then overwrites parts of the access stair and surrounding
fabric, which is why the stair appears to run backward into nothing.

## Next

Define one deterministic named-plot reservation policy using the settlement's declared envelopes
and 12 dm minimum spacing, apply it to secondary urban placement/lowering without changing stable
named plots or streets, and first encode the current 28-pair failure as a focused regression.
