# Plan

## Acceptance
Improve authoritative `WorldRoadNetwork` presentation without changing route/topology authority: coherent curved/diagonal edges; formed carriageway/shoulders and bounded cut/fill; deterministic shared wear; topology-aware junctions; stable chunk/LOD continuity; preserved terrain/vegetation/collision/destruction semantics and budgets; and production-quality exact built-player validation in `KentridgePlayableSlice`.

## Current evidence
Earlier Kentridge fixes removed the plot-frontage overwrite/trench. Valid player-height run `33416931897` then exposed a reusable physical road defect: internal curve/junction pieces produced repeated endpoint bands/ridges. Minimal generic run `33421466465` proved the same physical surface changed from top 16 to 15 solely by reversing incident `TerrainCorridor` piece order.

The implemented correction uses a continuous corridor arbitration stage in `FeatureRegionBuild`: contiguous corridor pieces remain bounded storage/execution partitions, but each x/z column chooses one closest physical corridor candidate before mutation. No endpoint fades, route rewrites, Kentridge coordinates, persisted vertex growth, or `EmitTerrainCorridor` width growth were added. Pre-master run `33422342809` passed the new invariant.

After merging master `b64e9456b150b374dc950ab11f02a2427412cc66`, exact feature SHA `0f40e94119c9222dd4242d16734319c6ad2a6c40` was validated by run `33430943763`. Attempt 1 passed the requested regression but hit a Unity process SIGSEGV (139) starting `SpatialReservationProductionIntegrationTests` at low RSS; this was an infrastructure failure. Retrying the failed workflow job in place succeeded. Exact attempt 2 passed the two continuous-field tests, all repository-derived `spatial-reservations` tests, and both standalone players within the targeted budget.

Human inspection of fresh Kentridge `t=64s` and `t=74s` player-height captures shows the piece-end/capsule repetition is materially reduced, so the continuous-field fix addresses its demonstrated cause. Visual acceptance still fails: long longitudinal crown/shoulder ridges and abrupt terraced lateral steps remain, especially through the foreground `t=74s` curve/junction.

## Next discriminator
The remaining shape matches the previously recorded secondary hypothesis: `TerrainCorridorRasteriser` computes target grade and `CrossSectionOffsetDm` after rounding the sample to whole decimetres, then multiplies that integer result by `VoxelsPerDecimetre`. At scales above one voxel/dm, a one-decimetre crown/drop therefore becomes a multi-voxel vertical jump instead of a voxel-precision slope.

Before another production change, add one generic non-Kentridge regression with a Kentridge-like core/shoulder and scale > 1. Sample adjacent physical columns across the cross-section and prove the current target surface jumps by more than one voxel. The required invariant is that physical cross-section/grade interpolation may preserve rounded semantic `TargetHeightDm`, but physical `TargetHeightVoxels` must progress at voxel precision without multi-voxel terrace jumps.

## Remaining gates
1. Prove the residual stair-step with the generic voxel-precision repro before changing production code.
2. If proven, move only physical grade/cross-section interpolation to voxel precision; keep semantic dm contracts, coverage/wear/vegetation, route authority, and budgets unchanged.
3. Re-run focused road regressions plus automatic module/player validation and inspect fresh player-height curve/junction evidence.
4. Measure unchanged primitive/program/vertex/storage budgets and check runtime cost.
5. Refresh/merge current master before final promotion; any merge requires exact-SHA revalidation.
