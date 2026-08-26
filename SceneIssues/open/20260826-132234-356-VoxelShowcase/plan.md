# Plan — 20260826-132234-356-VoxelShowcase

## Observed defect and acceptance
The saved VoxelShowcase pose has two marked Dirt/grass contacts. Exact replay of the first attempt left a large rectangular notch in the upper circle; the lower circle improved but acceptance requires both regions to read as continuous terrain rather than broad authored steps. Rendering/residency telemetry was stable during that replay.

## Competing hypotheses
1. **Inactive road-shoulder quantization.** `KentridgeTownSurfaceCatalogue` used coarse shoulder bands. Experiment 002 disproved this as the complete cause: its change passed CI but the upper defect remained.
2. **Live district-terrace shoulder quantization.** The VoxelShowcase catalogue composition actually includes `KentridgeDistrictTerraceCatalogue`, whose four shoulders are emitted as six broad Box slices. Urban shoulders are 72 dm wide, so each authored tread can span roughly 12 dm and visually matches the captured notch/stepping.
3. **Streaming/LOD churn or RegionCorridor overlap.** Exact replay was stable (`visible=714`, `missingMax=0`) and the suspected alternate catalogues are not in this scene's live composition, reducing these hypotheses.

## Discriminator and result
Tracing `ShowcaseCatalogue` → `WorldBuilderVoxelCatalogue` → `KentridgeCombinedVoxelCatalogue` established the runtime owner. `BoxEmitter` already provides an authoritative integer `Ramp` primitive intended for terrain skirts, including reversible rise direction. This falsifies the need for a rendering-only blend or a new geometry system.

## Selected fix
Replace each non-flat district shoulder's six Fill boxes with one `EmitRamp`, preserving the same carve envelope, edge/core elevations, material, footprint, precedence, retaining tiers, and surface-paint passes. Reverse the ramp only when the high endpoint lies on the negative axis. Restore the earlier inactive `KentridgeTownSurfaceCatalogue` experiment to master.

## Regression and blast radius
The focused EditMode regression builds the live `upper-shoulder` district feature, decodes its production ramp, and rasterizes it with `BoxEmitter.RampContains`. It requires more than six surface levels on a meaningful rise and rejects plateau widths inconsistent with a linear voxel ramp. Only Kentridge district shoulder massing changes; primitive count decreases substantially versus six boxes per non-flat edge.

## Remaining gates
- [x] Live owner identified and competing hypotheses recorded.
- [x] Small production fix implemented.
- [x] Focused behavioral regression added.
- [ ] Exact feature SHA targeted CI green.
- [ ] Exact saved pose replayed; both circles visually accepted.
- [ ] Commit `verification-final.png`, terminal metadata, and move open → pending in separate bookkeeping commit.
- [ ] Stop and wait for coordinator; do not close or push master.
