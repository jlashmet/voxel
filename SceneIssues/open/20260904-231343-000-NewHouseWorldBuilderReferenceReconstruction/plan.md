# New House WorldBuilder Implementation Plan

## Acceptance
Reconstruct the supplied medieval cottage through the production WorldBuilder/material/rendering path at 10 cm voxel scale. Final standalone-player evidence must preserve the tall near-frontal silhouette, dominant steep front gable, blue roof/shutters, stone lower storey/chimney, Tudor timber/plaster upper storeys, stacked arched openings, ridge finial, flower boxes/ivy, credible texture scale, grounding, and clean roof/wall/material transitions. Garage/driveway checklist items are `N/A — absent from supplied reference`.

## Ownership / architecture
- Runtime owner is `Assets/Game/WorldBuilder`; reusable geometry is `NewHouseReferenceAuthoring` over `IStructureAuthoringSession`.
- Reference site/camera/light policy stays outside reusable geometry.
- Game material identity/projection stays in `Assets/Game/Materials`; Rendering receives semantic-free texture slots.
- WorldBuilder supplies the six reference textures through `Resources/VoxelAdditionalTextureLayers.asset`; the generic renderer consumes them without project-global renderer edits.
- Module-local player proof is WorldBuilder `NewHouseReferenceReconstruction` plus Rendering `TextureLayers` validation.
- Bulk Structures authoring mutates resident storage but does not own the world's change journal; the application composition root publishes the completed bounded authoring phase before renderer binding.

## Hypotheses / discriminating results
1. **Existing massing was close; only material/camera polish remained.** Falsified by direct comparison with checked-in reference blob `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`: prior shape was broad/side-gabled with rectangular windows and wrong camera.
2. **Extra textures belonged in `Assets/Settings/VoxelUniversalRenderer.asset`.** Falsified by runs `33948973165`/`33949596796`: the global asset broadened module validation and inherited an unrelated URP legacy-Input failure. Selected path is the application-owned Resources slot asset.
3. **The first green built-player replay proved the focused validation was visually stable.** Falsified by run `33951274739`: captures showed fragmented terrain/house while logs repeatedly reported showcase castle authoring failures. `NewHouseReferenceWorldBuilderValidation.Update` was incorrectly calling `ShowcaseWorld.StepStreaming`, which admits unrelated landmarks and mutates residency as the audit camera moves.
4. **Removing integration streaming was sufficient to make authored house surfaces render.** Falsified by exact-SHA run `33952976056`: automatic module validation and the standalone replay passed, the castle failure disappeared, but direct hero/audit inspection still showed terrain only. The validation was constructing the legacy four-argument `ShowcaseWorld`, whose private world simulation palette does not register stable house IDs 23-28 even though raw authored voxels can be written/read successfully.
5. **Complete game-material binding alone was sufficient.** Falsified by exact-SHA run `33953740353`: the complete palette passed all automated validation, yet hero/audit captures still showed terrain only. Inspection of `StructuresComposition` and `IVoxelStorageRuntime` identified the missing publication boundary: `CreateAuthoringSession` intentionally returns a raw mutation capability, while bounded production helpers explicitly call `PublishAllResidentRegions()` after authoring. The house proof never published its completed writes, so rendering continued from the pre-authoring terrain journal state.

## Selected fix
Keep the deterministic preloaded production storage footprint and complete game-material binding, then publish the completed house/site authoring phase through the game composition root before `RenderingComposition.ConfigureWorld`. `ShowcaseWorld.PublishStructureAuthoringChanges()` delegates to the existing production `StorageRuntimeLifetime.PublishAllResidentRegions()` journal boundary; no renderer, storage, material registry, mesh, or structure-authoring bypass is introduced. The focused proof still does not advance showcase landmark streaming, and the player scenario continues to forbid the previously observed castle failure.

Current implementation basis: authoring publication fix at `cdfca5d33d96bda295bb460700cc1adb8183f1f5`.

## Remaining gates
1. Exact-SHA targeted CI on the current feature head; inspect the WorldBuilder hero/audit captures directly.
2. Fix only demonstrated remaining silhouette/material/roof/opening/grounding defects and complete every visual checklist item.
3. Record final exact-SHA evidence, close `open/`→`closed/`, reconcile current `origin/master`, then PR to `master` + auto-merge. Never push the feature head directly to `master`.
