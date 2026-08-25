# Experiment 003 — unpinned LOD diagnostic

**Hypothesis** — A PlayMode diagnostic can place the saved camera once, converge the renderer, and
report the authoritative block and visible source step under the marked ray.

**What was performed** — Added a temporary diagnostic test that set pose 1 before its convergence
loop, rendered the production URP path, raycast authoritative storage at the marked viewport point,
and inspected the visible renderer entries covering the hit. Evidence is in
`verification-marked-lod-attempt1.xml`.

**Result** — The test passed 1/1 but its evidence is invalid: it reported a source-step-1 hit only
38.3 metres away because `VoxelShowcase` movement replaced the one-time camera transform during
the yielded frames.

**What was learned** — Exact camera diagnostics must pin the transform on every render frame, as
the standalone replay harness does. A passing diagnostic with the wrong camera is not evidence.

**Next** — Pin the saved transform before every submitted render and immediately before the ray.
