# Plan — Kentridge vegetation meadow density

## Scope and acceptance
Work only `20260829-015100-000-KentridgeVegetationMeadowDensity` on `fixes/agent-5`. Kentridge must use reusable WorldBuilder ecology policy, render one connected grass meadow with at least 3,000 blades, respect exclusions, and show plainly visible wind in a stationary built-player replay. Do not edit scene serialization or `.github/test-request.json` on the feature branch.

## Material evidence
- Reusable regional ecology policy is implemented through production terrain sampling; Kentridge allows only semantic Grass and no trees/ambient animals.
- Clock experiments `33244533044`, `33246401704`, and `33246992214` each failed the mandatory player visual gate even though focused tests/workflows were green. The final clock cleanup uses engine-managed `_Time.y`; the production-shader minimal framebuffer repro proves the packed shader itself visibly deforms when explicitly rendered.
- Experiment 004 moved every generated Kentridge grass root from `surface * VoxelSize` to `(surface + 1) * VoxelSize` and added a matching production-scene assertion. Corrected exact-SHA run `33247764440` is workflow-green and reports 11,469 semantic grass instances / 115,119 rendered blades, 57,724 blades in the primary connected meadow, 8 grass mesh chunks, and zero excluded-surface placements.
- Mandatory real-player inspection of `33247764440` still fails: the stationary foreground/ground raster is byte-identical across late captures while only sky pixels move. Comparing the same real-player foreground against pre-grounding run `33246992214` also yields zero changed foreground pixels. Therefore the exposed-root grounding change is visually noncausal and the counted packed grass is not contributing readable pixels to this replay.

## Current hypotheses and discriminator
1. **Replay/view coverage:** the connected meadow may be generated outside the opening camera frustum, so large global blade counts do not prove the required player-height view actually contains grass.
2. **Automatic draw submission:** production packed grass may exist in meshes but not reach the real Kentridge camera during normal frame rendering; the isolated explicit-render minimal repro would not detect this lifecycle/submission failure.
3. **Geometry/depth/material visibility:** production-camera grass could be submitted but fully hidden/edge-on/indistinguishable. The zero-pixel response to a full-voxel Y translation makes simple root burial substantially less likely.

Before another production correction, use the exact Kentridge camera and generated grass instances to prove whether grass bounds/roots intersect the camera frustum and whether an explicit production-batch draw immediately before an actual-camera render produces grass pixels. This separates view coverage from renderer lifecycle from geometry/depth without another blind shader/placement edit.

## Blast radius / cost
The reusable ecology API remains additive and the packed renderer retains one mesh per vegetation chunk rather than per-blade GameObjects. Current density is ~115k rendered blades in 8 grass chunks, so the primary cost question is visibility, not additional topology. The experiment-004 Y offset adds one integer increment per sampled Kentridge root but has no demonstrated visual benefit and should not be retained merely to satisfy a synthetic assertion; final implementation must re-evaluate it against the real surface contract. CPU-ms/GPU-ms are not emitted by the current harness; use available player FPS/memory/build measurements without inventing missing metrics.

## Remaining gates
Update `tasks.md`/experiment evidence, run the production-camera discriminator locally/focused through the existing test path, implement only the causally supported correction, and keep `origin/master` current. Then issue one fresh final exact-SHA request on the assigned `ci-test/fixes/agent-5` mailbox only after the feature SHA changes and the mailbox is idle. Require green focused regression + exact built-player replay plus direct proof that the captured player-height meadow contains visible blades whose silhouettes change over time. Only then complete pending metadata, open→pending→closed bookkeeping, master merge, and non-force publish.
