# Experiment 003 — live Storage generation refresh during pending GPU admission

## Question
Why does exact-block recovery begin producing GPU chunks, then permanently plateau even though the built player stays fast and recovery work is finite?

## Competing explanations
1. The 64-block/frame recovery budget is still simply too small.
2. GPU readback/write dispatch hangs after a few chunks.
3. A pending stage holds the Storage generation captured at handoff; if any covered region changes before its demanded blocks become ready, `Covers(oldGeneration)` can never become true, so phase 9 polls forever.

## Runtime discriminator
Run `33232803150` is inconsistent with a pure throughput limit: GPU completions rose from zero to three, player rendering stabilized around 194–200 FPS, and coverage progressed to 27 draws, then remained exactly `27 drawn / 743 missing` for ~16 seconds. The traversal failure reported `gpuCompleted=3`, `gpuFallback=0`, `gpuWaitSlices=1611` with `dirty=2050`, demonstrating active world invalidation while all workers were waiting.

Code-path inspection matches hypothesis 3. `GpuSurfaceExtractionContext.TryBeginStage` captures `world.Storage.Version`, and the old implementation retained that value throughout bounded recovery. `GpuSurfaceMirrorCoordinator.Covers` correctly rejects a covered region whose last solid change is newer than that generation. Meanwhile `CpuTransvoxelChunkCache` phase 9 treats admission as `Pending` and keeps polling; renderer-generation staleness is checked before publication, not while mirror admission waits. A live Storage edit can therefore make the mirror gate permanently unsatisfiable without creating a CPU fallback or a retry.

## Change
Refresh only the mirror's Storage generation on each pending admission attempt. Do **not** rewrite the renderer build generation: the cache keeps its immutable `_build.SourceVersion` and still rejects a build before publication when a relevant renderer invalidation supersedes it. The persistent mirror represents live Storage and cannot reconstruct historical blocks, so refreshing this mirror-only gate lets bounded demand recovery converge without weakening publication correctness or adding an eligible CPU fallback.

## Prediction / falsifier
If the stale Storage gate caused the plateau, the unchanged production traversal should continue past the prior `gpuCompleted=3` stall, reach >=8 GPU completions with zero eligible fallbacks, and preserve visible coverage. The exact built-player replay should keep the low admission cost while near/mid voxel geometry continues converging instead of flattening at 27 draws. A renewed flat plateau with a current Storage gate would falsify this hypothesis and justify investigating queue fairness/budget next.

## Blast radius / cost
One `VoxelRenderBridge.TryGetWorld`/Storage-version read is added only while a GPU stage is admission-pending. No recovery budget, mirror allocation, shader, Storage write, water, HLOD, collision, visibility, or world-generation behavior changes. No per-frame allocation is added.
