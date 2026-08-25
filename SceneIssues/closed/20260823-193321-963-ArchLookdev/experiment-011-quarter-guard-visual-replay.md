# Experiment 011 — quarter-voxel guard visual replay (fix attempt 3)

**Hypothesis** — Increasing the rear-only intrados guard to the smallest Q4-aligned value above
the exact rear-layer 0.200-voxel error removes the marked staircase.

**What was performed** — Rebuilt the production `ArchLookdev` player through
`tools/unity-run.sh` on the working tree based at `7e5b34d95`, then ran it for 25 seconds at the
saved 1637x1140 `Hero Arch Camera` pose and 34-degree field of view. The settled 22-second frame
and marked crop are `verification-fixed-pose-attempt3.png` and
`verification-fixed-marked-region-attempt3.png`; build evidence is
`verification-quarter-guard-build.txt`.

**Result** — The hypothesis was disproven. The regular horizontal/vertical staircase remains
visible behind the smooth retained intrados in the same upper-left and crown regions. Increasing
the rear endpoint from 0.125 to 0.25 voxel does not materially change that edge.

**What was learned** — Rear-endpoint coverage is not the controlling geometry. The visible edge
is likely an earlier depth layer of the composed backing/opening, where a taper that reaches its
full offset only at the far endpoint provides little or no cover. This must be isolated rather
than inferred from the full scene.

**Next** — The three-attempt threshold is reached (rear cap, 0.125 guard, 0.25 guard). Build the
required bare-bones reproduction with the minimum backing/opening/profile geometry and inspect
crossing error versus retained-profile coverage at every depth layer before another fix.
