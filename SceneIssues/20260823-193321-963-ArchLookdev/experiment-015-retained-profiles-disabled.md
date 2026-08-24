# Experiment 015 — retained profiles disabled

**Hypothesis** — If the staircase is emitted by retained profile segmentation, removing retained
profiles will remove or materially relocate the marked edge.

**What was performed** — Temporarily passed no profile source to the production rendering binding,
rebuilt `ArchLookdev` through `tools/unity-run.sh`, and ran the exact 1637x1140 saved-camera replay
for 25 seconds on the working tree based at `7e5b34d95`. No authoring settings changed. Evidence is
`verification-no-profiles-pose.png`, `verification-no-profiles-marked-region.png`, and
`verification-no-profiles-build.txt`.

**Result** — The hypothesis was disproven. With profiles disabled, a stronger version of the same
axis-aligned staircase spans the complete inner opening, including every marked region. The smooth
front voussoir/profile overlay is absent, exposing the binary curve underneath.

**What was learned** — The retained analytic curve does not generate the staircase; it masks only
part of a binary arch/opening surface. The owning binary surface is now narrowed to the structural
arch ring or the composed bay backing/opening.

**Next** — Restore profile rendering and build the exact arch with only structural piers/ring plus
its retained profiles. Persistence means the structural ring owns the edge; disappearance means
the composed backing owns it.
