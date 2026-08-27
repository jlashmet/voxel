# Experiment 001 — authored handoff height

**Hypothesis:** the roof spawn is caused by `CharacterMotor.SnapToGround` choosing the highest occupied surface in a stacked pub column instead of respecting the generated interior approach's authored Y.

**Action / source:** traced `ReleasePlayerForGameplay` from `_pubAccess.InteriorApproach` into `SnapToGround`, compared the pre-fix unbounded `OccupiedSurfaceHeight` selection with the production-scene regression, then replayed the original capture in the real player. Earlier evidence isolated a second, tooling-only problem: command-line replay remained frozen through handoff and later exposed the capture UI after release.

**Result:** X/Z already come from the generated pub approach; pre-fix grounding admitted roof occupancy above authored Y. Exact request `6805aba87c04caac16dd84df93246c688036ed6f` passed the focused production regression and the real-player capture. Its 1928×900 final frame shows the player inside the pub looking through the doorway with replay/F8 overlays absent. The log confirms frozen-pose verification followed by replay release and capture-overlay disable at 85 s.

**Verdict:** grounding hypothesis confirmed for the product defect. Replay freeze/UI were evidence-tool state only; the development verifier now reuses `ReleaseReplayCamera` and disables only the development capture component after release.

**Next:** commit the exact clean native-resolution final frame, then perform fixed/closed bookkeeping and merge current master.
