# Experiment 004 — prove and remove the piazza-internal primitive seam

## Hypothesis

The surviving light-blue crack is not the authored Market Square +Z boundary fixed by attempt 1. It lies inside the hard piazza surface, at an interface between the dark perimeter masonry and the lighter centre stone. `KentridgeMarketPiazzaCatalogue.PiazzaProgram` currently emits five exactly-touching fill boxes (four border bands plus one centre). If rasterization/meshing treats those adjacent primitive extents independently, their shared edge can expose an empty row even though the catalogue footprint is correct.

The smallest structural correction is to make occupancy continuous: emit one full-footprint FoundationStone slab first, then overlay the four DarkMasonry perimeter bands. This preserves the authored footprint, surface altitude, thickness, precedence, and visible material design while removing reliance on exact-touch ownership between independently emitted primitives.

## Baseline / source SHA

- Branch: `fixes/agent-2`
- Baseline commit: `94e0377f0d612f6e89e09272f86e780182f28006`
- Production-fix attempts before this experiment: **1/3**
- Attempt 1 (`+1` authored +X/+Z endpoints) is present in this baseline and its focused boundary regression is present, but the fresh exact-pose replay recorded in experiment 003 still shows the crack.

## Regression and red evidence

Added `KentridgeMarketPiazzaTests.HardPiazzaUsesContinuousBackingSlabUnderBorderBands` in feature commit `f7906cd0143d489628d16d69d8e620c659d57a3b`.

The test requires the first piazza primitive to be a FoundationStone `EmitBox` spanning the complete hard-piazza footprint. Targeted CI run `32882314780` executed exactly one test and failed for the predicted reason:

- expected full depth `141`;
- actual first-primitive depth `5`.

This proved that the baseline program depended on independently touching perimeter/centre primitives rather than continuous backing occupancy.

## Production attempt 2

Production commit: `5a025ac0f668d786bb1399e11e967a716a33e858`.

Changed only `KentridgeMarketPiazzaCatalogue.PiazzaProgram`:

1. emit one full-width/full-depth FoundationStone box;
2. emit the four existing DarkMasonry perimeter bands over it;
3. remove the separate centre-only stone box;
4. keep `MaxPrimitives = 5` and all placement/footprint/precedence values unchanged.

Production-fix attempt: **2/3**.

## Structural verification

- Focused regression run `32882528039`: **pass**.
- Full `VoxelEngine.Tests.EditMode.KentridgeMarketPiazzaTests` run `32882857196`: **pass**.

The change therefore establishes the intended continuous-backing contract without breaking the existing authored-boundary, height, precedence, or shared-space tests.

## Fresh-bake replay verification

Verification ran from `ci-test/fixes/agent-2` using the existing assigned SceneIssue replay fixture; no new capture was created.

- Replay workflow run: `32883086329`.
- Workflow commit: `e6ea4de81aee96ddd74edc8846f62eb68ed728e9` (CI-branch-only replay wiring).
- Fresh VoxelShowcase bake: **pass**.
- Standalone saved-pose replay: **pass** (`Verified standalone frozen pose`).
- Evidence artifact: `scene-221508-unobscured-view`, artifact `9576720440`.

Manual inspection of the fresh `replay-latest.png` against the original screenshot shows the reported defect still present: the broad light-blue strip remains through the three lower marked regions, and light-blue exposure remains adjacent to the market-stall feet.

## Result

**Hypothesis disproven as the SceneIssue owner.** The hard piazza now has a stronger continuous-occupancy contract and all focused tests are green, but that change does not alter the captured visual seam. The visible strip is therefore not caused by exact-touch ownership between the piazza border and centre primitives.

Attempt 2 is unsuccessful for the SceneIssue and counts as **2/3**.

## Next

Do not make another piazza-geometry expansion or screenshot-coordinate patch. With only one production attempt remaining before the three-attempt reproduction rule, first inspect the actual fresh baked world at the saved seam row and identify whether the light-blue pixels correspond to empty density/material cells, a later writer, a region/chunk boundary, or renderer/mesher output despite occupied cells. Attempt 3 must target that proven final owner.
