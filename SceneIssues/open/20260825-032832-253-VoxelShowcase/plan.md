# Plan — reopened VoxelShowcase surface-transition issue

## Scope

Work only `SceneIssues/open/20260825-032832-253-VoxelShowcase` on `fixes/agent-4`. The earlier agent-4 closure was merged and explicitly reverted because the issue remained visible, so its prior fixes are evidence, not accepted resolution.

## Prior evidence carried forward

- Independent LOD rings can publish overlapping visible geometry. A previous render-staging ownership filter proved that overlap exists, but the exact saved-view replay still showed the reported coarse/striped patches.
- The analytic far terrain is not the source of the three marked regions; the saved view places them well inside its published hole.
- Changing serialized `m_DetailBandScale` from `0.6` to `1.0` moved the first handoff from 57.6 m to 96 m, but retained exact-replay evidence shows the same marked strips. Band distance alone is falsified.
- The previous hierarchy-ownership filter also failed to remove the marked strips. Do not revisit scheduler ownership as the next production change.

## Current root-cause question

`experiment-11.md` narrowed the remaining failure to geometry/material presentation that survives both ownership filtering and detail-band expansion. The next isolation target is the Transvoxel fine/coarse transition path itself: determine whether regular surface cells and transition cells overlap at the same boundary, or whether transition vertices carry discontinuous normals/material attributes that create the bright/dark serrated bands visible in the saved replay.

## Required isolation before another production change

The earlier investigation exceeded three implementation attempts, so first add a bare-bones deterministic EditMode reproduction around one transition boundary using production Transvoxel code. It should separate these invariants from scene streaming, camera/frustum selection, and unrelated materials:

1. Regular topology and transition topology must not publish two coplanar/near-coplanar surfaces for the same boundary region.
2. Transition vertices that represent the same surface as regular/fine neighbors must preserve compatible position/normal/material attributes.
3. The reproduction must fail on the current branch before any production behavior changes.

If the isolated boundary is already clean, falsify this hypothesis in a new experiment and move to the next surface-presentation cause rather than changing production code speculatively.

## Verification

- Establish the behavioral red regression first.
- Apply the smallest production fix tied to the reproduced invariant.
- Run the focused regression plus the repository fast suite through the prescribed targeted branch `ci-test/fixes/agent-4`.
- Replay `ShowcaseSceneIssue032832ReplayTests.SavedFixtureIsConfiguredForExactReplay` through targeted CI and inspect the saved 1364x836 standalone evidence for all three marked regions; do not trust the checker alone.
- Only if the exact replay is visually clean, record terminal `issue.json` fields, move the whole capture to `SceneIssues/closed/`, push the bookkeeping commit, and stop without starting another capture.
