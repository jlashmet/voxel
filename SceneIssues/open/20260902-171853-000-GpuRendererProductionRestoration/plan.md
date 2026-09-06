# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid LODs have GPU implementations. Step8 streaming handles 4,096 mixed sources through a 1,024-slot mirror and rejects edited candidates. Full coverage remains incomplete. Latest Showcase completed 180s/11 captures/exit 0, with 642 missing-visible chunks; reviewed 75.1s/150.1s remains unacceptable. Approximate stationary/walking diagnostics: 138/136 FPS, not accepted benchmarks.

Production water now uses GPU snapshot/count/write/allocation/publication/draw. The installed water mask fixes omitted river material 22. WaterDemo 42s/seven captures passed; river is restored, but planar cascade bands, incomplete surroundings and floating vegetation remain unacceptable.

## Current retirement and experiment

Deleted `CpuWaterSurfaceChunkCache`, `WaterBrickMeshBatchJob` and the water shader's contiguous CPU-buffer path. Canonical storage-to-water PlayMode tests now invoke the GPU cache. Narrow shader fixtures seed synthetic GPU buffers solely to isolate shading, using the actual cache entry/indirect kernel/shader; they are not scene art or visual acceptance. Four semantic extraction cases now run the GPU mesher. Five broader fixtures preserve exact sorted-vertex digests/counts captured from the old CPU oracle after another actual-GPU parity pass; no CPU extraction algorithm remains in tests.

First migrated PlayMode run: 12/14 passed. H1: GPU publication/draw regression. H2: tests still assume CPU uploads/old property types. Failures were an obsolete positive CPU-upload assertion and `GetInt` reading a `SetInteger` property; actual raster cases passed. Updated tests assert zero CPU geometry uploads plus real published GPU vertices, and use the typed integer accessor. Rerun: 14/14 passed, exit 0/14s.

Retirement-oracle capture: five passed/12s. Initial EditMode retirement compile failed because the new test helper referenced an unreferenced CoreUtils assembly; test-only immediate shader cleanup fixed it. Then 21 focused EditMode cases passed/15s, including canonical faces/seams/materials, fixed digests, cache lifecycle and GPU draw parity. No source references to the retired classes remain.

Standalone WaterDemo after deletion completed 42s/seven captures/exit 0; reviewed 26.3s/32.3s retains river/cascade with existing unacceptable defects. Three affected rendering architecture checks passed (13s); the requested WorldBuilder boundary test did not appear in the discovered results and is not claimed passed. User now accepts imperfect water and prioritizes performance. Finish this deletion commit, then investigate CPU scheduling/visibility cost without reducing content or distance; preserve the remaining solid-removal requirement.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
