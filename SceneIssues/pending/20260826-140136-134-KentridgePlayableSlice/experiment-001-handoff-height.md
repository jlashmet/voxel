# Experiment 001 — authored handoff height

**Hypothesis:** the roof spawn is caused by `CharacterMotor.SnapToGround` choosing the highest occupied surface in a stacked pub column instead of respecting the generated interior approach's authored Y.

**Action / source:** traced `ReleasePlayerForGameplay` from `_pubAccess.InteriorApproach` into `SnapToGround`, added a production-scene regression, then replayed the saved SceneIssue through the standalone player. Evidence tooling was discriminated separately from gameplay because early green runs stayed camera-frozen or retained the F8 overlay after the gameplay assertion had already passed.

**Result:** X/Z already come from the generated pub approach; pre-fix grounding admitted roof occupancy above authored Y. Exact request `6805aba87c04caac16dd84df93246c688036ed6f` passed the focused regression and clean real-player replay. The final frame visibly places the player inside the pub; replay and F8 overlays are absent. The log records frozen-pose verification followed by the real capture-tool release/disable at 85 s.

**Verdict:** highest-surface grounding is the product cause. Delayed replay release/overlay suppression is development-only evidence plumbing, not a second gameplay fix.

**Final evidence:** current canonical format is quality-40 JPEG at exactly 40% of the original 1928×836 capture. The inspected clean replay was converted to `verification-final.jpg` at 771×334.

**Next:** commit that exact JPEG, then fixed/closed bookkeeping and merge current master.