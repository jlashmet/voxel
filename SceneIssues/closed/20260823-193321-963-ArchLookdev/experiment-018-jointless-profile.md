# Experiment 018 — jointless retained profile

**Hypothesis** — The binary staircase leaks through the intentional angular mortar gaps between
retained voussoir profile blocks.

**What was performed** — Temporarily forced the retained-profile angular `jointAngle` to zero,
rebuilt the ordinary production `ArchLookdev` player through `tools/unity-run.sh`, and ran the exact
1637x1140 camera for 25 seconds on the working tree based at `7e5b34d95`. This closes all retained
front and soffit angular gaps while leaving the structural voxel mesh unchanged. Evidence is
`verification-jointless-profile-pose.png`, `verification-jointless-profile-marked-region.png`, and
`verification-jointless-profile-build.txt`.

**Result** — The hypothesis was disproven. Intentional radial seams disappear from the front ring,
but the axis-aligned inner staircase remains in the same upper-left and crown region.

**What was learned** — Binary leakage between retained voussoir blocks is not causal. The remaining
candidate is the structural wedge's depth-axis faceted cap/silhouette overlapping the retained
intrados, rather than angular coverage.

**Next** — Restore authored joint widths and suppress depth-axis faceted faces only for a
diagnostic build in profile-bearing chunks. This directly identifies or rules out the wedge cap.
