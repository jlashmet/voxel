# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in the saved `Hero Arch` pose. Both flowers and ivy must match the close-up reference intent: broad readable leaves, clustered blossoms, lush asymmetric left/crown growth, sparse right masonry.
- Generic semantic ivy/flower stamps had already received density/color/scale iterations and remained placeholder-like.
- The authored replacement builds 128 lobed ivy leaves plus stems and 30 clustered flower heads in 3 combined hero draws; two small ground ferns remain semantic.
- Green PlayMode tests alone are insufficient. Runs `33124601386` and `33125363847` passed while their standalone saved-pose replays remained essentially bare.

## Hypotheses / results
1. **Generic stamps are the close-up quality limit — confirmed.** Hero ivy/flowers use bounded art-directed meshes.
2. **Mesh/shader absence explains the bare replay — rejected.** The meshes exist, counts/bounds pass, and the foliage shader is compiled into the player.
3. **Camera parenting displaces world-authored vertices — confirmed.** Manual world-identity anchoring passes the minimal camera-pose repro.
4. **A render callback will deliver that correction — rejected.** SRP `beginCameraRendering` CI stayed bare; runtime evidence showed the saved pose verified but the anchor was never invoked.
5. **Transform lifecycle is the correct owner — current discriminator.** A camera-local child-change listener anchors immediately if the root exists or when it is parented, with no render callback or per-frame polling. The regression no longer calls `AnchorCamera` as a test-only repair and also covers disable/re-enable.

## Blast radius / cost
- ArchLookdev only; shared vegetation and other scenes unchanged.
- 3 hero draws, <=4,096 authored vertices, one CPU mesh build on enable, no per-leaf/flower GameObjects, no steady-state `Update`.
- Current master `cbb238b0` is merged into `fixes/agent-4` with no overlapping changes.

## Remaining gates
Green exact-SHA targeted PlayMode CI; original saved-pose standalone replay must visibly show the hero growth; then inspect for depth/occlusion and AAA quality. Final evidence must be clean and at least 1928x836 before pending/closed bookkeeping and non-force merge to master.
