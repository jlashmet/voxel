# Experiment 003 — Planar foot versus smoothed terrain seam

**Hypothesis.** The residual float is not lamp smoothing: the exact lamp foot begins in the first empty voxel above a Smooth terrace, whose reconstructed top can retract below that occupancy boundary.

**Action / source.** Inspected the fresh saved-pose replay from exact request `d453a2c8f095d027488121fb255afaa65d71e194`, source `b8a26fce06967699f89ad2f8788ec6e17b8c53dd`, run `33029508745`. That source already makes the ground-contact cylinder `SurfaceStyles.Planar` and the behavioral test proves its minimum Y equals the first voxel immediately above the generated working-yard solid.

**Result.** CI passed, but the replay still visibly shows the large east-market gray foot separated from the brown shoulder by roughly one reconstructed voxel. Planarizing the lamp therefore did not close the seam; occupancy adjacency is weaker than visual contact when the terrain side remains Smooth.

**Verdict / next step.** Falsified the lamp-foot-smoothing hypothesis. Keep all upper lamp geometry fixed and extend only the foot down one voxel into the higher-precedence lamp/terrace overlap. Strengthen the production-path regression to require that one-voxel embed while preserving the foot top, Planar styles, and pole/lantern continuity. If a fresh saved-pose replay still shows a gap, reject this hypothesis and do not broaden the renderer globally.
