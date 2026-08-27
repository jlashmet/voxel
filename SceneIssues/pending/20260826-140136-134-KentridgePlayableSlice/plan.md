# Plan

## Evidence and acceptance

- The capture has one native 1928×836 Game View pose and no circle annotations. Its note says the opening cutscene hands control back with the player on the pub roof; acceptance is to hand control back inside the generated pub at its architecture-owned `InteriorApproach`.
- `KentridgePlayableSlice.ReleasePlayerForGameplay` already preserves the generated pub's authored `InteriorApproach` X/Z and calls `CharacterMotor.SnapToGround` for Y.
- Before the fix, `SnapToGround` chose `OccupiedSurfaceHeight` across the whole player footprint with no authored-Y bound. In a stacked building column that can select roof occupancy above the interior approach even though the method's contract says “surface below the given position.”

## Competing hypotheses / discriminator

- **Wrong pub route/XZ:** rejected because release derives X/Z directly from generated `_pubAccess.InteriorApproach`; the regression preserves those coordinates.
- **Cutscene camera/controller residue pushes the player upward:** rejected as initiating cause because the motor position is assigned by `SnapToGround` before normal gameplay movement resumes.
- **Highest-surface grounding crosses a stacked roof:** selected. Falsifier: if production release remains near the authored interior Y while using the generated pub and the unbounded highest-surface query, this diagnosis is wrong.

## Fix / regression

- Make `SnapToGround` honor its existing “below the given position” contract: retain the footprint fast path when each column top is already below the authored Y; only stacked columns scan downward through authoritative voxel storage to the first occupied surface at/below that Y. Preserve the old highest-surface fallback when nothing exists below the authored point.
- PlayMode regression `KentridgeInteriorHandoffRegressionTests.OpeningRelease_StaysAtAuthoredInteriorElevationUnderPubRoof` loads the production scene, reads realized `InteriorApproach`, invokes the production release, and asserts X/Z stay authored and Y remains within 0.5 m of the interior elevation.

## Blast radius / cost / gates

- Shared motor behavior changes only for stacked columns whose top lies above the caller's authored Y; ordinary terrain keeps the existing footprint query. Fallback semantics are retained for malformed/no-below-surface inputs.
- Cost remains O(footprint) normally; stacked columns add a bounded downward scan only on explicit snap calls, not per frame.
- Production/test source state: `4aee470afe601a6ceb073a0e89229fff1aff8872`. Remaining gates: green exact-SHA targeted CI, native-resolution replay evidence, then close/merge bookkeeping.
