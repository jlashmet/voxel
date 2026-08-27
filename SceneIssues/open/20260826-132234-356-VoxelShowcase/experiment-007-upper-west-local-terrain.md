# Experiment 007 — upper west local terrain

## Hypothesis
The final upper-circle Grass rectangle is the west edge of `upper-shoulder` meeting varying natural terrain with one centre-z edge sample. Because the terrace spans 200 dm along z, a single z=340 sample can match the lower/central mark while leaving the upper natural surface proud of the Dirt wedge.

## Evidence
Fresh saved-camera replay `33035969054` retains the upper rectangular Grass intrusion with stable residency (`visible=644`, `missingMax=0`). Normal Kentridge streets do not occupy these world coordinates. Earlier core-only correction and the `market-main`→`upper-shoulder` tapered ownership seam removed real competing defects but did not remove this rectangle.

## Action
On `upper-shoulder` only, sample the natural west edge every 5 dm and emit one local x-axis shoulder transition per strip. Other district terraces retain the existing single-edge behavior. Raise only this terrace's bounded primitive budget from 40 to 96.

`KentridgeTerraceSeamRegressionTests.SceneIssue20260826132234356UpperWestShoulderFollowsLocalTerrainWithoutMetreScaleSteps` requires all 40 strips to cover the 200 dm edge contiguously, limits each plan-view step to 0.5 m, and verifies each emitted ramp's outer endpoint equals `TerrainQuery` at its production-seed local sample. It also checks the budget change does not leak to `market-main`.

## Blast radius / cost
One authored west transition is changed. Maximum work is 40 baked local strips (roughly 93 terrain primitives worst case) for one static feature; no per-frame work and no global terrain behavior changes.

## Verdict / falsifier
Pending exact-SHA targeted CI and a fresh saved-camera bake/replay. If either marked Dirt/grass contact still has a metre-scale rectangular or stair-step discontinuity, the hypothesis is false and the capture remains open.
