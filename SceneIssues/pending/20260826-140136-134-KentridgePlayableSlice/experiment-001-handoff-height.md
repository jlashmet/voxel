# Experiment 001 — authored handoff height

**Hypothesis:** the roof spawn is caused by `CharacterMotor.SnapToGround` choosing the highest occupied surface in a stacked pub column instead of respecting the generated interior approach's authored Y.

**Action / source:** inspected the captured note and production handoff at `4aee470afe601a6ceb073a0e89229fff1aff8872`; traced `ReleasePlayerForGameplay` from `_pubAccess.InteriorApproach` into `SnapToGround`, and compared the pre-fix unbounded `OccupiedSurfaceHeight` selection with the generated-scene behavioral regression.

**Result:** X/Z already come from the generated pub interior approach. The pre-fix grounding query had no Y ceiling, so roof occupancy above that approach was eligible and could replace the authored interior elevation. The regression exercises the production scene/handoff and fails if release rises away from the realized interior Y.

**Verdict:** hypothesis selected; route/camera-residue alternatives rejected as initiating causes. Fix grounding semantics at the shared contract boundary while retaining the ordinary-column fast path and no-below-surface fallback.

**Next:** exact-SHA PlayMode CI, then replay the original pose and capture native-resolution verification.
