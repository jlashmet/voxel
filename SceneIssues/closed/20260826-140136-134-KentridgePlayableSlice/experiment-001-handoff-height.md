# Experiment 001 — handoff height

**Hypothesis:** the roof spawn is caused by `CharacterMotor.SnapToGround` choosing the highest occupied surface in a stacked pub column instead of respecting the generated interior approach's authored Y.

**Action / source:** traced `ReleasePlayerForGameplay` from `_pubAccess.InteriorApproach` into `SnapToGround`, added a production-scene regression, and replayed the saved SceneIssue through the standalone player. Replay freeze/overlay behavior was discriminated separately as development-only evidence plumbing.

**Result:** X/Z already come from the generated pub approach; pre-fix grounding admitted roof occupancy above authored Y. Fix commit `4aee470afe601a6ceb073a0e89229fff1aff8872` bounds stacked-column grounding to occupied surfaces at or below authored Y. Exact targeted request `6805aba87c04caac16dd84df93246c688036ed6f` passed in workflow run `33126743291` (1/1 regression test). The inspected final real-player frame is 1928×900 and places the player inside the pub looking outward through the doorway; replay/F8 overlays are absent. The log records frozen-pose verification and release/capture-overlay disable at 85 s.

**Verdict:** highest-surface grounding was the product cause. The saved capture now passes behaviorally and visually. Shared replay/capture tooling on current master is newer than this branch's evidence-only tooling, so the merge keeps master's versions.

**Next:** none; close as fixed.
