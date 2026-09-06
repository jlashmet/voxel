# GPU renderer production restoration — tasks

**Plan:** [plan.md](plan.md)  
**Acceptance scene:** `Assets/Scenes/VoxelShowcase.unity` only. Kentridge and the mountain are acceptance content because they are rendered inside this scene; do not widen into separate scene assignments.  
**Execution rule:** first establish a production-quality CPU-rendered VoxelShowcase baseline. CPU forcing is a temporary diagnostic gate, not final success. Do not resume GPU diagnosis while any white/blob/missing/material artifact remains in CPU VoxelShowcase. After CPU is clean, restore/re-enable the production GPU path and complete GPU parity without broad fallback or weakened acceptance.

## Current execution priority — CPU VoxelShowcase first

- [x] **TGPU-019CPU0A — Clear the imported VoxelShowcase Input System build prerequisite.** Current-master input fix `7e6c609c...` passed run `33988857330`; feature `fc767620...`, run `33996360570`, compiled and exercised the production Showcase and validation assemblies again. Package presence alone is not the evidence.
- [x] **TGPU-019CPU0B — Restore the required module-owned player-validation pair.** The master-owned `ShowcaseInputRuntimeValidation.unity` / same-directory `.player-scenario.json` pair passed derived module validation and real-player execution in exact feature `fc767620...`, run `33996360570`.
- [x] **TGPU-019CPU1 — Capture the current CPU-only VoxelShowcase defect on exact SHA.** Exact feature `fc767620...`, request `8e6aac9...`, run `33996360570`, artifact `9978457769`: `SceneIssue/verification-final.png` and `SceneIssue/Screenshots/showcase-000-t015.2s-stationary.png`, `showcase-001-t025.2s-stationary.png`, `showcase-002-t035.2s-stationary.png`. The actual player log reports zero GPU requests/publications. Visual classification: **prototype/blockout quality**; giant slab and malformed far masses remain. This completes reproduction, not visual acceptance.
- [ ] **TGPU-019CPU2 — Identify the production draw owner of every white/blob region.** Correlate each bad region to CPU voxel surface, far terrain, semantic far-feature rendering, or another existing production VoxelShowcase presentation path. Add only bounded diagnostics needed to distinguish these owners; do not build a parallel renderer. Current trace also identifies a handoff concern: `ShowcaseFarFeatureRuntime.Update` submits near proxies without detailed publication readiness. Global convergence or a distance cutoff alone is not per-feature coverage proof.
- [ ] **TGPU-019CPU2A — Run a bounded normal/disabled/restored far-feature owner discriminator.** Required by the still-rejected exact frustum replay: left taper remains white and right-hand masses remain malformed. Opt in only through this issue's replay metadata; toggle only existing far-feature presentation, retain the same camera, restore original enable states, and prove opt-in/restoration behavior. The disabled interval is not acceptance. Inspect all three phases and final restored image before selecting the next fix.
- [x] **TGPU-019CPU3 — Find the first shared CPU-visible rendering divergence.** The canonical frustum's centre/direction/radii were lost in `FarFeaturePresentationAdapter` and Rendering substituted an AABB. Eight exact fail-before cases and eight pass-after cases prove that boundary; run `34003412217` shows the left AABB changing into a taper. This identifies one divergence, not all remaining regions.
- [ ] **TGPU-019CPU4 — Fix the demonstrated shared/CPU presentation defect generically.** No Kentridge-name, mountain-name, captured-coordinate, or magic-material special case. Preserve canonical world truth and reuse the production rendering/material/far-world path.
- [ ] **TGPU-019CPU4E — Preserve every required module-player artifact independently.** Run `34003412217` executed FarWorld then Water into the same Rendering output directory; the retained log/images belong to Water. Fix output identity per module/scene/scenario, add a filesystem regression proving two targets retain distinct evidence, and verify both FarWorld and Water evidence in the next exact artifact. Do not remove validation targets or weaken artifact assertions.
- [ ] **TGPU-019CPU4F — Prove the bounded canonical far-frustum correction.** Source `da3f5be...`, fail-before request `6ddc727...`, run `33999899224`, artifact `9979637933`: eight intended silhouette failures, 649 other EditMode passes. Candidate `a164456...` preserves resolved cap geometry. Pass-after request `fc6c3320d9b986b8d2401fcae0a17de80d286691`, run `34003412217`, exact source `e4e2f997...`, artifact `9980566933`: 657 module EditMode passes, three PlayMode passes, and eight focused passes including topology/cache assertions. Inspected full-scene final image shows the corrected taper but remains prototype/blockout quality. Keep this task open until required FarWorld player evidence is retained independently (CPU4E). Other primitive mismatches and full CPU visual acceptance remain open.
- [ ] **TGPU-019CPU5 — Prove CPU VoxelShowcase is production-quality.** Exact-SHA built-player captures must show clean castle/terrain plus Kentridge and mountain content with correct materials/silhouettes, no white blobs, no malformed far geometry, no large holes, and no near/far handoff artifact from representative stationary and traversal views.
- [ ] **TGPU-019CPU6 — Restore the production GPU implementation only after CPU passes.** Reconcile the pre-CPU-gate agent-1 GPU implementation from merge parent `a0ac0f5e...` with the shared CPU fix, restore normal GPU cutover policy, then resume the GPU-specific tasks below. The proven CPU capture becomes the visual oracle.

