# Experiment 023 - authoritative route diagnostic selection

## Symptom
Exact-source run `33715318543` reproduced the same grounded hard-stop at checked-in replay waypoint `resolved-89` after experiment 022 added a materially different scene-owned summit transition control. Per assignment policy, no third traversal fix is allowed until the actual current resolved route is distinguished from stale replay evidence.

## Hypotheses
1. The checked-in 95-waypoint evidence route is stale and still drives the old terminal geometry even though `ShowcaseMountainDragonLayout` now resolves a different terminal path.
2. The authoritative current resolved route still contains the failing terminal segment, so the defect is in realized corridor/collision rather than evidence drift.

## Diagnostic attempt
Run `33718723662` used the only allowed transport and correctly selected exact feature source `8b32f78d599ce24cfe39d2be4fa67d17c5723ef8`. It requested `VoxelEngine.Tests.PlayMode.MountainDragonEvidenceRouteTests.ResolvedProductionRouteCanBeSerializedForEvidence` as an EditMode diagnostic.

The request did not execute. The persistent CI runner performs automatically selected module assemblies first, and the run stopped at the unrelated `VoxelEngine.Rendering.Tests.EditMode` failures before reaching the requested top-level test. Artifact inspection contained no requested-test result and no `MOUNTAIN_DRAGON_RESOLVED_ROUTE_DM=` output. Therefore this run does not discriminate either route hypothesis.

## Root cause of diagnostic failure
The serializer lived in the repository-wide `VoxelEngine.Tests.PlayMode` assembly rather than an automatically selected module-local assembly. Requesting it through the persistent runner cannot bypass an earlier required-module failure, so the diagnostic seam itself was unreachable in the current repository state.

## Correction
Add an issue-owned serializer regression to `Game.Composition.Showcase.Tests.EditMode`, with only direct test references to `Game.WorldBuilder.Api` and `Game.WorldBuilder.Voxel`. This assembly is already selected and runs before rendering. The test computes the same production `ShowcaseMountainDragonLayout.CreateAscentNetwork` route and emits `MOUNTAIN_DRAGON_RESOLVED_ROUTE_DM=`. No production route, shared resolver, motor, tolerance, grade, cut/fill, renderer, or CI policy changes are made.

## Decision rule
- If the module-local serializer shows terminal points diverge from the checked-in 95-waypoint replay after the spiral exit, treat the replay fixture as stale and regenerate only from authoritative resolver output.
- If it shows the same failing terminal segment, reject stale-fixture hypothesis and isolate the realized terrain-corridor/collision mismatch before another composition change.
