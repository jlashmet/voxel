# Execution checklist

Work the next unchecked non-blocked step. Keep plan.md under approximately 500 words; put substantial
results in `experiment-NNN-<slug>.md` with hypothesis, exact SHA, inputs, evidence, verdict and next step.
This is a handoff specification, not a claim that any implementation or acceptance below has passed.

## 1. Establish source and reproduction

- [x] Coordinator publishes/assigns this issue using `SceneIssues/README.md`; select one assigned
  `fixes/agent-N` and matching `ci-test/fixes/agent-N`. Prevent overlapping work on the earlier
  GPU restoration issue. Fetch current master; record feature SHA, merge base and Unity version.
- [x] Inspect AGENTS.md, constitution, device matrix, both feature plans, previous restoration
  tasks, checkpoint-evidence.json and cpu-render-backend-removal-ledger.md. Treat historical fixes
  as leads, not proof that the current scene is correct.
- [ ] Confirm the actual editor/player project path. Audit tracked diffs AND untracked C#/shader/
  asmdef files, not just branch ancestry. Preserve user edits before repairing checkout state.
  Specifically verify obsolete `CpuWaterSurfaceChunkCache.cs` and `WaterBrickMeshBatchJobTests.cs`
  are absent; do not restore the retired CPU backend to satisfy references.
- [ ] Prove a fresh compile completes without C#/shader/Burst errors. Inspect project-relative
  `Logs/Editor.log`, not only the redirecting user-library log. Use `tools/unity-run.sh` for local
  launches and ask first if the developer's editor may be open; remote workers use targeted CI.
- [ ] Keep the production standalone capture path bounded but capable of completing a clean cold
  build. The prior 12,288 MB RSS guard killed Unity at 12,311 MB with ~28 GB free before product
  validation; validate the narrow 14,336 MB guarded default and its tooling regression on exact SHA.
- [ ] Obtain an exact-SHA standalone VoxelShowcase baseline using existing production capture
  tooling and real generation/material/presentation paths. Record executable/source identity,
  baked-world identity, seed, camera poses/route, timings, hardware/API, resolution, render scale,
  frame caps/VSync, settings and full console log. No new custom player-build command.
- [ ] Reproduce the castle stationary view and traversal independently. Preserve original poses
  and annotate every visible hole/seam/missing house/vegetation region with frame and world bounds.
  Capture startup, settled view, frontier traversal and return. Store portable evidence references;
  historical local Artifacts paths alone are insufficient. Define explicit settle/convergence
  deadlines from existing scenarios/budgets before evaluating results; do not extend them to pass.

Progress note: `experiment-001-source-and-baseline.md` records source/document review and the malformed
first request. `experiment-002-cold-player-build-memory-guard.md` records corrected run `34151335102` /
job `101834086315`: it entered the real player build but `unity-run.sh` killed the cold process tree at
12,311 MB against the 12,288 MB guard, before player evidence; no compiler error preceded the kill. The
narrow 14,336 MB guarded-build repair and regression are committed. GitHub remote state still cannot prove
developer-machine untracked-file absence, so the checkout-audit checkbox remains open.

## 2. Find the first broken geometry invariant

- [ ] Read `GpuSolidChunkCache`, `VoxelSurfaceScheduler`, `GpuSurfaceDiscovery.compute`,
  GPU source readiness/extraction/page-arena code, far coverage/selection dispatchers and
  `ShowcaseFarFeatureRuntime`. Identify what MissingVisibleCount actually counts, its readback
  age and whether it includes empty or correctly far-covered candidates. Retain raw counters;
  independently establish visible occupied coverage instead of redefining missing to zero.
- [ ] Choose a reproducible annotated gap. Obtain bounded diagnostic records keyed by world
  chunk/step/shard, generation and source version, covering: canonical cell occupancy/material;
  expected semantic/LOD ownership; source directory/mirror residency and pending uploads;
  discovery/demand and queue age; admission/rejection reason; extraction output/count;
  allocation/page handle; publication acknowledgment; retirement; selected draw and bounds.
  GPU readback is diagnostics only, never authoritative world state. Bound trace memory/cost.
- [ ] Discriminate H1 versus H2: occupied source never reaches ready/extraction indicates source/
  scheduling ownership; valid current geometry lost after publication indicates lifetime,
  replacement or draw ownership. If cells are actually absent, trace production generation/bake
  rather than inventing renderer geometry. If both fail, record separate defects and owners.
- [ ] Reproduce under fixed stationary demand, then boundary-crossing traversal and return.
  Exercise negative coordinates, steps 1/2/4/8, mixed/air/uniform sources, delayed uploads,
  air-to-solid and solid-to-air edits, cancellation and bounded capacity pressure. Explain how
  the first observed broken transition causes the original captured gap.
