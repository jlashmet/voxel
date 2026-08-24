# Experiment 004 — pinned LOD diagnostic

**Hypothesis** — With the camera pinned throughout convergence, the marked ray will identify the
distant structure and the production source step that owns it.

**What was performed** — Corrected the temporary diagnostic to restore the saved pose before every
URP submission and immediately before the authoritative raycast. After zero-missing convergence,
the test mapped the hit point to visible renderer entry bounds. Evidence is in
`verification-marked-lod-pinned.xml` and `verification-marked-lod-pinned.txt`.

**Result** — Passed 1/1. The marked ray hit authoritative block `(128,43,31)`, world point
`(102.8,34.8,25.2)` metres, about 416.4 metres from pose 1. The visible source-step mask was exactly
8.

**What was learned** — The circle targets the distant castle's valid step-8 HLOD. The original
terrain-like mound was therefore nearer invalid terrain geometry occluding that structure, not the
castle HLOD itself turning into terrain. This matches the cross-chunk GPU boundary-ownership defect
fixed by `9275602c3`, which also caused other transient sheets and triangles in this capture set.

**Next** — Remove the temporary diagnostic, rerun the focused boundary/vertex and HLOD-summary
regressions on the clean tree, then resolve against the existing causal fix if all pass.
