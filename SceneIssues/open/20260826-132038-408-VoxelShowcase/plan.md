# Plan — 20260826-132038-408-VoxelShowcase

## Observed
Single saved pose, no marked circles. Runtime replay still shows a hard polygonal grass boundary: one side fine/dark, the other pale with oversized blade/flower detail. Acceptance: the same ground material must not visibly change scale/treatment across that boundary.

## Evidence and hypotheses
- **Far-terrain presentation differs:** falsified by run `33095541153`. At the stable replay frame the log reports `FAR hole=365.9m ... coverage=True`; the recorded camera is only ~2 m above valley ground and points steeply down, so the photographed foreground is inside the far hole.
- **Semantic grass/material ID differs:** falsified earlier; both generated terrain roles resolve to `Grass` through the shared `SurfaceAt` contract.
- **World/voxel UV scale or texture-table publication differs:** falsified earlier by production state and saved-pose replays.
- **Texture-array mip/minification or luminance-only policy differs:** focused tests passed, but exact saved-pose replays stayed broken; not causal.
- **Near-field LOD/section ownership differs:** live candidate. The boundary is polygonal and consistent with one near surface section/LOD meeting another.
- **Near-field vegetation/overlay mesh covers only one side:** competing candidate. Procedural vegetation has separate foliage/surface shaders and can create a patch edge over the voxel ground.

## Next discriminator
Use production runtime ownership at the recorded camera pose: trace which near renderer(s) cover pixels on each side, then reproduce that responsible path in a focused behavioral test. Do not alter another far shader property without ownership evidence.

## Regression / blast radius / cost
Regression must render or inspect the responsible production near path, not source strings alone, and fail on the current defect. Prefer a presentation-only fix with no storage/generation mutation. Recheck saved pose plus performance logs; reject fixes that add persistent texture copies, extra full-scene draws, or per-pixel expensive work.

## Current state
`fixes/agent-5` is refreshed with current master (`cb309fa1771dc0afc2b9aaa2d923d7aa624b3fd1`). Previous CI request `fbdda553...` is completed and will not be replaced. No new CI transport request until a production candidate and behavioral regression are ready.
