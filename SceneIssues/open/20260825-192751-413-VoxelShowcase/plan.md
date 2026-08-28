# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
The sole capture has one marked region: the top-left FPS/surface telemetry at the saved `Showcase Camera` pose. The report is moving-player stutter, slow convergence, and transient missing geometry; the architectural direction is GPU-backed near-field rendering with ~1 ms / ~1000 FPS as the headroom target, not a CPU-only tuning exercise.

Final behavioral gates remain unchanged: the 420-frame production traversal must keep visible solids on every moving frame, preserve <= 5 cm far fallback whenever near coverage is incomplete, perform no frame-path blocking completions, cross >= 4 streamed regions, and stay below p95 18 ms / p99 25 ms. A 45 s replay at the captured pose must show intact geometry and no convergence hole.

## Competing hypotheses / discriminator
1. **Near-ring CPU preparation dominates movement.** Eight-worker profiling measured scheduler/admission/worker preparation around 9.16/6.39/5.21 ms and snapshot spikes while upload was ~0.16 ms. Coalescing tiny exact-metadata jobs reduces queue fan-out but cannot by itself satisfy the GPU-migration requirement.
2. **GPU eligibility was semantically unsafe.** Exact rings were routed to GPU after a CPU raw-voxel classifier; removing that scan without reproducing the full surface-semantic rules can turn unsupported authored/faceted/decorated chunks into zero geometry. The prior final request (`71e826f`, run `33125988697`) falsified “GPU candidate routing is already safe”: traversal lost every visible solid draw at frame 8.

Discriminator: classify the raw mirror bricks on GPU using the same packed surface semantics as Storage; unsupported semantics must produce zero GPU count and exercise the existing CPU fallback, while supported continuous chunks complete through GPU extraction.

## Selected fix / regression
The branch now reuses the persistent GPU brick mirror, decodes Storage’s packed surface semantics on GPU, classifies every near-ring mirror brick during the existing sample dispatch, and rejects non-continuous or decorated surfaces before count/write. Step-1/2 CPU classification no longer walks mixed voxel payloads; unsupported chunks fall back to the existing CPU chain rather than publishing emptiness. GPU geometry remains in the shared arena and is drawn through the existing batched submission path.

`ShowcaseGpuMigrationTests.MovingShowcaseActuallyCompletesGpuSurfaceBuilds` is the added behavioral regression: it moves through streamed terrain, requires visible solids and zero blocking completions, and proves a new production GPU surface build actually completes. The unchanged traversal test remains the final performance/coverage gate.

## Blast radius / cost / remaining gates
Only exact near rendering (steps 1/2) changes routing. Storage, collision/gameplay authority, world generation, voxel meaning, and step-4/block-HLOD paths are unchanged. Classification adds a bounded GPU raw-brick scan inside the existing sample dispatch; no geometry readback or player-frame GPU wait is added. CPU/GPU parity and negative-shell tests cover semantic/topology risk.

Remaining: green exact-SHA traversal + 45 s replay; compare every marked region; commit clean `verification-final.png`; pending metadata/move; close; merge latest master; non-force advance master.
