# GPU renderer production restoration — GPU-first plan

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. All task gates remain mandatory.

## Execution and prior results

User directs local harness/tests and screenshot review; **no further origin pushes**. Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Existing remote request/run preserved; local work does not wait on it.

Local watchdog repair passes five behavioral tests. Shared 44-byte allocator descriptor fixes second-record identity corruption. Common-point capacity status write fixes Metal Exhausted/TooLarge results; descriptor aliases/default-store changes alone were falsified. Thirteen focused GPU tests passed previously. Three 180-second player runs remained unacceptable; their incomplete geometry invalidates performance acceptance.

## Proven layout defect and current validation

Earlier category counts (`category-trace/`: regular36.2M/faceted26.3M requested vertices) are **not legitimate capacity demand**: malformed prepared layouts contaminate them. Adaptive capture selected a high-count step2 chunk after the exact-coordinate replay missed it.

`chunk-trace-adaptive/Prepared`: origin(-256,128,-128), extractor cacheEdge18 but only1000 dense entries (edge10), versus5832 required. Lanes retained first-use resources and admitted incompatible extractors. Wrong flattening strides caused missing/invented surfaces. Temporary readbacks and full inputs are archived; instrumentation removed.

Fix: group requests by cells/padding/cache-edge compatibility, prefer matching idle resources, recreate incompatible idle resources only after completion, and reject mismatched count/write dispatches. A boundary fixture alternating cache edges4/6 fails before (expected256 vertices, got0) and passes after. Thirteen focused tests pass, zero skips, in17s (`layout-after.xml`).

The180-second `layout-fixed/` player replay completed at1920x1080 with11 screenshots. Upper castle walls are restored without floating strips, but traversal retains missing ground, terrain bands, cyan water and grey blockout far structures: **unacceptable**. Diagnostic timings and limitations are in its JSON; no benchmark acceptance.

## Corrected coverage evidence and next fix

`layout-coverage-trace/` completed180s/11 PNGs on d125cbe32. At60s: all538 visible handles live-ready,491 nonempty draws,7454 free vertex pages,zero failures. At120s:69 visible/45 ready,2 free pages,138 failures. At165s:172 visible/171 ready,1 free page,273 failures. By175s,290 of1635 completed requests report Exhausted. Thus stationary corruption is fixed, but traversal genuinely exhausts capacity and fence-only completion hides rejected chunks. Temporary readbacks archived/removed. Screenshots remain unacceptable.

Next implement explicit render-control outcomes and pressure/retry, preserving prior geometry until valid commit. Existing outcome parser and retry branch are disconnected; relief monitors only CPU-arena failures. Resolve the small asynchronous status/identity-channel contract explicitly against G05/G10–G13 before implementation; never transfer geometry or authoritative state, block, increase budgets or hide visible demand.

Hypotheses: (1) bounded offscreen eviction plus retry restores traversal; (2) true visible demand still exceeds capacity and requires semantic-preserving compaction. Discriminator: force exhaustion, prove a rejected current request retries after retired pages become safe, then repeat exact traversal/live-record capture. Do not optimize from corrupted historical counts.

## Remaining gates

Full-scene coverage/visuals -> GPU step4/8/water migration -> physical CPU-backend deletion -> independent-consumer/edit/lifecycle proof -> locked repeated performance/memory workloads -> final local regression/artifact review.
