# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Observed reference / acceptance
The target is a tall ornate three-register house: pale stone lower storey; deep round-arched central portal with pronounced voussoir ring and recessed timber door; narrow lower side windows; plaster/timber middle register with compact blue-shuttered arch; broad swept blue portrait gable with smaller arch and dense flowers; transverse roof shoulders; compact crest; tall left masonry chimney; natural flower boxes/ivy; blue-gold banner and bracketed sign. Final target/audits must be **production-quality** and very close in silhouette, proportions, openings, detail hierarchy, materials, and presentation, with no holes, floating pieces, overlaps, or z-fighting.

## Material results / root causes
Iterations 1–13 repaired ordering, shell holes, swept-gable seams, upper flowers, spring-line width, tall chimney, and middle-opening scale. Iteration 14 added a recessed timber door/projecting stone surround and repaired focused-test discovery. Exact feature `f33995a5877e67e1606ea043ace621435b78f32f`, run `34187949123` then proved non-zero focus/module/player success but exact visual review still rejected the lower ring as too flat.

Iteration 15 feature `29edfec6ce93ee7f2a247fdef62c2641ed5c8890`, request `90313bd07435dd5c223bc04673126b0e64dafc45`, run `34191643674`, artifact `single-test-34191643674` passed. The focused portal FQN executed non-zero (`8.66s`), all derived module tests/players passed, WorldBuilder module-local validation and the 32s SceneIssue replay succeeded, and canonical `KentridgePlayableSlice` passed. Direct pinned-reference/target/front-left/rear-right inspection shows the continuous portal relief now reads materially better and the shell remains closed, but the exact target is still **prototype/blockout quality**.

## Competing hypotheses / next experiment
1. **Favored:** portrait-roof authored proportions are now dominant. `RebuildSweptPortraitShell` rebuilds from `portraitEave` to the global ridge (53 voxels in the default), producing a needle-spire versus the reference's broader/lower swept gable; the shallow clear also leaves full-height legacy front-gable depth visible in the rear audit.
2. **Rejected as primary:** camera/FOV exaggerates the height. The exact front target itself shows excessive gable rise relative to facade width, so framing cannot explain the authored ratio.

## Selected fix / remaining gates
Keep portal, openings, materials, site, camera/light, chimney, planting, banner/sign, lower roofs, and facade details fixed. Clear the complete old front-gable footprint while staying well ahead of the rear shell; rebuild only a lower width-driven portrait rise and anchor its crest to that rebuilt apex. Extend the existing behavioral swept-profile regression to prove full legacy clear, lower apex, nonlinear/seam-closed edges, restored high opening, and crest placement. Then run exact-SHA CI and directly compare the exact standalone target/front-left/rear-right frames again. Finish every `tasks.md` item before `open/`→`closed/`, current-master reconciliation, PR + auto-merge; never use `pending/` or direct-push master.
