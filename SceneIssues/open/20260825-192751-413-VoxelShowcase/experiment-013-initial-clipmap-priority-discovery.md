# Experiment 013 — initial clipmap priority discovery

**Hypothesis.** The deterministic zero-draw failure is caused by first-window surface discovery starvation, not camera/frustum rejection or arena pressure.

**Action / source.** Audited the production `VoxelSurfaceScheduler.Prepare` path after exact-pose experiment 012. On `e4c940895f59a93a0440d2ec022dbdad25aa1304`, `UpdateClipmapWindow` returned `changed=true, hadPrevious=false` for the initial camera window, but the caller skipped both region-difference admission and `AddImmediateCameraDiscoveryRegions`. Applied the smallest control-flow correction in `20a32987b0273e6f8f2718e4bb169648cf7e3dae`: any changed clipmap window activates camera-near priority discovery; only windows with a previous state enqueue the geometric difference boxes.

**Result.** The patch is 9 changed lines in the scheduler and does not alter meshing, LOD, upload, arena, residency, or frame-time budgets. It directly targets the captured state where step1 was `res=0 known=0` despite a valid Unity frustum.

**Verdict.** This is the leading causal fix. Falsification criterion: the exact captured-pose production regression still reaches a valid Unity forward probe but never obtains production frustum candidates, or the unchanged moving traversal still drops all visible voxel draws after initial convergence.

**Next.** Require green exact-SHA focused CI, then run the unchanged moving performance/coverage test together with saved-pose real-player replay and inspect the resulting visual artifact.
