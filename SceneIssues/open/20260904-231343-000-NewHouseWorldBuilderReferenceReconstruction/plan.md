# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Green CI is not visual acceptance.

## Observed reference / acceptance
Target: tall ornate three-register house; pale stone lower storey; deep round portal; narrow lower arches; plaster/timber middle register with compact blue-shuttered arch; broad swept blue portrait gable with smaller upper arch/flowers; transverse shoulders; compact crest; tall left chimney; natural planting; blue-gold banner and bracketed sign. Exact built-player evidence must be **production-quality** and very close in silhouette, proportions, opening hierarchy, detail, materials, and presentation with no holes, unsupported pieces, overlap, or flicker.

## Material results / root causes
Iteration 17 exact feature `58b3cae158c0ded3d41cb648fc062bcdf47006a1`, request `955c061feff237e9895e9cdecbb805ad84248d24`, run `34206000148`, artifact `single-test-34206000148` / ID `10051474910` passed. Focused FQN `AuthorHouse_FinalIvyUsesSparseWallHuggingClustersWithoutColumnPrimitives` executed non-zero (`8.81s`); all repository-derived module tests/players, WorldBuilder module-local player, SceneIssue 32s replay, and canonical `KentridgePlayableSlice` passed. Direct reference/target/front-left/rear-right inspection confirms the tall ivy columns are gone and roof/portal/chimney/shell survive. Closure remains rejected as **prototype/blockout quality**.

## Competing hypotheses / discriminating source inspection
1. **Favored:** the largest remaining facade mismatch is an authored oversized middle-storey timber cage. `NewHouseReferenceFinishPass.RefineMiddleFacadeOpening` draws a 39x31 outer rectangle plus 25-voxel side posts/diagonals around the compact arch; the pinned reference has plaster around the compact arch/shutters with horizontal belts, not a giant rectangular frame.
2. **Rejected:** camera/material is primary for this mismatch. The exact frontal frame shows the oversized rectangle geometrically even with otherwise readable materials.

## Selected fix / remaining gates
Iteration 18 changes only the late middle-facade outer framing: preserve the plaster repair, 13x19 arched glass opening, compact shutters, flower box, roof, portal, ivy, site, camera/light, chimney, banner/sign, and shell; remove the oversized cage/diagonals so the compact arch owns the hierarchy. Add a focused production-path regression proving the final middle facade has no tall outer timber cage while the compact arch/shutters/refill remain correctly ordered. Then exact-SHA CI + module/player replay + direct reference/target/front-left/rear-right inspection. If still below production-quality, record only the next largest demonstrated mismatch. Finish every `tasks.md` item before open→closed, master reconciliation, PR + auto-merge; never use `pending/` or direct-push master.
