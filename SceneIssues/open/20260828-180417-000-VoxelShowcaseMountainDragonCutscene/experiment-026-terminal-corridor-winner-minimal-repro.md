# Experiment 026 - terminal corridor winner minimal repro

## Trigger
Fresh current-source built-player run `33746226437` reaches `summit-supported` and then hard-stops grounded at approximately `(-108.50, 47.10, 27.50)` m while targeting `resolved-91`, with 3.808 m horizontal remaining and zero one-second movement.

Run `33806764602` corrected an earlier assumption about that position. The current production route is `p89=(-1080,468,280)`, `p90=(-1089,471,288)`, `p91=(-1120,482,260)` dm. The built-player stall is therefore not p90; it is an off-centre footprint location near the terminal turn.

Experiment 025 / run `33754305666` proved analytic mountain overburden on resolved segment 90 -> 91 is only 3-10 dm while the emitted generic terrain corridor allows 42 dm maximum cut/fill and clears 24 dm above target. The exact sample deltas were `+3,+3,+5,+6,+7,+8,+9,+9,+10` dm. Insufficient cut depth is therefore rejected.

## Shared-composition discriminator
`ContinuousTerrainCorridorRasteriser` is explicitly order-independent. For every horizontal column it selects one winner from all overlapping terrain-corridor primitives by visible surface coverage, then grading coverage, then closest distance, with deterministic tie-breaks. It rasterises only that winner. Therefore the previous hypothesis that a later same-precedence segment simply rewrites an earlier segment is rejected.

`WorldRoadNetworkVoxelCatalogue` lowers each resolved presentation segment into a bounded `EmitTerrainCorridor` definition named with its segment/piece index. The remaining question for this experiment was whether the winner transition at the sharp terminal join supplied a discontinuous or unexpectedly high target surface within a player-scale footprint.

## Diagnostic-only change
`MountainDragonResolvedRouteDiagnosticTests.CurrentProductionTerminalWinnerSerializesForCollisionIsolation`:

- builds the exact production mountain and ascent network;
- builds the exact production road voxel catalogue;
- reconstructs each terrain-corridor primitive from its immediate `EmitTerrainCorridor` operands plus its explicit world placement;
- calls the production `ContinuousTerrainCorridorRasteriser.TryChoose` and `TerrainCorridorRasteriser.TrySample` rather than duplicating winner/sample policy;
- samples p90 -> p91 at nine deterministic longitudinal positions and approximately 4.5 dm on both lateral sides;
- logs generated definition name, target height, closest distance, visible-surface coverage, and grading coverage with prefix `MOUNTAIN_DRAGON_TERMINAL_WINNER=`.

The Showcase EditMode test assembly references existing Structures runtime dependencies only so this diagnostic can exercise the shared compositor directly. No production assembly/API, route control, motor/tolerance, grade/cut-fill, summit placement, or material policy changes.

## Result
Run `33806764602` executed this test on exact source `152fc7f8649e94716aa41eab3e93b26b45963caa`; the test finished `Passed` in 0.005 s before a later Unity Test Framework temporary-scene restoration failure aborted the workflow.

The production winner is continuous across p90 -> p91. Segment `s135p0` owns centre samples 0-6 and `s136p0` owns centre samples 7-8. Centre target heights progress smoothly `473,474,475,476,478,479,480,481,483` dm. Lateral samples remain full visible-surface and grading coverage (`31/31`); their target heights vary only by the ordinary cross-section/adjacent-segment amount rather than jumping to a high uncut mountain surface.

This rejects the shared terminal join/winner transition as the cause of the built-player hard stop. The next minimal repro must sample the realized collision/terrain footprint at the actual stall `(-1085,275)` dm, not assume that location is p90.

## Decision
Do not change route, motor/tolerance, grade/cut-fill, summit placement, or shared corridor policy from this result. Continue only at the realized stall footprint boundary. If that footprint reveals a shared realization defect, prove it independently in Structures before a narrow shared fix; if shared realization is correct, keep any repair scene-specific.
