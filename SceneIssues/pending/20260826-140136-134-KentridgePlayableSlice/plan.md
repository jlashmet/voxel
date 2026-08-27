# Plan

## Evidence and acceptance

- The capture has one native 1928×836 Game View pose and no circle annotations. Its note says the opening hands control back with the player on the pub roof; acceptance is a clean post-opening replay with the player inside the generated pub at its architecture-owned `InteriorApproach`.
- `KentridgePlayableSlice.ReleasePlayerForGameplay` preserves generated `InteriorApproach` X/Z and calls `CharacterMotor.SnapToGround` for Y. Pre-fix `SnapToGround` selected the highest occupied surface with no authored-Y bound, making a stacked pub roof eligible.

## Competing hypotheses / discriminator

- **Wrong pub route/XZ:** rejected; release derives X/Z directly from `_pubAccess.InteriorApproach`, and the behavioral regression preserves them.
- **Cutscene/controller residue raises the player:** rejected as initiating cause; `SnapToGround` assigns the bad elevation before normal gameplay resumes.
- **Highest-surface grounding crosses the roof:** selected and covered by the production-scene regression.
- **Green CI artifact still proves closure:** rejected. Exact request `41740715ea52d62260492991c36fe7254b3bd8a6` passed the regression, but its 1928×900 final frame still showed the roof pose plus `Scene issue replay` overlay. Runtime evidence showed command-line `SceneIssueCapture` continued applying the frozen pose after the verifier succeeded.

## Fix / regression

- `SnapToGround` now honors “below the given position”: ordinary columns retain the footprint fast path; stacked columns scan authoritative voxels downward from authored Y and preserve the old top-surface fallback only when nothing exists below.
- `KentridgeInteriorHandoffRegressionTests.OpeningRelease_StaysAtAuthoredInteriorElevationUnderPubRoof` loads the production scene, invokes production release, and requires authored X/Z plus Y within 0.5 m of `InteriorApproach`.
- Replay proof now reuses `SceneIssueCapture`'s existing `ReleaseReplayCamera` transition when the real-player runner requests delayed release. Kentridge keeps the captured pose through line 27 at ~84 s, releases at 85 s, then lets the real opening handoff run before the ~94 s final frame.

## Blast radius / cost / gates

- Gameplay cost is unchanged from the grounding fix: O(footprint) normally, bounded downward scan only on stacked explicit snaps. Replay release logic is `DEVELOPMENT_BUILD`-only and opt-in via the existing command-line delay.
- The unrelated temporary `SceneIssueCameraReplayHarness` release workaround is reverted; no production camera behavior is changed by evidence tooling.
- Production/test fix commit remains `4aee470afe601a6ceb073a0e89229fff1aff8872`. Remaining gates: green exact-SHA targeted CI on the replay-evidence correction, clean native-resolution final replay, then close/merge bookkeeping.