# New House WorldBuilder Implementation Plan

## Acceptance
Reconstruct the supplied medieval cottage through the production WorldBuilder/material/rendering path at 10 cm voxel scale. Final standalone-player evidence must preserve the tall near-frontal silhouette, dominant steep front gable, blue roof/shutters, stone lower storey/chimney, Tudor timber/plaster upper storeys, stacked arched openings, ridge finial, flower boxes/ivy, credible texture scale, grounding, and clean roof/wall/material transitions. Garage/driveway checklist items are `N/A — absent from supplied reference`.

## Ownership / architecture
- `Assets/Game/WorldBuilder` owns reusable `NewHouseReferenceAuthoring` over `IStructureAuthoringSession`; reference site/camera/light stay outside it.
- `Assets/Game/Materials` owns stable material identity/projection; Rendering receives semantic-free texture slots from WorldBuilder `Resources/VoxelAdditionalTextureLayers.asset` without project-global renderer edits.
- Module-local proof is WorldBuilder `NewHouseReferenceReconstruction` plus Rendering `TextureLayers`; bounded Structures authoring is published through the application composition root before renderer binding.

## Material results
1. Direct reference comparison falsified the original broad/side-gabled massing; the current authored form uses the tall front gable, stacked arched openings, open shutters, chimney, finial, flower boxes, and right-heavy ivy from the supplied image.
2. Runs `33948973165`/`33949596796` falsified putting extra textures in `Assets/Settings/VoxelUniversalRenderer.asset`; application-owned Resources slots avoid global validation blast radius.
3. Runs `33951274739`, `33952976056`, `33953740353`, and `33954740928` isolated unrelated showcase streaming, incomplete game-material palette binding, and the missing bounded-authoring publication boundary. Those corrections were required, but captures still showed fractured terrain/no recognizable house.
4. Exact-SHA diagnostic run `33960811414` on feature `51deddcb21c52810145e746f886ee1903f7881dc` passed all derived module/player gates and the focused replay. At t=5-30 s the focused world reported complete coverage (`missingVisible=0`) and rising GPU publications, while GPU `count/write/copy=0`, CPU arena usage stayed zero, and per-ring `drawn=0`. The remaining visual failure is downstream of WorldBuilder authoring/storage/material publication.

## Current blocker / selected path
Current `origin/master` (`af61066de669431a6555e737887bd5d4031525b8`) still carries the production GPU missing-surface defect tracked by `SceneIssues/open/20260902-171853-000-GpuRendererProductionRestoration`. Renderer exact run `33965331065` on feature `34c2f2bdaec77a2fcba05a1d0c4950a7867a66e6` moved past the prior desired-generation-read mismatch, but remains red at allocation: the focused diagnostic and reclamation stress fixture both require exhaustion (`1`) and receive `0`, while the production multi-chunk oracle still diverges at page-allocation/current-generation ownership (`2` vs `0`). Per `feature-readme.md`, do not substitute CPU/test-only rendering or another house-scoped speculative fix. Keep this feature open and resume visual acceptance when the renderer restoration reaches master.

Current feature head before this refresh: `5e13b92c4d8e62e4e4fd123ffc5423f400c99082`.

## Remaining gates
1. Merge the renderer prerequisite from current `origin/master` when available and reconcile only in-scope conflicts.
2. Re-run exact-SHA module + standalone validation; inspect frontal, front-left, and rear-right captures directly against the supplied reference.
3. Fix only demonstrated house visual defects and complete every remaining checklist/acceptance item.
4. Run final exact-SHA validation, close `open/`→`closed/`, merge current master, open PR to `master`, enable auto-merge, and monitor the required `affected` gate until merged. Never push the feature head directly to `master`.