## GPU visual parity and first deterministic divergence

- [ ] **TGPU-019 — Restore stationary GPU VoxelShowcase to the proven CPU visual baseline.** The same scene content visible in the CPU proof—castle, terrain, Kentridge, mountain, nearby structures/far content—must render without large missing/white regions, absent surfaces, stale chunks, or fallback-hidden success.
- [x] **TGPU-019C — Minimal production-faithful GPU solid validation scene exists and passed.** Exact feature `6451cf98...`, run `33929485980`.
- [x] **TGPU-019D — Exact CPU expectations exist for the minimal fixture.** 41 authored solids -> 114 exposed faces, 456 vertices, 684 indices.
- [x] **TGPU-019E — Minimal fixture passed the production GPU path.** Same exact run reported `pub=1`, `fallback=0`, `visible=1`, `missing=0`; this falsifies a universal one-chunk defect.
- [ ] **TGPU-019G — Keep the reusable production-batch CPU/GPU oracle harness.** Compare prepared inputs, density, count/prefix, canonical pre-page geometry, allocation/pending state, CPU Commit/Abort outcome, and final live draw visibility. Readback remains test-only.
- [ ] **TGPU-019H — Re-run deterministic static multi-chunk CPU/GPU parity after CPU acceptance.** Stop at the earliest GPU-only mismatch and require fail-before/pass-after exact evidence.
- [ ] **TGPU-019A — Correlate the first broken full-scene GPU chunk only after deterministic parity is classified.** Use the same VoxelShowcase world locations proven clean on CPU; trace admission -> mirror -> extraction -> pending/live pages -> draw visibility without scene-specific renderer logic.
- [ ] **TGPU-019F — Reject false paged-GPU completion.** `Ready/Exhausted/Stale/TooLarge` must be observed by the CPU state machine; only successful pending geometry may complete. Failure preserves old live geometry, reclaims pending state exactly once, and cannot create a permanent hole.
- [ ] **TGPU-019B — Built-player VoxelShowcase replay is the immediate visual gate after each GPU correctness fix.** Compare directly with the exact CPU baseline.

## Proven work retained from the investigation

- [x] **TGPU-001 — Reproduced the original GPU density failure on current production path.**
- [x] **TGPU-002 — Inventoried production GPU selection, fallback, page-handle publication, and metrics.**
- [x] **TGPU-003 — Inventoried supported semantic surface features and unsupported categories.**
- [x] **TGPU-004 — Preserved the minimal CPU/GPU density repro.**
- [x] **TGPU-005 — Proved the original defect was at/above centre occupancy rather than smooth taps alone.**
- [x] **TGPU-006 — Isolated the historical Metal compiler/context introducing boundary instead of continuing speculative fixes.**
- [x] **TGPU-010 — Moved persistent resolution into the dedicated resolver and restored dense production sampling.**
- [x] **TGPU-011 — Proved source-step 1/2 density parity.**
- [x] **TGPU-012 — Proved material classification parity.**
- [x] **TGPU-013 — Proved Smooth/Rounded/Planar/Sharp/Cubic surface-style parity.**
- [x] **TGPU-014 — Proved authored boundary/coating parity and unsupported decoration handling.**
- [x] **TGPU-015 — Proved regular topology parity.**
- [x] **TGPU-016 — Proved faceted topology parity.**
- [x] **TGPU-017 — Proved negative-shell ownership parity.**
- [x] **TGPU-018 — Proved transition-face parity.**
- [x] **TGPU-020 — Proved persistent and dense semantic inputs agree.**
- [x] **TGPU-021 — Proved mixed/uniform/empty publication semantics and eviction tombstoning.**
- [x] **TGPU-022 — Proved negative-coordinate/boundary lookup and directory collision handling.**
- [x] **TGPU-023 — Proved runtime edit propagation rejects stale generation and recovers current coverage.**

## GPU mirror, allocation, publication, and lifetime correctness

