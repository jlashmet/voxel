# Experiment 001 — trace the active Showcase surface path

**Question** — Is the capture primarily blocked by draw submission, or is the validated GPU mesher present but bypassed while terrain converges?

**Method** — Inspected the current `VoxelSurfaceScheduler`, `CpuTransvoxelChunkCache`, GPU extraction context/arena integration, relevant EditMode tests, and the repository history that measured player-frame cost and introduced/validated the GPU Transvoxel path.

**Result** — Production exact-ring extraction is unconditionally forced to CPU by `GpuCutoverDisabled = true`. The GPU implementation remains integrated behind that gate: it supports steps 1 and 2, performs asynchronous counter readback, writes geometry directly to the shared arena, and retains CPU fallback for ineligible work. Existing CPU/GPU oracle history found and repaired density/topology mismatches before the original cutover. Historical full-Showcase profiling also separated drawing from build cost: settled drawing was inexpensive relative to active CPU extraction/upload, and missing-visible coverage was a convergence symptom rather than a need for more draw calls.

**Interpretation** — The smallest architecture-preserving experiment is to restore the already-validated exact-ring GPU cutover, not replace Unity submission or create another mesh pipeline. Because the gate was deliberately rolled back later, restoration must retain a runtime emergency override and must be re-proven by current regression/oracle/arena tests plus a production replay before closure.

**Next** — Run the new cutover policy regression against the hard-disabled baseline; it should fail before production code is changed.
