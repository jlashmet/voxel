# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and the normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Observed reference / acceptance
The exact reference is a tall ornate three-register house: pale stone ground storey; central round-arched timber portal and narrow arched side windows; timber/plaster middle storey with one blue-shuttered arched window; steep blue portrait gable with a smaller arched window; lower transverse blue roof shoulders; swept eaves; compact warm crest; left chimney; flower boxes/ivy; blue-gold banner and bracketed sign. No garage, driveway, dormer, porch roof, or materially visible gutter is present in the reference. Final built-player target and audit views must be production-quality, very close in silhouette/proportion/details/material relationships, and free of holes, floating pieces, missing faces, obvious overlaps, and z-fighting.

## Material results
Iterations 1–4 were prototype/blockout quality; the roof replacement then erased upper openings, proving authoring order mattered. Iteration 5 restored the front openings but exposed wall-sized side/rear shell voids.

Iteration 6: exact feature `a679777dc24cba437837fe50050b55c019026fdc`, request `e9c814785db3477a9b74a29c5705afbaf52c221f`, run `34136916256`, artifact `single-test-34136916256` completed successfully. The artifact preserved the exact reference (`ReferenceInputs/NewHouse/...png`, verified Git blob `6d87b08...`) and target/front-left/rear-right frames at 10/20/30 seconds. Module validation and requested regressions passed; `missingVisible=0` and complete published near-surface coverage were reported. The rear-right frame confirms the wall-sized shell holes are repaired.

Direct inspection still classifies the house **prototype/blockout quality**, so green CI is rejected as completion. Largest remaining target-view defects: the portrait gable is too blank and straight-sided; the high window is oversized relative to the reference; the gold crest is a large blocky mass; eaves lack the reference's outward/downward sweep; banner/sign remain flat blockouts; facade/foliage detail density is still coarse. These are rendered-product defects, not camera or CI defects.

## Selected fix / current experiment
Keep the repaired shell, current camera cadence, material path, and broad three-register massing. Apply a late production authoring finish pass that (1) layers timber braces/belts across the portrait gable, (2) replaces the high opening with a smaller arched window, (3) adds swept eave tips, (4) clears/rebuilds a compact crest, and (5) rebuilds the hanging sign/banner closer to the reference silhouette. Add a focused operation-order/detail regression. Do not change site/camera/light policy in this experiment. Current implementation head before documentation refresh: `04cbfefea3ff438ddde2dcbb335c2361b8f87732`.

## Ownership / remaining gates
`Assets/Game/WorldBuilder` owns reusable authoring/refinement and module-local validation; site/camera/light remain validation composition. `Assets/Game/Materials` owns presentation; Rendering remains semantic-free. Run a new exact-SHA targeted CI on the final feature head and inspect its exact target/front-left/rear-right frames. Continue compare/correct cycles until production-quality, then satisfy every remaining `tasks.md` item, merge current `origin/master`, close `open/`→`closed/`, PR + auto-merge, and pass the required `affected` gate. Never use `pending/` or push directly to master.
