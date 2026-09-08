# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Observed reference / acceptance
The target is a tall ornate three-register house: pale stone lower storey; deep round-arched central portal with pronounced voussoir ring and recessed timber door; narrow lower side windows; plaster/timber middle register with compact blue-shuttered arch; swept blue portrait gable with smaller arch and dense flowers; transverse roof shoulders; compact crest; tall left masonry chimney; natural flower boxes/ivy; blue-gold banner and bracketed sign. Final target/audits must be **production-quality** and very close in silhouette, proportions, openings, detail hierarchy, materials, and presentation, with no holes, floating pieces, overlaps, or z-fighting.

## Material results / root causes
Iterations 1–12 repaired ordering, shell holes, swept-gable shape/seams, upper flowers, spring-line width, and tall chimney. Iteration 13 run `34174645322` improved the compact middle arch/shutters but exact visual review rejected the flat lower entry. Iteration 14 feature `efd5765a6d755ae44527d686e6df364a7d3c3a03` added a recessed timber door and projecting stone surround; request `ccd49641a680cf39f5582249b3fca447a815d3b6`, run `34179033173`, failed because the new fixture was absent from discovery and the focused phase matched zero tests.

Explicit NUnit fixture/test metadata repaired discovery without changing production geometry. Exact feature `f33995a5877e67e1606ea043ace621435b78f32f`, request `a56b9bcd569f1d1a99e33a337cc562981d4a98a4`, run `34187949123`, artifact `single-test-34187949123` passed: the focused FQN executed non-zero (`8.42s`), automatic module validation passed, the module-local player and SceneIssue 32s replay succeeded, and exact target/front-left/rear-right frames retained a closed shell. Direct comparison still classifies the target **prototype/blockout quality**. The single largest demonstrated mismatch remains the lower portal: proud jambs exist, but the same-material arch visually collapses into the facade and reads as an angular/pilaster frame rather than the reference's continuous deep round voussoir ring.

## Selected fix / next discriminating experiment
Keep massing, roof, openings, materials, site, camera, lighting, chimney, planting, banner/sign and audit geometry fixed. Add only a thin continuous front relief lip to the existing production stone jamb/round-arch ring so its full curve produces a readable shadow edge while the timber door stays recessed. Expand the same already-discovered focused FQN to require both jamb and crown relief; do not switch regressions. New exact-SHA CI must prove the focused test executes non-zero, automatic module validation passes, and standalone replay succeeds before another visual comparison.

## Ownership / remaining gates
`Assets/Game/WorldBuilder` owns reusable authoring/refinement and module-local validation; site/camera/light remain validation composition. Finish every `tasks.md` item, then reconcile current `origin/master`, close `open/`→`closed/`, and promote only by PR + auto-merge with required `affected` gate. Never use `pending/` or push the exact feature head directly to master.
