# Plan

## Evidence
- Note-only issue: `captures` is empty, so there are no screenshots/marked regions to inspect or replay; the note is the complete repro/acceptance contract.
- Shared voxel structures, WorldBuilder sites/story bindings, Campaign runtime, and CutsceneRunner already covered generation/composition/dialogue; the missing shared seam was authored site proximity.
- Exact-source CI on `4431b3765a5651ce0682939cb1bde257ba637bde` built/replayed VoxelShowcase but failed the focused regression at the final switchback landing invariant. Primitive ramp rasterization confirmed a real topology defect: the final X→Z turn uniquely lacked the flat path landing used by earlier turns.
- Source `0168881859b560e75033e993809480f512916fae` passed the focused regression and the 1600x900, 30 s real-player VoxelShowcase harness in run `33222414785`, attempt 2. Attempt 1 passed both substantive gates but hit the workflow's five-minute whole-job limit after a cold bake; the single infrastructure retry restored that bake from cache and completed green.

## Competing hypotheses
1. **Missing mountain/cutscene systems.** Rejected: production shared systems existed and the built-player replay starts cleanly.
2. **Regression was overly strict.** Rejected: the final transition was uniquely missing a path-material landing and integer ramp rasterization left only a narrow edge connection.
3. **Missing reusable proximity seam plus authored encounter, followed by one topology defect.** Supported and fixed with shared proximity/story composition plus one bounded final landing primitive.

## Fix / verification
- VoxelShowcase remains limited to parameters/materials/composition; WorldBuilder owns mountain/path/placeholder geometry and proximity, Story/Campaign own semantic dispatch, and Cutscenes own dialogue execution.
- Behavioral regression `VoxelEngine.Tests.PlayMode.MountainDragonProductionAcceptanceTests.MountainPathDragonAndProximityFlowUseProductionWorldBuilder` verifies substantial mountain mass, shallow connected ascent, summit connection, grounded red placeholder, production Showcase composition, proximity dispatch, one-shot cutscene, and exact dialogue.
- Exact-SHA targeted CI and the production `Assets/Scenes/VoxelShowcase.unity` real-player harness are green; no captured poses remain to replay.

## Blast radius / cost
- Shared proximity evaluation is allocation-free after construction, O(configured triggers) per update, and skips completed one-shot triggers; Story remains position-agnostic.
- Landmark generation is world-build-only and bounded to the authored footprint. The topology repair adds one fixed `PathWidth x 1 x PathWidth` path box (30x1x30 here), with no new per-frame voxel-generation cost.
