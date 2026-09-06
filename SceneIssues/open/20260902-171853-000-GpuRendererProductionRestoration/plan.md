# GPU renderer production restoration — GPU-first plan

## User-authorized objective — 2026-09-05 (America/Los_Angeles)

Make the GPU voxel path work in the complete `Assets/Scenes/VoxelShowcase.unity`, delete every file/code path used solely by the retired CPU rendering backend, and pursue **1,000 FPS / 1.00 ms whole-frame time**, or the closest reproducibly achieved result without hiding content or reducing quality. This supersedes the CPU-first sequence and permanent CPU-fallback requirement. **CPU visual polish is not a prerequisite for GPU testing.** Correct final GPU visuals remain mandatory.

## Current source and evidence

Starting feature: `1e1010383ec1eaccc444f57ad8fa509414fb077e` (production/tests `95d4d304...`); fetched remote master: `ef475182b866eabfe8e1d1a39c82bf7810a03f49`. Request `fc6c3320...`, run `34003412217`, passed taper regressions but captured CPU extraction (`gpu req/pub=0`) and rejected blockout visuals; its Rendering player artifacts collided. See `frustum-geometry-evidence.md` and `gpu-density-oracle-history.md` for historical proof, not final acceptance.

Existing request `560b0c08f022c42faa9c6877e63d109083eb2dc9`, run `34005604349`, job `101412081392`, was queued when replanning. Preserve it until terminal; inspect its artifact-isolation/probe results. It is an earlier CPU diagnostic, not the next milestone. Continue GPU implementation while it runs; do not schedule another CPU-only cleanup replay first.

## Next discriminator

Reconcile the retained GPU implementation with current shared fixes, remove CPU-forcing from the GPU validation launch path, and run canonical multi-chunk extraction plus a real GPU VoxelShowcase replay on one exact source revision.

**H1:** GPU-only input, mixed-LOD, allocation/publication or lifetime failure loses otherwise valid surfaces. Compare prepared inputs through final live draw ownership, stopping at the first mismatch.

**H2:** malformed geometry/materials already arise in shared authoring/far presentation. Compare identical inputs/cameras, label shared defects separately, and fix them with GPU enabled rather than waiting for a perfect CPU scene. Bounded owner toggles are diagnostic only; final output restores every production system.

## Delivery order

1. Prove actual GPU extraction, publication and drawing now; reject silent CPU fallback. Start whole-frame profiling immediately.
2. Cover every required semantic and LOD, including CPU-dependent steps 4/8, then streaming, edits, fences, pressure and restart. Inspect GPU captures after fixes.
3. Migrate genuinely shared responsibilities out of mixed CPU/GPU owners; physically delete CPU-only implementations, fallback switches, assets and obsolete tests. Preserve independent canonical expectations, not a hidden CPU renderer.
4. Optimize measured bottlenecks toward 1.00 ms with the locked workload in `tasks.md`. Report attained FPS, tails and limiting stages honestly.
5. Validate the CPU-backend-free final source, close only after all required gates, then current-master integration and PR + auto-merge.

## Boundaries and cost

Authoritative integer CPU storage, generation, collision and simulation remain. Rendering owns GPU extraction/URP integration and local validation; Composition owns wiring; Showcase owns workload setup. Extend affected module-owned scenes, not parallel renderers. Headless value adapters use unit tests. Inventory every deletion dependency; keep memory/blocking/device budgets unchanged. No global render-pipeline rewrite without measured necessity. Separate plan/checklist, exact-SHA evidence and final merged closure remain required.
