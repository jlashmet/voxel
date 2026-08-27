# Experiment 001 — authored handoff height

**Hypothesis:** the roof spawn is caused by `CharacterMotor.SnapToGround` choosing the highest occupied surface in a stacked pub column instead of respecting the generated interior approach's authored Y.

**Action / source:** traced `ReleasePlayerForGameplay` from `_pubAccess.InteriorApproach` into `SnapToGround`, compared the pre-fix unbounded `OccupiedSurfaceHeight` selection with the production-scene regression, then inspected the exact green real-player artifact from request `41740715ea52d62260492991c36fe7254b3bd8a6`.

**Result:** X/Z already come from the generated pub approach; pre-fix grounding admitted roof occupancy above authored Y. The regression now passes. The green artifact still showed the roof and replay overlay at ~94 s; frame ~84 s was opening line 27 and frame ~94 s had gameplay control, proving the command-line replay freeze overlapped the actual handoff. `SceneIssueReplayVerification` only observed the frozen pose; `SceneIssueCapture.LateUpdate` remained the owner applying it.

**Verdict:** grounding hypothesis selected for the product defect. The remaining visual-evidence failure is replay-tool state, not a second gameplay cause. Keep the 85 s release point (between opening line 27 and gameplay control) and make the development verifier invoke the capture tool's existing `ReleaseReplayCamera` transition at that opt-in deadline.

**Next:** rerun the same focused production regression plus real-player replay on the exact new feature SHA; accept only a clean native-resolution post-opening frame inside the pub.