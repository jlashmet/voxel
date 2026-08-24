# Experiment 001 — current-head streaming replay

**Hypothesis** — The transient brown terrain patches are still reproducible at the saved pose while
the current renderer converges, then disappear once every visible surface has published.

**What was performed** — Built the production `VoxelShowcase` macOS player through
`tools/unity-run.sh` at source `87bfc27d7`, then ran it at the exact saved 1293×718 camera pose and
70-degree field of view for 35 seconds. The camera was pinned every `LateUpdate`; screenshots were
captured every second. Evidence is in `verification-current-transient.png`,
`verification-current-settled.png`, and `verification-current-streaming-replay.txt`.

**Result** — The hypothesis was confirmed. At 19.2 seconds, the view closely reproduces the
original frame-wide irregular brown patches and comb-like boundaries while 420 surfaces are drawn
and 45 visible surfaces remain missing. By 25.2 seconds `missingVisible` reaches zero; at 26.2
seconds 458 surfaces are stable and the broad brown patches are gone, leaving only narrow authored
dirt features.

**What was learned** — The defect is a deterministic presentation handoff during partial surface
residency. It is neither the authoritative settled terrain nor a one-off capture corruption.

**Next** — Repeat the exact replay with the GPU cutover disabled. If the brown transient changes,
the near step-1/step-2 GPU path is implicated; if it does not, isolate far-terrain fallback versus
CPU coarse-ring coverage.
