# Experiment 004 — prove and remove the piazza-internal primitive seam

## Hypothesis

The surviving light-blue crack is not the authored Market Square +Z boundary fixed by attempt 1. It lies inside the hard piazza surface, at an interface between the dark perimeter masonry and the lighter centre stone. `KentridgeMarketPiazzaCatalogue.PiazzaProgram` currently emits five exactly-touching fill boxes (four border bands plus one centre). If rasterization/meshing treats those adjacent primitive extents independently, their shared edge can expose an empty row even though the catalogue footprint is correct.

The smallest structural correction is to make occupancy continuous: emit one full-footprint FoundationStone slab first, then overlay the four DarkMasonry perimeter bands. This preserves the authored footprint, surface altitude, thickness, precedence, and visible material design while removing reliance on exact-touch ownership between independently emitted primitives.

## Baseline / source SHA

- Branch: `fixes/agent-2`
- Baseline commit: `94e0377f0d612f6e89e09272f86e780182f28006`
- Production-fix attempts before this experiment: **1/3**
- Attempt 1 (`+1` authored +X/+Z endpoints) is present in this baseline and its focused boundary regression is present, but the fresh exact-pose replay recorded in experiment 003 still shows the crack.

## Planned red regression

Add `KentridgeMarketPiazzaTests.HardPiazzaUsesContinuousBackingSlabUnderBorderBands`.

The test will inspect the emitted shape program and require the first primitive to be a FoundationStone `EmitBox` spanning the complete hard-piazza footprint. On the baseline implementation the first primitive is a DarkMasonry north/south border strip, so the test should fail before any production change.

## Production change if red is confirmed

Change only `KentridgeMarketPiazzaCatalogue.PiazzaProgram`:

1. emit one full-width/full-depth FoundationStone box;
2. emit the four existing DarkMasonry perimeter bands over it;
3. remove the separate centre-only stone box;
4. keep `MaxPrimitives = 5` and all placement/footprint/precedence values unchanged.

This counts as production attempt **2/3** only when the production behavior change is committed.

## Verification required

- targeted red CI for the new regression on `ci-test/fixes/agent-2`;
- production/test commit on `fixes/agent-2`;
- targeted green CI for the regression and relevant piazza test class;
- regenerate the VoxelShowcase artifact and replay the original saved camera/circles using the existing replay workflow, without creating a new SceneIssues capture;
- close the issue only if all marked seams are absent in the fresh replay and the terminal bookkeeping commit records the verified fix commit.

## Result

Pending.
