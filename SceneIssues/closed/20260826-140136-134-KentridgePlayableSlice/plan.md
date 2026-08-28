# Plan

## Evidence and acceptance

- Original 1928×836 capture: gameplay resumes on the generated pub roof; acceptance is post-opening gameplay inside the pub at architecture-owned `InteriorApproach`.
- Release already preserves generated X/Z. The defect was Y grounding: `CharacterMotor.SnapToGround` could select a higher occupied roof surface in the same stacked column.

## Competing hypotheses / discriminator

- **Wrong pub route/XZ:** rejected; production release derives X/Z from `_pubAccess.InteriorApproach` and the regression preserves them.
- **Cutscene/controller residue raises the player:** rejected as initiating cause; grounding assigns the bad elevation before normal gameplay resumes.
- **Highest-surface grounding crosses the roof:** confirmed by source trace, production-scene regression, and saved-capture replay.
- **Replay tooling masks the fix:** evidence-only; CI replay now proves frozen pose first, then releases gameplay and disables the capture overlay.

## Fix / regression / verification

- `SnapToGround` honors “at or below the given position”; ordinary columns retain the footprint fast path, while stacked columns scan downward from authored Y and retain the old top-surface fallback only when nothing exists below.
- Regression: `KentridgeInteriorHandoffRegressionTests.OpeningRelease_StaysAtAuthoredInteriorElevationUnderPubRoof` invokes the production release path and requires authored X/Z plus Y within 0.5 m of `InteriorApproach`.
- Fix commit: `4aee470afe601a6ceb073a0e89229fff1aff8872`.
- Exact targeted request `6805aba87c04caac16dd84df93246c688036ed6f`, workflow run `33126743291`: **success**, 1/1 test passed.
- CI artifact was inspected directly. Its final 1928×900 real-player frame places the player inside the pub looking through the doorway; replay/F8 overlays are absent. `verification-replay.txt` records the durable evidence.

## Blast radius / cost / completion

- Gameplay cost remains O(footprint) normally; downward voxel scanning is bounded to stacked explicit snaps. Replay verification is development-only.
- Current master already contains the same production fix/regression; merge resolution keeps master's newer shared replay/capture tooling and applies only this issue's close bookkeeping.
- All gates complete; issue closed as fixed.
