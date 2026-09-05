# New House WorldBuilder Implementation Plan

## Acceptance
Reconstruct the supplied medieval cottage reference through the production WorldBuilder/material/rendering path at the project 10 cm voxel scale. Final standalone-player evidence must preserve the tall near-frontal silhouette, dominant steep front gable, blue roof/shutters, stone lower storey/chimney, Tudor timber/plaster upper storeys, stacked arched openings, ridge finial, flower boxes/ivy, credible texture scale, grounding, and clean roof/wall/material transitions. Garage/driveway items from the imported generic checklist are `N/A — absent from supplied reference`.

## Ownership / architecture
- Runtime house owner: `Assets/Game/WorldBuilder`; reusable geometry is `NewHouseReferenceAuthoring` over `IStructureAuthoringSession`.
- Reference-specific site/camera/light remain outside reusable geometry.
- Game material identity/projection stays in `Assets/Game/Materials`; Rendering receives semantic-free ordered texture slots only.
- WorldBuilder supplies the six reference textures through `Resources/VoxelAdditionalTextureLayers.asset`; the generic renderer loads that optional slot resource without editing project-global renderer settings.
- Module-local player proof remains WorldBuilder `NewHouseReferenceReconstruction` plus Rendering `TextureLayers` validation.

## Hypotheses / discriminating result
1. **Existing house massing was already close; remaining work was material/camera polish.** Falsified by direct comparison with the exact checked-in reference blob (`6d87b08d4c7c9bddc1705c0f34343aa79bc18423`): prior code was broad/side-gabled, split rectangular facade windows, flat shutters, and a three-quarter camera while the source is tall/front-gabled with stacked arched openings and open shutters.
2. **The six extra textures had to live in `Assets/Settings/VoxelUniversalRenderer.asset`.** Falsified by CI runs `33948973165`/`33949596796`: that unrelated global asset path forced repository-wide module fallback and inherited the known URP legacy-Input PlayMode failure. The selected fix is an application-owned Resources texture-slot asset consumed by generic Rendering; the global renderer asset is restored to `master`.

## Selected fix
Rebuild the cottage around centralized integer datums: stone lower storey, projected plaster/Tudor upper storey, dominant depth-ridged front gable, lower cross-roof shoulders, real arched carve/panel geometry, depth-bearing open shutters, chimney/finial, flower boxes/ivy, and grounded stone steps. The validation camera is portrait and frontal for direct reference comparison, then visits front-left and rear-right audit angles before returning to the hero view.

Supplemental EditMode tests retain translation invariance/site separation, material registration/projection, and renderer extra-slot bounds. No alternate storage, mesh, renderer, or structure-authoring path is introduced.

## Remaining gates
1. Run exact-SHA targeted CI after the structural/resource changes; confirm the module plan no longer falls back through global renderer settings.
2. Inspect standalone-player WorldBuilder and Rendering captures directly; classify the hero render and fix only demonstrated acceptance defects.
3. Use audit-angle frames for holes, floating pieces, overlaps, material mistakes, roof gaps, and z-fighting; complete every checklist box.
4. Close open→closed with final exact SHA/evidence, merge current `origin/master` into the feature if needed, then PR to `master` + auto-merge. Never push the feature head directly to `master`.
