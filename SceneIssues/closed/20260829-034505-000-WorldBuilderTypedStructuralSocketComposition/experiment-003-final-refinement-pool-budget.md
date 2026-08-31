# Experiment 003 — final-refinement pool budget

## Hypothesis
Run `33334360953` still exhausted the focused full-refinement fixture because the requested `262144` bricks were being clamped by the simple `ShowcaseWorld` constructor's default memory ceiling, not because production refinement intrinsically requires more than the gallery pool.

## Action / source
Inspected the run artifact and production construction path. The focused failure reported `BrickPool exhausted at capacity 127100`. `ShowcaseWorld(seed, capacity, ...)` defaults `maxMixedBrickAllocationBytes` to `VoxelEngineBootstrap.MaximumMixedBrickAllocationBytes` (256 MB), while the real gallery detects `DeviceTierBudget` and passes that tier's brick-pool byte budget through both capacity clamping and storage construction.

## Result
The 262144 request is deterministically reduced to 127100 by the 256 MB fallback. The exact player on the same source independently authored refinement and passed all three production `CharacterMotor` traversals plus negative contracts.

## Verdict
Confirmed fixture-construction mismatch, not a production memory-budget defect. The focused full-refinement regression now mirrors the gallery's device-tier budget path; planner-only tests remain at 4096 bricks. No global/device budget was raised.

## Next step
Exact-SHA CI must prove the focused class is green with the production-equivalent constructor path.
