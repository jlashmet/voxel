# Plan — VoxelShowcase dirt/grass seam

## Observed / acceptance
The immutable capture marks two Dirt/grass transitions. Fresh built-player replays reached full residency. The earlier road-shoulder granularity change cleaned the lower circle, but the upper circle still contains a hard rectangular grass tongue at about X=91.0..93.8 m, Z=28.6..30.4 m. Acceptance requires both original circles clean in a fresh saved-pose replay.

## Hypotheses / discriminators
1. **Stale bake / streaming:** falsified by repeated fresh fully resident replays.
2. **District terrace/civic/correction ownership:** falsified by replay evidence and, decisively, by composition: VoxelShowcase seed `1592594996` selects organic Kentridge, so those district-only stages are absent.
3. **Organic plot grading:** falsified. Exact-seed plot regression passed, but the real-player ground plane remained pixel-identical to the rejected replay.
4. **Organic route rasterization:** supported. `KentridgeDirectedTownSurfaceCatalogue` is live for this seed; its sampled route points currently carve/fill axis-aligned square stamps, and live route placement bounds overlap the corrected upper marked envelope. The square union produces the captured plan-view step/tongue.

## Selected fix / regression
Keep route centers, widths, terrain-following heights, precedence, and the two-primitive budget unchanged; replace each square route carve/fill stamp with a vertical cylinder of the same half-width. This removes square corners without changing route extent or introducing scene-specific geometry.

`VoxelEngine.Tests.PlayMode.KentridgeOrganicRouteSceneIssueRegressionTests.SceneIssue20260826132234356OrganicRouteEdgesUseRoundSurfaceStamps` builds the production directed-town catalogue at seed `1592594996`, proves the organic backend is selected, proves live route placements overlap the corrected marked envelope, and verifies every route uses the bounded round carve/fill pair.

## Blast radius / cost
The change is limited to organic Kentridge circulation. Placement count, footprint, precedence, primitive count, and per-frame work are unchanged; only primitive shape changes from box to cylinder. District/legacy roads are untouched.

## Gate
Use only `ci-test/fixes/agent-8`. Exact-SHA PlayMode CI must pass the focused regression and the built-player harness must replay the saved VoxelShowcase pose for 45 seconds, reach full residency, and show the lower circle still clean and the upper rectangular tongue gone.
