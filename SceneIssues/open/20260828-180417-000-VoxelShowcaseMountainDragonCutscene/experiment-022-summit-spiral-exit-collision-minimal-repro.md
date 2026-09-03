# Experiment 022 - summit spiral-exit collision minimal repro

## Trigger

The source-matched startup bake repaired the earlier stale-payload/mid-turn symptom, but the same standalone grounded replay then failed late at waypoint 90/95 while approaching `resolved-89`.

Because production traversal had already received multiple materially different fixes, this experiment freezes traversal-tolerance changes and isolates the new failure before another fix.

## Exact-source evidence

Targeted CI run `33655077271` selected feature source `b6516f2008beb993f2659d82a04b2c0dd02898d7` and the real one-shot baker succeeded. It exported:

- payload size: `15,696,411` bytes
- payload SHA-256: `108195fbd188a877392671121276e6df5a060241b8e2d37cf54b7f23326b921e`
- content signature: `7554A9C4`
- focused bake test duration: `167.662 s`, below the unchanged `240 s` guard
- observed CI peak RSS remained below the unchanged `14 GiB` guard

The fresh-payload standalone replay passed the prior vertical-ascent discriminators (`lower-turn`, `mid-turn`, `upper-turn`) and reached `resolved-88`. It then attempted `resolved-89` and hard-stalled while grounded at approximately `(-104.589, 45.600, 28.000)` feet position. Horizontal movement decayed to zero while the target remained about 3.4 m away.

## Discriminators

- The player is grounded at the stall, so this is not a falling/air-control symptom.
- Z is already aligned and X stops at a stable voxel-face-like coordinate, so steering loss is rejected.
- `WorldRoadResolver` has already accepted every production segment under the unchanged `280` permille grade and `42 dm` cut/fill contracts, so the authored road is semantically legal.
- `WorldRoadNetwork` adds shoulder/clearance outside the profile influence radius; this is not a 1 m-wide corridor misunderstanding.
- The red summit placeholder footprint is south of the stalled Z coordinate, so the cube dragon is not the collider.
- The stalled radial distance from `ShowcaseMountainDragonLayout` mountain centre is about `10.45 m`, effectively the configured `SummitApproachRadiusDm = 10.5 m`.

That last discriminator localizes the failure to the scene-owned transition where the 1.5-turn spiral previously jumped directly from its 10.5 m terminal control to the summit centre. The downstream voxel terrain-corridor realization exposes a collision seam at that abrupt semantic heading transition even though the resolver's grade/cut-fill contract is legal.

## Narrow fix

Keep the shared road API/resolver, grade, cut/fill, widths and grounded acceptance unchanged. Add one Showcase-owned semantic control that continues the existing 22.5-degree angular progression inward onto the broad `SummitRadius` before the final centre point. This smooths only the composition-owned terminal approach and lets the generic resolver/corridor grade the same road normally.

Regression: `MountainDragonSummitApproachRegressionTests.SummitApproachKeepsInwardSpiralControlBeforeCentre` verifies the production intent retains this inward spiral transition rather than regressing to a direct radial jump.

## Required validation

Run the exact new feature head through only `ci-test/fixes/agent-4` with the same source-matched bake + standalone replay. A valid fix must pass the new regression, keep existing road acceptance green, and traverse beyond the former 10.5 m spiral-exit seam without waypoint-tolerance changes.
