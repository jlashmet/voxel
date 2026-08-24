# Experiment 017 — oversized profile guard

**Hypothesis** — An intentionally oversized two-voxel inner-profile inset distinguishes a missing
or back-facing retained soffit from one that is present but provides insufficient occlusion.

**What was performed** — Temporarily set both near and rear profile guards to 2 voxels, rebuilt
the ordinary production `ArchLookdev` player through `tools/unity-run.sh`, and ran the exact
1637x1140 camera for 25 seconds on the working tree based at `7e5b34d95`. Evidence is
`verification-oversized-guard-pose.png`, `verification-oversized-guard-marked-region.png`, and
`verification-oversized-guard-build.txt`.

**Result** — The retained intrados moves dramatically into the opening, proving it is emitted and
camera-facing. Each voussoir becomes a long triangular tooth from its exact front face to the
oversized side inset; this is intentionally invalid presentation evidence, not a candidate fix.

**What was learned** — Missing emission/back-face culling is disproven. Increasing radial inset is
not a viable production direction: values large enough to dominate the view destroy the cut-stone
soffit. The remaining likely leak is angular/depth coverage between retained blocks rather than
the radial endpoint alone.

**Next** — Restore measured guards and temporarily close retained-profile angular joint gaps. If
the staircase disappears, add a continuous recessed intrados liner behind the intentional joints;
if it persists, isolate depth-cap overlap instead.
