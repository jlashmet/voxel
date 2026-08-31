# Experiment 011 — module-local validation adoption

## Reason
The repository adopted module-owned built-player validation because `VoxelShowcase` and Kentridge integration scenes are too large for focused module verification. Mountain Dragon now needs its own scene while production-scene replay remains an integration/acceptance gate.

## Implementation
- Added `Assets/Game/Composition/Showcase/Tests/Scenes/MountainDragonValidation.unity`.
- Added a player-safe runtime-support driver that consumes production `ShowcaseMountainDragonLayout` and `WorldBuilderMountainLandmarkCatalogue` rather than duplicating route/headroom policy.
- Added focused PlayMode coverage and `mountain-dragon.module-validation.json` plus a standalone-player scenario.
- Synced the repository integration-gate descriptor/scenario and validation-tool self-tests from master so automatic planning can attach the required Kentridge integration gate.

## Exact-CI evidence
- Run `33354762048`: dedicated Mountain Dragon focused test passed and SceneIssue standalone-player replay passed. Automatic planning failed before selection because the selective convention sync lacked the required integration-gate manifest.
- Added the exact master Kentridge integration manifest/scenario.
- Run `33355884835`: dedicated Mountain Dragon focused test passed again; planner successfully selected both `mountain-dragon` and `kentridge-integration`, including the dedicated Mountain Dragon scene. The step then failed only because `tools/tests/test_module_validation_plan.py` had not been included in the selective convention sync. SceneIssue standalone-player replay still passed.
- Synced both validation-tool self-tests from master. Exact retry `33356149526` is the next discriminator and must not be replaced while queued/running.

## Interpretation
The centered-lane production regression is independently green through the dedicated scene test. Current failures are validation-convention synchronization defects, not evidence for another Mountain Dragon geometry change. Do not alter route/support/headroom production code unless the automatic module gate produces a new product failure.
