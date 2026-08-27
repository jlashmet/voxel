# Plan

## Evidence and acceptance

- The capture has one native 1928×836 Game View pose and no circle annotations. Its note says the opening hands control back with the player on the pub roof; acceptance is a clean post-opening replay with the player inside the generated pub at its architecture-owned `InteriorApproach`.
- `KentridgePlayableSlice.ReleasePlayerForGameplay` preserves generated `InteriorApproach` X/Z and calls `CharacterMotor.SnapToGround` for Y. Pre-fix `SnapToGround` selected the highest occupied surface with no authored-Y bound, making a stacked pub roof eligible.

## Competing hypotheses / discriminator

- **Wrong pub route/XZ:** rejected; release derives X/Z directly from `_pubAccess.InteriorApproach`, and the behavioral regression preserves them.
- **Cutscene/controller residue raises the player:** rejected as initiating cause; `SnapToGround` assigns the bad elevation before normal gameplay resumes.
- **Highest-surface grounding crosses the roof:** selected and covered by the production-scene regression.
- **Replay tooling masks the fix:** confirmed only as an evidence problem. Earlier green replay stayed frozen through handoff; a later replay released correctly but exposed the F8 capture UI. The development verifier now uses `SceneIssueCapture`'s existing release transition at 85 s and disables only that development capture component afterward.

## Fix / regression

- `SnapToGround` now honors “below the given position”: ordinary columns retain the footprint fast path; stacked columns scan authoritative voxels downward from authored Y and preserve the old top-surface fallback only when nothing exists below.
- `KentridgeInteriorHandoffRegressionTests.OpeningRelease_StaysAtAuthoredInteriorElevationUnderPubRoof` loads the production scene, invokes production release, and requires authored X/Z plus Y within 0.5 m of `InteriorApproach`.
- Exact request `6805aba87c04caac16dd84df93246c688036ed6f` is green. Its real-player artifact is 1928×900, shows the player inside the pub looking through the doorway, and has neither replay nor F8 capture overlays. The player log confirms frozen-pose verification followed by replay release/capture-overlay disable at 85 s.

## Blast radius / cost / gates

- Gameplay cost is unchanged from the grounding fix: O(footprint) normally, bounded downward scan only on stacked explicit snaps. Replay release/overlay suppression is `DEVELOPMENT_BUILD`-only and opt-in via the existing command-line delay.
- Production/test fix commit remains `4aee470afe601a6ceb073a0e89229fff1aff8872`; evidence-tool source is `60a97a77e832260fee014f5e373323d6f01d20c8`.
- Remaining gate: commit the exact clean native-resolution `verification-final.png`, then fixed/closed bookkeeping and current-master merge. Do not substitute a degraded or pointer image if binary transfer is unavailable.
