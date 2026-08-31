# Plan

## Acceptance
Improve authoritative `WorldRoadNetwork` presentation without changing route/topology authority: coherent curved/diagonal edges; formed carriageway/shoulders and bounded cut/fill; deterministic shared wear; topology-aware junctions; stable chunk/LOD continuity; preserved terrain/vegetation/collision/destruction semantics and budgets; and production-quality exact built-player validation in `KentridgePlayableSlice`.

## Current evidence
Earlier Kentridge fixes removed the plot-frontage overwrite/trench. Valid player-height run `33416931897` then exposed a reusable physical road defect: internal curve/junction pieces produced repeated endpoint bands/ridges. Minimal generic run `33421466465` proved the same physical surface changed from top 16 to 15 solely by reversing incident `TerrainCorridor` piece order.

The first correction uses a continuous corridor arbitration stage in `FeatureRegionBuild`: contiguous corridor pieces remain bounded storage/execution partitions, but each x/z column chooses one closest physical corridor candidate before mutation. No endpoint fades, route rewrites, Kentridge coordinates, persisted vertex growth, or `EmitTerrainCorridor` width growth were added. Post-master run `33430943763` attempt 2 passed the continuous-field regressions, repository-derived spatial-reservation tests, and both standalone players after attempt 1 hit a proven Unity SIGSEGV infrastructure failure.

Fresh Kentridge `t=64s`/`t=74s` captures showed piece-end/capsule repetition materially reduced, but long crown/shoulder ridges and abrupt lateral terraces remained. That separated the original composition seam from the previously recorded secondary quantization hypothesis.

Exact discriminator run `33431993454` on feature SHA `b328f169333d802857425359ce981bdd6fab70e5` confirms the residual defect. The existing partition/junction tests still pass, while `PhysicalCrossSectionMustNotTurnOneDecimetreCrownIntoMultiVoxelTerrace` fails with an adjacent-column target-height jump of 4 voxels versus an allowed maximum of 1. The fixture uses a generic Kentridge-like corridor at scale 4 and directly samples the shared physical rasterizer, so whole-decimetre cross-section rounding is now a demonstrated independent cause rather than a screenshot hypothesis.

## Selected correction
Keep the semantic centreline, closest-point, edge variation, coverage, wear and vegetation calculations in the authored decimetre grid. Change only physical crown/shoulder vertical interpolation: derive the existing semantic crown/drop amplitudes in dm, convert those amplitudes to voxels, and interpolate the physical offset in voxel units before forming `TargetHeightVoxels`. This preserves rounded semantic `TargetHeightDm` behavior while preventing one dm from becoming an artificial multi-voxel lateral terrace at `VoxelsPerDecimetre > 1`.

Production commit `4670c82dc2b2f8e4e036d6c7dc13f70c864df364` implements that narrow change in `TerrainCorridorRasteriser`; it adds no route data, no road-specific vocabulary, no primitive, persisted vertex, or program-width growth. Exact request commit `163e8082edcfcea9765af9b50ddf5c4342f0aebf` is queued as run `33434127106`; it must not be replaced while queued/running.

## Remaining gates
1. Let run `33434127106` complete. If product failure, fix cause; if proven infrastructure failure, retry the same run/transport only.
2. Once the voxel-precision regression is green, run the broader road regressions and repository-derived module/player validation on the exact current feature head (documentation commits after the production SHA mean the queued run is supporting, not final-head evidence).
3. Inspect fresh player-height curve/junction captures specifically for removal of the long lateral terraces while retaining the already-fixed partition continuity, frontage integration, vegetation recovery and traversability.
4. Measure unchanged primitive/program/vertex/storage budgets and runtime impact; do not weaken budgets.
5. Refresh/merge current `origin/master` before promotion; any merge invalidates exact-SHA gates and requires revalidation.
