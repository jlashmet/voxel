# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Observed reference / acceptance
Target: tall ornate three-register house; pale stone lower storey; deep round portal; narrow side arches; plaster/timber middle register with compact blue shutters; broad swept blue portrait gable with small upper arch/flowers; transverse shoulders; compact crest; tall left chimney; natural ivy/flower boxes; blue-gold banner and bracketed sign. Exact built-player target/audits must be **production-quality** and very close in silhouette, proportions, openings, detail hierarchy, materials, and presentation, with no holes, unsupported pieces, overlap, or flicker.

## Material results / root causes
Iterations 1–15 repaired ordering, shell holes, roof seams, upper flowers, spring-line width, chimney, middle opening, portal depth/relief, and test discovery. Iteration 16 exact feature `dc4217a8500fdcc88ca3398f42de44666e770a9d`, request `c2ba4d220564bd61f247a99682f696c15f4af051`, run `34198674000`, artifact `single-test-34198674000` passed: focused FQN executed non-zero (`8.28s`), all repository-derived module tests/players, WorldBuilder module-local player, SceneIssue 32s replay, and canonical `KentridgePlayableSlice` passed. Direct pinned-reference/target/front-left/rear-right review confirms the needle-spire and obsolete rear triangle are corrected while portal/openings/chimney/crest/shell survive. Visual closure is still rejected as **prototype/blockout quality**: facade ivy reads as tall segmented green columns rather than irregular wall-hugging growth.

## Competing hypotheses / discriminating source inspection
1. **Favored:** two production ivy passes (`NewHouseReferenceAuthoring.AddIvy` and `NewHouseReferenceRefinement.AddIvyMass`) both emit tightly spaced overlapping rectangular clusters, producing continuous vertical columns.
2. **Rejected:** foliage material/color is primary. Exact frames show the geometric repetition/continuous vertical mass independent of material identity.

## Selected fix / remaining gates
Iteration 17 changes only facade ivy geometry. Keep roof, portal, windows, materials, flowers, site, camera/light, chimney, banner/sign, and shell fixed. Replace both dense ladder-style ivy emitters with deterministic sparse wall-hugging clusters: lower depth, smaller leaves, irregular lateral drift/branching, and vertical gaps while preserving translation invariance and semantic `p.Foliage`. Add one focused final-path behavioral regression that rejects tall/deep column primitives and proves side growth spans multiple lateral positions with gaps. Then exact-SHA CI + module/player replay + direct reference/target/front-left/rear-right inspection. If still below production-quality, record only the next largest demonstrated mismatch. Finish every `tasks.md` item before open→closed, master reconciliation, PR + auto-merge; never use `pending/` or direct-push master.
