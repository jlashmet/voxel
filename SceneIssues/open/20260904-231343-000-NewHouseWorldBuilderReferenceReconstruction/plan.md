# New House WorldBuilder Implementation Plan

## Acceptance
Reconstruct the supplied compact medieval cottage through the production WorldBuilder/material/rendering path at 10 cm voxel scale. Final built-player evidence must preserve the near-frontal silhouette, steep blue front gable, pale stone lower storey/chimney, brown Tudor timber with light plaster, compact central brown door, blue-framed lower windows and upper shutter bank, small high gable window/ridge ornaments, flower boxes/ivy, believable texture scale, grounding, and clean roof/wall/material transitions. Garage/driveway items are `N/A — absent from supplied reference`.

## Ownership / architecture
- `Assets/Game/WorldBuilder` owns reusable `NewHouseReferenceAuthoring` over `IStructureAuthoringSession`; site/camera/light remain separate.
- `Assets/Game/Materials` owns stable material identity/presentation; Rendering receives semantic-free texture layers through the existing additional-layer resource.
- Module-local proof is WorldBuilder `NewHouseReferenceReconstruction` plus Rendering `TextureLayers`; bounded Structures writes publish before renderer binding.
- Visual proof uses the supported production CPU fallback (`VOXEL_DISABLE_GPU_CUTOVER=1`), not a test renderer or the unrelated GPU-restoration assignment.

## Material results / discriminating evidence
1. Earlier terrain-only captures were traced to showcase streaming, incomplete game-material palette binding, missing bounded-authoring publication, and default GPU cutover. Exact runs now show complete CPU-rendered house geometry.
2. Direct comparison falsified the oversized/arched interpretation; current massing/openings/details were rebuilt to the compact supplied reference.
3. Run `33994976147` exposed wrong supplied texture-role ordering; layers were remapped and Rendering `TextureLayers` proof was scoped to its own readiness assertion.
4. Runs `33996415142`, `33998165969`, and `33999873622` passed automation but direct inspection rejected the painted accents. The neutral-Albedo hypothesis was falsified by `33999873622`: the supplied `HouseDoor` plate itself is dark/gold. `SmoothSurface.shader` provides an existing luminance-only path that preserves texture value/detail over game-owned colour. `HouseDoor` now uses saturated blue Albedo with luminance detail and zero texture chroma; `PaintedHouseAccent_UsesBlueBaseWithSuppliedLuminanceDetail` locks the contract.

## Selected path
Validate exact feature head `a4ac20404aaeb68057b76c3eb0ab076b68ef8a59`. Inspect frontal, front-left, and rear-right built-player captures directly; make no further change unless a demonstrated acceptance defect remains.

## Remaining gates
1. Final exact-SHA module + standalone validation and direct visual inspection.
2. Complete every remaining task/acceptance checkbox from that evidence.
3. Move only this SceneIssue `open/`→`closed/` with fixed metadata.
4. Merge current `origin/master` into `fixes/agent-5`, open PR, enable auto-merge, and monitor required `affected` gate until merged. Never push the feature head directly to `master`.
