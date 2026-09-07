# Experiment 001 — evidence-driver compile

## Hypothesis
The first exact-SHA gate may expose a product integration error in the new built-player evidence driver even though the physical planner/catalogue code is structurally complete.

## Action / source
- Feature source SHA: `b0583ff734a7517f6be2992382e48f92609d4236`
- CI request SHA: `4424c2eaa328e573eea12a971a2c493b970a0f93`
- Workflow run: `33230924543`
- Target: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody` plus assigned Kentridge scene replay.

## Result
Product compile failure before test/player execution:
`KentridgeMacroWorldEvidenceDriver.cs(184,13): CS0246 TopDownWorldLayout could not be found`.
The same compiler error prevented the built-player scene build. No additional compiler error was reported before Unity aborted.

## Verdict / next step
Product failure, not infrastructure. `TopDownWorldLayout` is declared in `Game.WorldBuilder.Api`; add that missing import, keep the feature open, and validate the repaired exact feature SHA through the same assigned CI transport. The import fix landed in `339ca94f593653e84a02fe2d19712971bfd99e20`.
