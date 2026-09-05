# New House WorldBuilder Implementation Plan

## Acceptance
Reconstruct the supplied medieval cottage through the production WorldBuilder/material/rendering path at 10 cm voxel scale. Final standalone-player evidence must preserve the tall near-frontal silhouette, dominant steep front gable, blue roof/shutters, stone lower storey/chimney, Tudor timber/plaster upper storeys, stacked arched openings, ridge finial, flower boxes/ivy, credible texture scale, grounding, and clean roof/wall/material transitions. Garage/driveway checklist items are `N/A — absent from supplied reference`.

## Ownership / architecture
- Runtime owner is `Assets/Game/WorldBuilder`; reusable geometry is `NewHouseReferenceAuthoring` over `IStructureAuthoringSession`.
- Reference site/camera/light policy stays outside reusable geometry.
- Game material identity/projection stays in `Assets/Game/Materials`; Rendering receives semantic-free texture slots.
- WorldBuilder supplies the six reference textures through `Resources/VoxelAdditionalTextureLayers.asset`; the generic renderer consumes them without project-global renderer edits.
- Module-local player proof is WorldBuilder `NewHouseReferenceReconstruction` plus Rendering `TextureLayers` validation.

## Hypotheses / discriminating results
1. **Existing massing was close; only material/camera polish remained.** Falsified by direct comparison with checked-in reference blob `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`: prior shape was broad/side-gabled with rectangular windows and wrong camera.
2. **Extra textures belonged in `Assets/Settings/VoxelUniversalRenderer.asset`.** Falsified by runs `33948973165`/`33949596796`: the global asset broadened module validation and inherited an unrelated URP legacy-Input failure. Selected path is the application-owned Resources slot asset.
3. **The first green built-player replay proved the focused validation was visually stable.** Falsified by run `33951274739`: captures showed fragmented terrain/house while logs repeatedly reported showcase castle authoring failures. `NewHouseReferenceWorldBuilderValidation.Update` was incorrectly calling `ShowcaseWorld.StepStreaming`, which admits unrelated landmarks and mutates residency as the audit camera moves.

## Selected fix
Keep the already-built reference house on its deterministic 3x3 production storage footprint after synchronous preload/authoring. The focused validation no longer advances showcase integration streaming; only the evidence camera moves. The player scenario now forbids `Castle authoring exceeded`, so the demonstrated failure cannot silently recur. No alternate storage, renderer, mesh, or authoring path is introduced.

Current implementation basis: `c29d1aa43327b109ba7e70376e7bd5105dba3a41` plus task-record update.

## Remaining gates
1. Exact-SHA targeted CI on the final feature head; inspect the WorldBuilder hero/audit captures directly.
2. Fix only demonstrated remaining silhouette/material/roof/opening/grounding defects and complete every visual checklist item.
3. Record final exact-SHA evidence, close `open/`→`closed/`, merge current `origin/master`, then PR to `master` + auto-merge. Never push the feature head directly to `master`.
