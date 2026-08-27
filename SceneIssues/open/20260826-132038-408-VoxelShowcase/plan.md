# Plan — 20260826-132038-408-VoxelShowcase

## Observed defect / acceptance
The single captured pose has no annotation circles. At the visible near/far terrain handoff, the left/near grass is finely tiled while the right/far grass shows the same texture at a much larger apparent scale. Acceptance: replay the saved 1928×836 pose with no texturing discontinuity at that handoff and keep the far terrain on the same game-owned grass presentation as the near surface.

## Competing hypotheses and evidence
- **Material identity/policy differs.** Disproved. The far mesh now carries the exact application-owned material ID and selects the same `_MaterialSampling`/`_MaterialSurface` row; targeted tests went green, but saved-pose replay still showed the defect.
- **World/voxel coordinate scale differs.** Disproved. Near extracted vertices are already world metres; both shaders divide world position by the 0.1 m voxel size. Explicitly binding far `_VoxelSize = 0.1` still reproduced the defect in saved-pose replay.
- **Shared presentation state is published too late.** Selected. `VoxelFarTerrain` is an ordinary `Geometry` opaque draw, while `VoxelRenderPass` owns the canonical material tables/texture arrays and was configured at `BeforeRenderingTransparents`. The far draw therefore depends on state whose producer is scheduled after it.
- **Texture-array minification/mip policy.** Retained only as fallback if the ordering fix fails the original replay.

## Minimal discriminator / regression
`SharedPresentationPublisherRunsBeforeOpaqueFarTerrain` exercises the production scheduling rule: a configured transparent-boundary pass must resolve to `BeforeRenderingOpaques`, while intentionally earlier events remain earlier. Existing shared-material tests continue to cover exact material identity and UV policy. Final saved-pose replay is the causal rendering gate.

## Selected fix / blast radius / cost
Clamp the existing continuous-surface pass to no later than `BeforeRenderingOpaques`. This moves the one existing publisher/draw pass; it adds no pass, texture array, table upload, mesh rebuild, allocation, or world/simulation change. Affected consumers are cameras using `VoxelRenderFeature`; opaque depth semantics remain unchanged, while later far-terrain draws can consume the already-bound canonical presentation.

## Current state / remaining gates
Candidate is based on refreshed `origin/master` plus only this capture's changes. Remaining: final exact-SHA targeted EditMode regression + saved-pose replay on `ci-test/fixes/agent-5`; inspect artifact; commit `verification-final.png`; complete metadata; promote bookkeeping as instructed; merge latest master and fast-forward master non-force.
