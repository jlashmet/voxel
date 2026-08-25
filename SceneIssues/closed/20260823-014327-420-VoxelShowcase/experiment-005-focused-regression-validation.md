# Experiment 005 — focused regression validation

**Hypothesis** — The current clean two-pose presentation is backed by green boundary/vertex parity
invariants for the invalid occluder and feature-preserving HLOD invariants for the marked castle.

**What was performed** — After removing all temporary camera, scene, and diagnostic-test changes,
ran one graphics-enabled EditMode invocation through `tools/unity-run.sh` at source `4813b91dd`.
The exact filter included the GPU boundary-ownership test, both mixed-LOD GPU/CPU vertex attribute
parity cases, and all ten `SurfaceBlockHlodSummaryTests`. Evidence is in
`verification-focused-regressions.xml` and `verification-focused-regressions.txt`.

**Result** — Passed 13/13 in 0.187 NUnit seconds. Unity exited with status 0 in 14 seconds.

**What was learned** — The existing production fix and regressions cover both sides of the visual
diagnosis: nearer GPU geometry cannot claim the wrong chunk-boundary cell, while the distant castle
retains disconnected features, openings, materials, capacity, and greedy-mesh correctness in HLOD.
No additional production or test change is justified.

**Next** — Review and commit the capture-local evidence, then resolve the issue separately against
the existing boundary-ownership fix.
