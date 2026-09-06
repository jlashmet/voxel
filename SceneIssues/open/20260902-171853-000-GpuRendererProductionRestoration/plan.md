# GPU renderer production restoration — GPU-first plan

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. All task gates remain mandatory.

## Execution and prior results

User directs local harness/tests and screenshot review; **no further origin pushes**. Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Existing remote request/run preserved; local work does not wait on it.

Local watchdog repair passes five behavioral tests. Shared 44-byte allocator descriptor fixes second-record identity corruption. Common-point capacity status write fixes Metal Exhausted/TooLarge results; descriptor aliases/default-store changes alone were falsified. Thirteen focused GPU tests passed previously. Three 180-second player runs remained unacceptable; their incomplete geometry invalidates performance acceptance.

## Prior GPU findings

`publication-trace/`: vertex exhaustion rejected259 requests; host fences concealed missing live records. Nonempty live records reached draw compaction. `explicit-bucket-offset/`: explicit GPU metadata prefixes fixed Metal indirect-instance addressing and restored castle towers; production-shader raster regression fails before/passes after. These repairs leave visual acceptance unmet. See tasks.md for exact artifacts and limitations.

## Proven layout defect and current validation

Earlier category counts (`category-trace/`: regular36.2M/faceted26.3M requested vertices) are **not legitimate capacity demand**: malformed prepared layouts contaminate them. The exact selected chunk was not requested in the first85s replay; adaptive capture selected the first step2 chunk above120K regular vertices instead.

`chunk-trace-adaptive/Prepared`: origin(-256,128,-128), extractor cacheEdge18 but only1000 dense entries (edge10), versus5832 required. Lanes retained first-use resources and admitted incompatible extractors. Wrong flattening strides caused missing/invented surfaces. Temporary readbacks and full inputs are archived; instrumentation removed.

Fix: group requests by cells/padding/cache-edge compatibility, prefer matching idle resources, recreate incompatible idle resources only after completion, and reject mismatched count/write dispatches. A boundary fixture alternating cache edges4/6 fails before (expected256 vertices, got0) and passes after. Thirteen focused tests pass, zero skips, in17s (`layout-after.xml`). The initial all-solid fixture passed before and was insufficient; replaced with a discriminating solid/air boundary.

The180-second `layout-fixed/` player replay completed at1920x1080 with11 screenshots. Upper castle walls are restored without floating strips, but traversal retains missing ground, terrain bands, cyan water and grey blockout far structures: **unacceptable**. Diagnostic timings and limitations are in its JSON; no benchmark acceptance.

## Next hypotheses and discriminator

1. Correct layout removes the false geometry and most arena pressure.
2. Remaining capacity/recovery failures still leave real visible chunks absent.

After traversal review, recapture bounded live/count/status diagnostics on corrected layouts. Do not infer GPU readiness from host fences. Fix genuine pressure/retry only from corrected demand, then migrate step4/8/water and address visual defects. No budget/content changes.

## Remaining gates

Full-scene coverage/visuals -> GPU step4/8/water migration -> physical CPU-backend deletion -> independent-consumer/edit/lifecycle proof -> locked repeated performance/memory workloads -> final local regression/artifact review.
