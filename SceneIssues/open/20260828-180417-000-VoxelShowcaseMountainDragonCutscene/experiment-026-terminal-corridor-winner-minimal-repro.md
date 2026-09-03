# Experiment 026 - terminal corridor winner minimal repro

## Trigger
Fresh current-source built-player run `33746226437` reaches `summit-supported` and then hard-stops grounded at approximately `(-108.50, 47.10, 27.50)` m while targeting `resolved-91`, with 3.808 m horizontal remaining and zero one-second movement. That horizontal position is production resolved point 90 (`-1085, 471, 275` dm).

Experiment 025 / run `33754305666` proved analytic mountain overburden on resolved segment 90 -> 91 is only 3-10 dm while the emitted generic terrain corridor allows 42 dm maximum cut/fill and clears 24 dm above target. Insufficient cut depth is therefore rejected.

## Shared-composition discriminator
`ContinuousTerrainCorridorRasteriser` is explicitly order-independent. For every horizontal column it selects one winner from all overlapping terrain-corridor primitives by visible surface coverage, then grading coverage, then closest distance, with deterministic tie-breaks. It rasterises only that winner. Therefore the previous hypothesis that a later same-precedence segment simply rewrites an earlier segment is also rejected.

`WorldRoadNetworkVoxelCatalogue` lowers each resolved presentation segment into a bounded `EmitTerrainCorridor` definition named with its segment/piece index. The remaining question is whether the winner transition at the sharp terminal join around p90 supplies a discontinuous or unexpectedly high target surface within the player's footprint even though each segment is individually legal.

## Diagnostic-only change
`MountainDragonResolvedRouteDiagnosticTests.CurrentProductionTerminalWinnerSerializesForCollisionIsolation`:

- builds the exact production mountain and ascent network;
- builds the exact production road voxel catalogue;
- reconstructs each terrain-corridor primitive from its immediate `EmitTerrainCorridor` operands plus its explicit world placement;
- calls the production `ContinuousTerrainCorridorRasteriser.TryChoose` and `TerrainCorridorRasteriser.TrySample` rather than duplicating winner/sample policy;
- samples p90 -> p91 at nine deterministic longitudinal positions and approximately 4.5 dm on both lateral sides, covering a player-capsule-scale footprint;
- logs generated definition name, target height, closest distance, visible-surface coverage, and grading coverage with prefix `MOUNTAIN_DRAGON_TERMINAL_WINNER=`.

The Showcase EditMode test assembly references the existing `VoxelEngine.Structures.Runtime` assembly only so this diagnostic can exercise the shared compositor directly. No production assembly/API, route control, motor/tolerance, grade/cut-fill, summit placement, or material policy changes.

## Decision
Run only the focused discriminator first.

- If the shared winner changes between otherwise continuous adjacent segments in a way that creates a target-height discontinuity at an ordinary polyline join, prove the defect independently in `VoxelEngine.Structures` before a narrow shared fix.
- If shared winner output is continuous and consistent with the intended route, reject corridor composition as the collision cause and continue the minimal repro at the realized collision/character footprint boundary without changing route or motor policy.
- If the discontinuity is caused only by this scene's pathological terminal route geometry while shared semantics are coherent, repair only Mountain Dragon composition.

No expensive bake/replay is justified until this discriminator completes.
