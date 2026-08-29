# Experiment 019 — foundation regression compile failure

## Hypothesis
The foundation-footprint candidate could not be evaluated because the new behavioral regression was missing world-generation namespace imports; this is a test compilation defect, not evidence against the production geometry.

## Action / source
Workflow `33225920872` requested the exact VoxelShowcase PlayMode regression from source `c4f4ec8b737d94be5164bc60072fa102d3e15e21`. The Showcase bake and real-player build both stopped at script compilation.

## Result
`KentridgePlotSurfaceSceneIssueRegressionTests.cs` failed with CS0246 for `SettlementPlan`, `BuildingPlot`, and `Int3`. Those types are world-generation contracts; adding `using MountingForce.WorldGen;` is sufficient and does not change the production candidate.

## Verdict / next step
Product test-harness defect. Repair the import, keep the realized-foundation geometry unchanged, refresh current master, and run the exact focused regression plus built-player replay again.
