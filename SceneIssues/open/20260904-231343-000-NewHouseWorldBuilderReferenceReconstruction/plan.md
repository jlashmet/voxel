# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Observed reference / acceptance
The target is a tall ornate three-register house: pale stone lower storey; deep round-arched central portal with pronounced voussoir ring/recessed timber door; narrow lower side windows; plaster/timber middle register with compact blue-shuttered arch; broad swept blue portrait gable with smaller arch/dense flowers; transverse roof shoulders; compact crest; tall left masonry chimney; natural flower boxes/ivy; blue-gold banner and bracketed sign. Exact target/audits must be **production-quality** and very close in silhouette, proportions, openings, detail hierarchy, materials, and presentation, without holes, floating pieces, overlaps, or z-fighting.

## Material results / root causes
Iterations 1–14 repaired ordering, shell holes, swept-gable seams, upper flowers, spring-line width, tall chimney, middle-opening scale, portal depth, and focused-test discovery. Iteration 15 exact feature `29edfec6ce93ee7f2a247fdef62c2641ed5c8890`, request `90313bd07435dd5c223bc04673126b0e64dafc45`, run `34191643674`, artifact `single-test-34191643674` passed: focused FQN executed non-zero (`8.66s`), all derived module tests/players, WorldBuilder module-local validation, SceneIssue 32s replay, and canonical `KentridgePlayableSlice` passed. Direct reference/target/front-left/rear-right review shows portal relief now reads materially better, but exact target remains **prototype/blockout quality** because the portrait roof is still a needle-spire; rear-right also exposes a giant plaster triangle.

## Competing hypotheses / discriminating source inspection
1. **Favored:** `RebuildSweptPortraitShell` rebuilds from `portraitEave` to the obsolete global ridge (53 voxels default), creating the wrong height/width ratio. The rear protrusion is independently authored by `FinishAuditElevations.FillRearGable`, also tied to global ridge.
2. **Rejected:** camera/FOV is primary. The exact front target itself has the wrong authored ratio.

Source inspection also falsified the initial “shallow finish clear leaves legacy front-gable depth” theory: the earlier refiner front gable ends at `o.z + 15`, while the late finish clear reaches about `o.z + 17` in the default, so that geometry was already removed. The rear artifact is the separate audit `FillRearGable` patch.

## Selected fix / remaining gates
Iteration 16 keeps portal/openings/materials/site/camera/light/chimney/planting/hanging details/lower roofs fixed. The late finish clear still removes the entire old high portrait range, but rebuilds only a width-driven lower swept rise; the compact crest follows that apex. A late shallow rear-only carve removes only the obsolete triangle above the rear wall/window/shoulder-roof zone. Existing production-path behavioral tests now prove lowered apex, no surviving high roof edge, nonlinear/seam-closed profile, restored high opening, rear-triangle removal, and lowered crest. Next gate: exact-SHA CI, then direct exact standalone visual comparison. Finish every `tasks.md` item before open→closed, master reconciliation, PR + auto-merge; never use `pending/` or direct-push master.
