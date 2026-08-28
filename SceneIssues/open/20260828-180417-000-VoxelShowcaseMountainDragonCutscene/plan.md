# Plan

## Evidence
- Note-only issue: `captures` is empty, so there are no screenshots/marked regions to inspect and no marked region is omitted; the note is the complete repro/acceptance contract.
- Shared voxel structures, WorldBuilder sites/story bindings, Campaign runtime, and CutsceneRunner already cover generation/composition/dialogue; the missing shared seam was authored site proximity, not a new scene-local interaction system.
- Exact-source CI on `4431b3765a5651ce0682939cb1bde257ba637bde` built/baked VoxelShowcase and completed the 1600x900, 30 s real-player replay, but the focused regression failed at the final switchback landing invariant. This isolates the remaining defect to generated path topology rather than Showcase startup/runtime integration.
- Primitive ramp rasterization confirms the failure is causal: the last X ramp reaches its high surface at its extreme end while the following shallow Z ramp has no top-surface voxels across its first integer-slope columns; unlike earlier turns, that direction change had no explicit flat path landing.

## Competing hypotheses
1. **Missing mountain/cutscene systems.** Rejected: production shared systems exist and the built-player replay starts cleanly.
2. **Regression is overly strict.** Rejected: the final X→Z transition is uniquely missing the flat path-material landing already emitted at earlier turns, and primitive rasterization leaves only a narrow edge connection.
3. **Missing reusable proximity seam plus authored encounter, followed by one topology defect.** Supported: site proximity is now shared WorldBuilder/Story/Campaign behavior; the remaining repair is one bounded landing primitive in the reusable mountain generator.

## Fix / verification
- Keep VoxelShowcase limited to parameters/materials/composition; WorldBuilder owns mountain/path/placeholder geometry and proximity, Story/Campaign own semantic dispatch, and Cutscenes own dialogue execution.
- Add the same explicit flat path landing at the final X→Z turn that earlier switchbacks use; do not weaken the behavioral regression.
- Focused regression: `VoxelEngine.Tests.PlayMode.MountainDragonProductionAcceptanceTests.MountainPathDragonAndProximityFlowUseProductionWorldBuilder` must verify substantial mountain mass, shallow connected ascent, summit connection, grounded red placeholder, production Showcase composition, proximity dispatch, one-shot cutscene, and exact dialogue.
- Final validation must be green exact-SHA targeted CI plus the production `Assets/Scenes/VoxelShowcase.unity` scene replay; tests alone are insufficient.

## Blast radius / cost
- Shared proximity evaluation is allocation-free after construction, O(configured triggers) per update, and skips completed one-shot triggers; Story remains position-agnostic.
- Landmark generation is world-build-only and bounded to the authored footprint. The topology repair adds one fixed `PathWidth x 1 x PathWidth` path box (30x1x30 with Showcase parameters), automatically counted by the feature instruction budget; there is no new per-frame voxel-generation cost.
