# Experiment 006 — rear-guard focused validation compile

**Hypothesis** — The rear guard and its two focused fixtures compile and pass together.

**What was performed** — Added the 0.125-voxel rear-only taper, extended the profile-emitter
contract, converted the crossing diagnostic into a threshold, and requested both focused EditMode
fixtures through `tools/unity-run.sh` on the working tree based at `7e5b34d95`.

**Result** — The hypothesis was inconclusive because test compilation failed before any test ran.
`ArchCrossingStabilityTests` referenced `CpuTransvoxelChunkCache` without importing its
`VoxelEngine.Rendering.Runtime.SurfaceExtraction` namespace. Unity exited 1 after 5 seconds.

**What was learned** — This is test wiring only; no production behavior was exercised and it does
not count as a geometry fix result.

**Next** — Add the missing namespace import and rerun the identical focused filter.
