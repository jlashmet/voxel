# New House WorldBuilder Implementation Plan

## Acceptance
Reconstruct the supplied medieval cottage through the production WorldBuilder/material/rendering path at 10 cm voxel scale. Final standalone-player evidence must preserve the tall near-frontal silhouette, dominant steep front gable, blue roof/shutters, stone lower storey/chimney, Tudor timber/plaster upper storeys, stacked arched openings, ridge finial, flower boxes/ivy, credible texture scale, grounding, and clean roof/wall/material transitions. Garage/driveway checklist items are `N/A — absent from supplied reference`.

## Ownership / architecture
- `Assets/Game/WorldBuilder` owns reusable `NewHouseReferenceAuthoring` over `IStructureAuthoringSession`; reference site/camera/light stay outside it.
- `Assets/Game/Materials` owns stable material identity/projection; Rendering receives semantic-free texture slots from WorldBuilder `Resources/VoxelAdditionalTextureLayers.asset` without project-global renderer edits.
- Module-local proof is WorldBuilder `NewHouseReferenceReconstruction` plus Rendering `TextureLayers`; bounded Structures authoring is published through the application composition root before renderer binding.
- This feature's visual proof uses the repository's supported production CPU fallback (`VOXEL_DISABLE_GPU_CUTOVER=1`). It does not depend on the separate GPU-restoration SceneIssue.

## Material results
1. Direct reference comparison falsified the original broad/side-gabled massing; the current authored form uses the tall front gable, stacked arched openings, open shutters, chimney, finial, flower boxes, and right-heavy ivy from the supplied image.
2. Runs `33948973165`/`33949596796` falsified putting extra textures in `Assets/Settings/VoxelUniversalRenderer.asset`; application-owned Resources slots avoid global validation blast radius.
3. Runs `33951274739`, `33952976056`, `33953740353`, and `33954740928` isolated unrelated showcase streaming, incomplete game-material palette binding, and the missing bounded-authoring publication boundary.
4. Run `33960811414` proved the house world was authored and published but the standalone SceneIssue replay was exercising the default GPU cutover. That made the GPU restoration look like a prerequisite even though repository-owned module-player validation already launches production scenes with `VOXEL_DISABLE_GPU_CUTOVER=1`.
5. User clarification plus repository inspection falsified the GPU-prerequisite hypothesis: `GpuSurfaceProductionPolicy` explicitly defines the disable flag as the CPU emergency/A-B fallback, and `CpuTransvoxelChunkCache` uses it to keep the near rings on the CPU mesher. The focused house scene now sets that flag before its first rendered frame and logs `NEW_HOUSE_VALIDATION renderer=cpu-fallback`; its module scenario requires the marker.

## Selected path
Re-run the exact-SHA built-player proof on the CPU renderer now. Inspect the portrait hero and audit captures directly against the supplied reference, then make only demonstrated house visual corrections. Do not wait for or merge agent-1's GPU restoration merely to complete this feature.

Current feature head before exact-SHA request: `7e3c31f437fd453a082728ad526ed6403ecf49cd`.

## Remaining gates
1. Re-run exact-SHA module + standalone validation with the CPU fallback active; inspect frontal, front-left, and rear-right captures directly against the supplied reference.
2. Fix only demonstrated house visual defects and complete every remaining checklist/acceptance item.
3. Run final exact-SHA validation, close `open/`→`closed/`, fetch/merge current master, open PR to `master`, enable auto-merge, and monitor the required `affected` gate until merged. Never push the feature head directly to `master`.
