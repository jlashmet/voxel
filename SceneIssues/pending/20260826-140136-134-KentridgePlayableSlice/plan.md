# Plan

## Evidence and acceptance

- One 1928×836 Game View capture, no circles. The note says gameplay resumes on the pub roof; acceptance is a clean post-opening replay with the player inside the generated pub at architecture-owned `InteriorApproach`.
- `KentridgePlayableSlice.ReleasePlayerForGameplay` preserves generated `InteriorApproach` X/Z and calls `CharacterMotor.SnapToGround` for Y. Pre-fix grounding selected the highest occupied surface with no authored-Y bound, making the roof eligible.

## Competing hypotheses / discriminator

- **Wrong pub route/XZ:** rejected; release derives X/Z from `_pubAccess.InteriorApproach`, and the regression preserves them.
- **Cutscene/controller residue raises the player:** rejected as initiating cause; `SnapToGround` assigns the elevation before normal gameplay resumes.
- **Highest-surface grounding crosses the roof:** selected and covered by the production-scene regression.
- **Replay tooling masks the fix:** evidence-only. Earlier green replay stayed frozen through handoff; later replay released but exposed F8 UI. The verifier now uses `SceneIssueCapture`'s release transition at 85 s and disables only that development capture component afterward.

## Fix / regression / evidence

- `SnapToGround` now honors “below the given position”: ordinary columns retain the footprint fast path; stacked columns scan authoritative voxels downward from authored Y and preserve the old top-surface fallback only when nothing exists below.
- `KentridgeInteriorHandoffRegressionTests.OpeningRelease_StaysAtAuthoredInteriorElevationUnderPubRoof` loads the production scene, invokes production release, and requires authored X/Z plus Y within 0.5 m of `InteriorApproach`.
- Exact request `6805aba87c04caac16dd84df93246c688036ed6f` is green. Its real-player frame shows the player inside the pub and no replay/F8 overlay; the log confirms frozen-pose verification then release/overlay disable at 85 s.
- Current canonical evidence is `verification-final.jpg`, JPEG quality 40 at exactly 40% of the original capture: **771×334**. A compliant file has been produced from the clean replay and visually inspected.

## Blast radius / cost / gates

- Gameplay cost: O(footprint) normally; bounded downward scan only on stacked explicit snaps. Replay logic is `DEVELOPMENT_BUILD`-only and opt-in.
- Production/test fix commit: `4aee470afe601a6ceb073a0e89229fff1aff8872`; evidence-tool source: `60a97a77e832260fee014f5e373323d6f01d20c8`.
- Remaining gate: commit the compliant `verification-final.jpg`; then fixed/closed bookkeeping and current-master merge.