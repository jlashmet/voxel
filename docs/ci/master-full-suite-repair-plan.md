# Master full-suite repair

## Observed behavior / acceptance
Master full-suite run 33529708076 fails in the persistent EditMode phase with 44 failures (331 passed). Acceptance is a green `Tests (master — full suite)` run without weakening architecture or behavioral assertions.

## Competing hypotheses
1. A small number of architecture/reservation regressions fan out into most failures; fixing shared causes will collapse the failure set.
2. The 44 failures are mostly independent stale tests and require many local fixes.

## Discriminators / results
- Current and preceding master artifacts contain the same 44 failing test names, so the targeted-CI batching optimization did not introduce this failure set.
- Failures already existed on older master runs, including run #244 after WorldBuilder spatial reservation closure; older artifacts are expired, so exact historical failure comparison is unavailable.
- Proven architecture defects include a deleted `VoxelEngine.Core` asmdef reference and a deterministic storage source importing `UnityEngine` solely for logging.

## Selected repair
Repair proven shared causes first on `fixes/master-full-suite`, then run the smallest targeted EditMode evidence on the single assigned CI transport. Next isolate the Kentridge reservation conflict that fans out through grammar/catalogue tests, followed by remaining runtime-boundary and world-object lifecycle failures.

## Implemented
- Removed deleted `VoxelEngine.Core` reference from Kentridge playable composition.
- Removed UnityEngine logging dependency from deterministic `RegionResidencyStore` while preserving storage behavior/bookkeeping.

## Remaining gates
- Repair runtime/API boundary violations without exceptions.
- Root-cause Kentridge protected-corridor/site reservation conflict and related layout regressions.
- Repair WorldObject runtime lifecycle/tick regressions.
- Run targeted exact-SHA validation; then merge current master into repair branch and require a green full master suite before declaring repaired.
