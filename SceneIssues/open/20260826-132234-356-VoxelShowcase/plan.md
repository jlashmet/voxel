# Plan — VoxelShowcase civic west terrace seam

## Observed / acceptance
The immutable capture has two marked world hits: upper `(X=92.15m, Z=11.57m)` and lower `(X=89.03m, Z=20.91m)`. The exact-SHA fresh bake/replay for `4179a07d…` still showed the same axis-aligned civic approach/terrace defects. Acceptance is a continuous west transition into the civic summit at both marks, without the broad rectangular shelf/tongue.

## Competing hypotheses / evidence
1. **Stale bake or streaming/LOD:** falsified. CI rebuilt `ShowcaseWorld.bytes`, the real player converged to `missingVisible=0`, and the defects remained.
2. **Material-only correction:** falsified. Restricting the later correction layer away from urban shoulders changed ownership but did not remove the geometry.
3. **Civic south shoulder:** falsified by coordinates. The prior repair only touched `Z=24.0..31.2m`; the saved hits are `Z=11.57m` and `20.91m`. That off-target geometry repair is removed.
4. **Late civic-west court fill:** falsified by coordinates. The court is `X=92.8..108.2m, Z=25.4..29.8m`, so neither saved hit lies inside it. Restore the court to its prior behavior rather than broadening this issue.
5. **Civic-summit west shoulder reuses one edge sample:** supported. The civic core begins at `X=92.0m`; its 7.2m west shoulder spans `X=84.8..92.0m` across `Z=4.0..24.0m`. Both marks lie on that transition. `KentridgeDistrictTerraceCatalogue` sampled its whole west edge once at `Z=14.0m` and emitted one 20m-deep X ramp, even though local natural height varies along Z. The adjacent `upper-shoulder` already solves the same problem with 0.5m Z strips sampled at each local west-edge segment.

## Selected fix / regression
Use the existing profiled-west-edge path for `civic-summit` as well as `upper-shoulder`; no new generation stage. The civic definition uses the same existing profiled-terrace primitive ceiling (`96`). The PlayMode regression builds `KentridgeCombinedVoxelCatalogue` and checks the production civic placement/program at both captured Z values (`116dm`, `209dm`), proving each west ramp meets its own local `TerrainQuery` edge sample rather than the old single centre-Z sample.

## Blast radius / cost
Geometry change is limited to the 7.2m × 20m west shoulder of `civic-summit`; other district edges and all courts keep their prior behavior. The civic west edge grows from one ramp/carve pair to at most 40 pairs (5dm strips), matching the already-established upper-shoulder strategy and existing `96` primitive budget. Sampling happens once during catalogue build; no per-frame work.

## Gate
Keep the issue open until the exact final feature SHA passes `SceneIssue20260826132234356CivicWestShoulderFollowsLocalTerrainAlongBothMarkedRegions` through the single `ci-test/fixes/agent-8` transport and its fresh saved-camera replay visually clears both original marks.
