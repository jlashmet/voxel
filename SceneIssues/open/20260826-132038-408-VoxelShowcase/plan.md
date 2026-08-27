# Plan — 20260826-132038-408-VoxelShowcase

## Observed defect / acceptance
The single captured pose has no annotation circles. At the visible near/far terrain handoff, the left/near grass is finely tiled while the right/far grass shows the same texture at a much larger apparent scale. Acceptance: replay the saved 1928×836 pose with no texturing discontinuity at that handoff and keep the far terrain on the same game-owned grass presentation as the near surface.

## Competing hypotheses and evidence
- **Material identity/policy differs.** Disproved. The far mesh carries the exact application-owned material ID and selects the same `_MaterialSampling`/`_MaterialSurface` row; targeted tests went green, but saved-pose replay still showed the defect.
- **World/voxel coordinate scale differs.** Disproved. Near extracted vertices are already world metres; both shaders divide world position by the 0.1 m voxel size. Explicitly binding far `_VoxelSize = 0.1` still reproduced the defect in saved-pose replay.
- **Shared presentation state is published too late.** Disproved by the original saved-pose replay after the ordering fix. The focused scheduling regression passed, but the visual mismatch remained.
- **Texture-array minification/mip policy.** Selected. Near and far shaders use the same world-space UV and sampling functions, but `BuildTextureArray` created the shared arrays with no mip chain and bilinear filtering. The far clipmap heavily minifies the base texture while the near surface does not, matching the captured scale/aliasing discontinuity.

## Minimal discriminator / regression
`SharedPresentationTextureArraySupportsMinification` builds the production shared texture array and requires a real mip chain plus trilinear filtering. This directly covers the resource contract that differs under far-field minification without encoding grass identity or a second far-only texturing path. Final saved-pose replay remains the causal rendering gate.

## Selected fix / blast radius / cost
Generate the shared albedo/normal texture-array mip pyramids once during catalogue construction: blit each normalized source into a mipmapped temporary render texture, generate its mips on the GPU, copy every mip into the array layer, and sample the array trilinearly. This adds no shader pass, draw, mesh work, or per-frame allocation. Mip pyramids add ~33% texture memory; with the existing 1024 cap and two 8-layer RGBA32 arrays the incremental worst-case is roughly 21 MiB.

## Current state / remaining gates
Feature was refreshed from current master before this attempt. Candidate source/test and experiment are pushed on `fixes/agent-5`. Remaining: re-check/integrate master if it advanced; exact-SHA targeted EditMode regression; exact-SHA saved-pose PlayMode replay; inspect the artifact against the original handoff; commit `verification-final.png`; complete `issue.json`; move the capture to `closed`; merge current master into the feature if needed and push the verified feature head to master non-force.