- [ ] Implement only the proven repair: e.g. bounded fair readiness/service and retry for H1,
  generation-safe publication/retirement and valid replacement handoff for H2. These are conditional
  directions, not prescribed fixes. Never increase global capacity, hide demand, shorten distance,
  suppress objects or lower quality to make missing counts disappear.
- [ ] Add failing-then-passing behavioral regression through production computation and the owning
  module's focused production scene. Reuse Rendering/Validation/SolidGpu, FarWorld or Water as
  appropriate. Composition changes require its own local scene. No primitives, parallel mesher,
  private reflection oracle or manual scene registration. Use player-scenario.json only for behavior.
- [ ] If two materially different fixes fail the same symptom, stop speculative patching and reduce
  to a causal production-path repro before another fix. Revisit hypotheses explicitly.

## 3. Full-scene correctness gate — blocks optimization

- [ ] Replay every baseline pose/annotation on the fixed SHA plus startup, stationary convergence,
  traversal/return, LOD/far handoff, edits and restart. Require no persistent occupied-visible holes,
  cracks, stale versions or missing required content. Show bounded recovery under pressure;
  allocation failures alone do not establish missing geometry, nor do zero failures prove coverage.
- [ ] Compare rendered systems with representative production scenes. Inspect silhouette, grounding,
  materials, seams, terrain integration, repetition and missing content. Classify each evidence set
  explicitly; only production-quality passes. Add and fix observed acceptance defects here.
  Record the prior rough-water allowance explicitly; it does not excuse missing water geometry.
- [ ] Pass owning module tests/players AND full VoxelShowcase. A small scene's zero-missing counter
  cannot satisfy this gate. Record image evidence alongside independent cell-to-draw coverage.

## 4. Diagnose and repair performance

- [ ] Resolve 400 versus historical 1,000 FPS objective with coordinator before numeric signoff.
  Lock complete content and correctness configuration; use 1920×1080/scale 1.0 M4 Max/Metal
  for the recorded target, and report native resolution separately. Other hardware is supplemental.
- [ ] Repeat the same baseline castle pose/window (historically 60–90 seconds) and same traversal
  route/window (120–180 seconds), at least three runs. Report per-run whole-frame median/p95/p99,
  FPS aggregation, CPU/GPU stages, caps, coverage, memory and run variability. Never compare the
  stationary castle directly with simpler walking terrain as evidence of an optimization.
- [ ] Capture a separate Metal System Trace/profile of the stationary workload. Attribute critical
  path to CPU preparation, source service, submission count, compute/draw/shader time, waits,
  presentation or other-process GPU work. Profiling overhead must not become benchmark evidence.
  External contention requires trace evidence; CPU process activity alone is insufficient.
- [ ] Change the measured bottleneck only. Re-run identical performance windows and full-scene
  correctness after each change. Revert ineffective product changes; preserve evidence of falsified
  causes. Report a missed target honestly and keep required acceptance open.

## 5. Finish migration and lifetime work

- [ ] Audit remaining water CPU `CollectVisible`/nearest-build ranking and retirement ledger against
  current code. Migrate presentation decisions to bounded GPU paths with independent parity,
  cancellation, edit, eviction/rebuild and disposal regressions. Preserve CPU integer authority
  and necessary host orchestration; delete only proven retired backend helpers/oracles.
- [ ] Measure index-stream/scratch/page-arena/mirror/far-atlas allocation against every authoritative
  device-tier budget. Fix proven console/mobile overruns without weakening budgets or content.
- [ ] Exercise repeated load/unload, traversal/return, edits, eviction/rebuild and restart. Prove
  generation-safe fences/handles and plateauing caches after warmup. Use bounded scenario cycles
  and repository-approved soak workflow; targeted tests must finish within five minutes of start.
  Test repeated editor lifecycle in EditMode, not batch PlayMode. Report leaks/finalizer warnings.

## 6. Exact-SHA validation and closure

- [ ] Review final diff and affected module ownership. Run convention-derived owned assemblies and
  module players, exact-SHA VoxelShowcase acceptance and canonical Kentridge standalone integration.
  No zero-test, failed, cancelled, timed-out or intermediate-artifact success claims.
- [ ] Preserve distinct per-scene/scenario artifacts with source SHA, logs, captures, assertions,
  raw timings and budget results. Include remaining uncertainty; do not infer correctness from FPS.
- [ ] Follow canonical CI admission/retry rules; leave queued/running requests alone. Keep issue open
  until all required gates pass. Then closure bookkeeping, merge current master, PR/auto-merge and
  confirm closed issue on origin/master per README. Do not auto-close the previous restoration issue.
