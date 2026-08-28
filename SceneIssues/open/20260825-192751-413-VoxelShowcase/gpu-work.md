# GPU work remaining — SceneIssue 20260825-192751-413

- [ ] Move GPU eligibility/classification off the CPU for GPU-candidate chunks. Classify raw mirrored bricks in compute and route unsupported/empty cases to the existing CPU fallback without publishing holes.
- [ ] Decode `PackedStorage` surface semantics correctly on GPU before classification/reconstruction; add coating/detail parity coverage.
- [ ] Port planar/sharp/faceted semantics to GPU only after explicit CPU↔GPU geometry, ownership, material, and normal parity tests are green.
- [ ] Replace `GpuSurfaceExtractionContext.TryPin`'s dense per-chunk CPU brick-cache staging walk with a persistent GPU brick mirror fed by compact storage brick/version deltas.
- [ ] Keep generated geometry GPU-resident through count/reserve/write and drawing. No geometry readback or blocking GPU wait may enter the player-frame path.
- [ ] Preserve CPU fallback correctness for unsupported semantics, unavailable compute, mirror exhaustion, allocation refusal, count/write disagreement, and stale-version rejection.
- [ ] Instrument CPU classification/meshing, GPU staged/written/fallback chunks, mirror publication, bytes uploaded, GPU compute, readbacks/waits, visible/missing coverage, and mesh throughput.
- [ ] Benchmark startup/convergence and sustained production-speed movement, not only stationary FPS.
- [ ] After meshing migration, re-profile visibility/submission; move it GPU-side or into a compact Burst path if it is still material.
- [ ] Final targeted CI must prove the production GPU path is exercised and preserve the existing traversal/correctness gates. Do not close on a CPU-only optimization.
- [ ] Continue toward the original ~1000 FPS rendering-headroom target; if that target is unattainable with this design, document measured evidence and replace the architecture rather than weakening the issue.
