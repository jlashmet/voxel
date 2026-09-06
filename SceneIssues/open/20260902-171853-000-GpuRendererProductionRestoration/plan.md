# GPU renderer production restoration — implementation plan

**Acceptance:** `Assets/Scenes/VoxelShowcase.unity` only. Production-quality CPU stationary/traversal presentation precedes GPU cutover. CPU images and diagnostic ablations never count as GPU or final visual proof.

## Verified result

Before source `da3f5be...`, request `6ddc727...`, run `33999899224` failed eight canonical taper cases; 649 other EditMode tests passed.

Pass-after request `fc6c3320d9b986b8d2401fcae0a17de80d286691`, run `34003412217`, completed **success** without replacement. Artifact `9980566933`: 657 module EditMode passes, three PlayMode passes, and eight repeated focused taper passes. The real emitter -> adapter -> renderer repair, topology and cache assertions passed on exact source `e4e2f997...`. See [frustum-geometry-evidence.md](frustum-geometry-evidence.md) for identities, digests and limits.

Inspected `SceneIssue/verification-final.png`: the left AABB became a taper, but its flat white surface and the malformed right-hand masses remain **prototype/blockout quality**. GPU requests/publications remain zero. Capture-less metadata used the default scene view; zero-sample frame timings are not performance proof.

FarWorld and Water executed into the same Rendering output directory; the retained log/images belong to Water. Required FarWorld evidence was overwritten despite the successful workflow. CPU4E isolates outputs by module/scene/scenario. Filesystem tests reproduce four old-path failures and pass all five cases after repair; actual standalone evidence retention still requires CI.

## Hypotheses and discriminator

1. **Far-feature overlap/approximation:** `ShowcaseFarFeatureRuntime.Update` submits selected proxies without per-feature detailed publication readiness; other shapes still use AABBs and omit carves.
2. **Another owner/source:** remaining masses might instead be voxel surfaces or far terrain, or canonical source boxes.

Probe `c684d27...` opts in only through this issue's `renderProbe` metadata. It holds the actual authored camera, keeps normal presentation at 25 s, suppresses only existing far-feature renderers during 30–40 s, restores original states for the 45 s capture, and ends at 50 s. A 55 s replay retains a normal final image. No world/geometry/material mutation or production fallback policy. Fourteen behavioral cases cover opt-in, timing, instance/state preservation and teardown.

## Active exact CI

Source **`95d4d30467463b47beb57a731b137da01c56d7d4`**; direct-child request **`560b0c08f022c42faa9c6877e63d109083eb2dc9`**; run **`34005604349`**, job **`101412081392`**, currently **queued**. Leave it untouched until terminal. This documentation update does not change its production/test source. Then inspect all probe phases, restored final output, actual GPU counters, and independently retained FarWorld/Water proof before selecting a fix.

## Ownership and remaining gates

Rendering owns its regressions and `Rendering/Validation/FarWorld/`. Headless Composition uses local EditMode tests. Showcase SceneRuntime owns existing runtime validation plus bounded replay instrumentation; its disabled interval is diagnostic, not rendered acceptance. Python tooling owns artifact-path preservation.

Continue CPU2A/CPU4E/CPU4F and remaining geometry/material/handoff work; do not replace coverage proof with distance/global-convergence hiding. Obtain clean CPU stationary/traversal evidence, then complete GPU reconciliation, parity, lifetime, streaming/edits, no-fallback, performance and independent-consumer gates. Every required checkbox precedes closure, current-master integration and PR + auto-merge.