- [ ] **TGPU-024 — Verify eviction/recovery/liveness under pressure.** No permanent holes, deadlock, or silent CPU takeover.
- [ ] **TGPU-025 — Verify no production frame-path blocking.** Diagnostic readback is test-only.
- [ ] **TGPU-025A — Stress visible-handle upload-ring reuse under actual GPU lag.** Change only if a stall is demonstrated.
- [ ] **TGPU-026 — Make extraction completion lane-local and GPU-backed with bounded in-flight lanes.**
- [ ] **TGPU-026A — Hold source mirror/residency leases until the GPU actually finishes consuming them.**
- [ ] **TGPU-026B — Prove the exact Metal fence capability/`passed` contract; do not equate graphics-queue ordering with CPU reuse safety.**
- [ ] **TGPU-026C — Make teardown safe with submitted GPU work still in flight.**
- [ ] **TGPU-027 — Retire draw pages on draw-completion evidence, not CPU-frame delay.**
- [ ] **TGPU-027A — Make paged publication two-phase and CPU-authoritative: pending Allocate/Write -> explicit Commit or Abort.**
- [x] **TGPU-027B — Coalesce duplicate handle commands deterministically per handle.** Focused evidence run `33916627573`.
- [ ] **TGPU-027C — Separate renderer build generation from Storage/mirror source generation.**
- [ ] **TGPU-027D — Reclaim pending pages on release/cancel/reacquire exactly once.**
- [ ] **TGPU-028 — Prove lifetime safety under rapid handle/page/mirror reuse pressure.**
- [ ] **TGPU-028A — Prove rejected stale generations never become live and preserve prior live geometry.**
- [ ] **TGPU-029 — Prove cross-LOD batch scratch compatibility for source steps 1 and 2.**
- [ ] **TGPU-029A — Separate physical prepared-cache stride from logical per-request resolver extent.** Optimization remains parked until correctness proves need.
- [ ] **TGPU-029B — Execute a real mixed-LOD production batch and enforce explicit lane compatibility.**

## Production GPU cutover and reuse

- [ ] **TGPU-030 — Make GPU eligibility semantic and explicit.**
- [ ] **TGPU-031 — Eliminate silent eligible CPU fallback once GPU acceptance resumes.**
- [ ] **TGPU-032 — Preserve explicit CPU fallback only for declared unsupported work/devices with observable reason.**
- [ ] **TGPU-033 — Prove VoxelShowcase production GPU cutover.** Representative streaming/traversal must show visible GPU builds, zero eligible fallback, and no blocking violation.
- [ ] **TGPU-034 — Prove an independent production consumer uses the same GPU renderer without duplicate enabling/render logic.** This is renderer reuse proof, not additional visual-scene scope for CPU diagnosis.
- [ ] **TGPU-035 — Verify renderer restart/lifecycle without leaks, stale statics, duplicate ownership, or disabled cutover.**

## Built-player visual, traversal, edit, and performance acceptance

- [ ] **TGPU-040 — Maintain Rendering-owned focused production validation scene/scenario using the real stack.**
- [ ] **TGPU-041 — Capture exact-SHA VoxelShowcase traversal evidence against the CPU baseline.** Inspect holes, cracks, stale chunks, materials, faceted surfaces, far representation, and LOD seams.
- [ ] **TGPU-041A — Validate optional nonresident halo behavior while traversing VoxelShowcase frontiers.** Preserve liveness; fix continuity only if artifacts are demonstrated.
- [ ] **TGPU-042 — Capture representative VoxelShowcase edit evidence.** Old geometry must be replaced and converge without stale remnants.
- [ ] **TGPU-043 — Prove final visual success is genuinely GPU-rendered rather than hidden by fallback.**
- [ ] **TGPU-044 — Preserve moving-frame p95/p99 performance budgets; do not relax them.**
- [ ] **TGPU-045 — Preserve settled/stationary performance and eliminate pathological continuing churn.**
- [ ] **TGPU-046 — Measure mirror/scratch/page/upload memory and traffic against authoritative budgets.**

## Regression, cleanup, and close

- [ ] **TGPU-050 — Run Rendering module EditMode regressions on the exact final feature SHA.**
- [ ] **TGPU-051 — Run any specifically required Rendering PlayMode regressions on the exact final feature SHA.**
- [ ] **TGPU-052 — Run repository-derived affected module validation including owned validation scene(s).**
- [ ] **TGPU-053 — Pass the canonical standalone full-application integration gate required by the repository.** This is CI integration evidence; CPU visual diagnosis remains scoped to VoxelShowcase.
- [ ] **TGPU-054 — Remove/bound investigation-only probes, readbacks, operand echoes, test controls, and verbose logging.**
- [ ] **TGPU-055 — Audit fallback and duplicate renderers; no scene-local replacement or broad eligible fallback remains.**
- [ ] **TGPU-056 — Review final diff/blast radius.** Keep only demonstrated Rendering/VoxelShowcase integration, validation, shared-fix, and required SceneIssue metadata changes.
- [ ] **TGPU-057 — Close only with exact evidence for every required checkbox/acceptance criterion.** Move directly `open` -> `closed`, set fixed metadata, merge current master, then final PR + auto-merge and verify closed issue on `origin/master`.
