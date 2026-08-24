# Experiment 016 — structural arch only

**Hypothesis** — If the composed bay backing/opening owns the staircase, rendering only the
structural `ArchFeature` plus its retained profiles will remove it.

**What was performed** — Restored profile rendering, then temporarily changed the lookdev
authoring pipeline to emit only the exact structural arch (two piers, 13 arc wedges, and retained
profile blocks) at the production origin. The bay backing, veneer, shoulders, face backing,
plinths, imposts, and damage composition were absent. Rebuilt through `tools/unity-run.sh` and ran
the exact 1637x1140 saved-camera replay for 25 seconds on the working tree based at `7e5b34d95`.
Evidence is `verification-structural-only-pose.png`,
`verification-structural-only-marked-region.png`, and `verification-structural-only-build.txt`.

**Result** — The hypothesis was disproven. The same regular staircase remains along the inner
upper-left curve and crown with only the structural ring/piers and retained profiles present.

**What was learned** — This is the faithful bare-bones visual reproduction required after the
three failed full-scene attempts. The composed bay and its backing are ruled out; the owning
surface is the structural arc-wedge mesh that remains visible behind/through its retained profile.

**Next** — Restore ordinary bay authoring. Inspect extraction classification and depth-cap/side
ownership for the arc wedges, especially whether the visible surface is a faceted plane rather
than the smooth density crossing measured by the earlier diagnostic.
