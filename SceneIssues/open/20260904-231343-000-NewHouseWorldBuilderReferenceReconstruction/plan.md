# New House WorldBuilder Implementation Plan

## Acceptance
Reconstruct the supplied compact medieval cottage through the production WorldBuilder/material/rendering path at 10 cm voxel scale. Final built-player evidence must preserve the near-frontal silhouette, steep blue front gable, pale stone lower storey/chimney, brown Tudor timber with light plaster, compact central brown door, blue-framed lower windows and upper shutter bank, small high gable window/ridge ornaments, flower boxes/ivy, believable texture scale, grounding, and clean roof/wall/material transitions. Garage/driveway items are `N/A — absent from supplied reference`.

## Ownership / architecture
- `Assets/Game/WorldBuilder` owns reusable `NewHouseReferenceAuthoring` over `IStructureAuthoringSession`; site/camera/light remain separate.
- `Assets/Game/Materials` owns stable material identity and presentation; Rendering receives semantic-free texture layers through the existing additional-layer resource.
- Module-local proof is WorldBuilder `NewHouseReferenceReconstruction` plus Rendering `TextureLayers`; bounded Structures writes are published before renderer binding.
- Visual proof uses the repository-supported production CPU fallback (`VOXEL_DISABLE_GPU_CUTOVER=1`), not a test renderer and not the unrelated GPU-restoration assignment.

## Material results / discriminating evidence
1. Earlier terrain-only captures were traced to showcase streaming, incomplete game-material palette binding, missing bounded-authoring publication, and then the default GPU cutover. Those causes are fixed/falsified; exact runs now show complete CPU-rendered house geometry.
2. Direct comparison falsified the earlier oversized/arched interpretation. The current 88x60 footprint uses a compact rectangular entry/lower windows, a four-leaf upper blue bank, small high arched window, smaller chimney/finials, restrained flowers/ivy, and wider reference framing.
3. Run `33994976147` exposed wrong supplied texture-role ordering; the additional layers were remapped by visual role and the unrelated TextureLayers water assertion was removed from that focused proof.
4. Runs `33996415142` and `33998165969` passed all automatic/module/standalone gates. Direct inspection still found the blue-painted accent plate rendering charcoal. Hypothesis A (wrong texture slot) was falsified: `HouseDoor` samples the intended supplied blue/gold layer. Hypothesis B was confirmed: its brown Albedo multiplier crushed the authored blue chroma. The production `HouseDoor` presentation now uses neutral Albedo, with `PaintedHouseAccent_PreservesAuthoredBlueChroma` locking that invariant.

## Selected path
Validate feature head `5bce84d3ed88c2eb08473463b243fecfe0b3b34f` exactly. Inspect frontal plus module-owned front-left/rear-right captures against the supplied reference; make no further changes unless a demonstrated acceptance defect remains.

## Remaining gates
1. Final exact-SHA module + standalone validation and direct visual inspection.
2. Complete every remaining task/acceptance checkbox from that evidence.
3. Move only this SceneIssue `open/`→`closed/` with fixed metadata.
4. Merge current `origin/master` into `fixes/agent-5`, open PR, enable auto-merge, and monitor required `affected` gate until merged. Never push the feature head directly to `master`.
