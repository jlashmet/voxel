# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and the normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Observed reference / acceptance
The reference is a tall ornate three-register house: pale stone ground storey; central round-arched timber portal and narrow arched side windows; timber/plaster middle storey with one blue-shuttered arched window; steep blue portrait gable with a smaller arched window; lower transverse blue roof shoulders; swept eaves; compact warm crest; left chimney; flower boxes/ivy; blue-gold banner and bracketed sign. Garage, driveway, dormer, porch roof, and materially visible gutters are absent. Final target/audits must be production-quality, very close in silhouette/proportion/details/material relationships, and free of holes, floating pieces, missing faces, obvious overlaps, and z-fighting.

## Material results
Iterations 1–5 established the roof replacement/order hazards and then exposed side/rear shell voids. Iteration 6 (`a679777d`, request `e9c81478`, run `34136916256`) repaired those voids but remained prototype/blockout quality.

Iteration 7: feature `cd9ae8c2ed0f8789e5f2ddf858365ef78e0ba9a1`, exact request `02d3bf547696212b0530e42cb797884c687b7bae`, run `34145422534`, artifact `single-test-34145422534` completed successfully. Exact reference plus target/front-left/rear-right frames at 10/20/30 seconds were inspected directly. Rear shell remains closed and module/player validation is green. Visual quality is still **prototype/blockout quality**: the intended smaller high gable window is not visibly open (only its timber cross reads on plaster), the crest remains an accumulated rectangular post, the eave hook is still too short, and the facade remains much coarser than the reference.

Source/evidence discriminated the high-window cause: `FillArch` writes opaque plaster forward to `frontZ-4`, but the replacement `ArchedPanel` carved only from `frontZ-1`, leaving three opaque front layers in front of the glass. The crest clear similarly began at `ridge+2`, leaving duplicate `ridge+1` layers from older builders. These are production authoring depth/order defects, not camera/material/CI failures.

## Selected fix / current experiment
Preserve the repaired shell, material path, camera cadence, and broad massing. For iteration 8, carve the high-window replacement through the full front repair depth, clear duplicate crest layers from `ridge+1`, and extend/increase the quadratic swept-eave hook. A focused regression now asserts the visible-depth carve, complete crest replacement, and far swept tip. Do not change camera/light/site in this experiment. Current implementation/test head before documentation refresh: `f0846da86cff6e05b9a15850572913d230868d49`.

## Ownership / remaining gates
`Assets/Game/WorldBuilder` owns reusable authoring/refinement and module-local validation; site/camera/light remain validation composition. `Assets/Game/Materials` owns presentation; Rendering remains semantic-free. Run exact-SHA targeted CI on the refreshed feature head and directly inspect target/front-left/rear-right frames. Continue compare/correct cycles until production-quality, then satisfy every remaining `tasks.md` item, merge current `origin/master`, close `open/`→`closed/`, PR + auto-merge, and pass the required `affected` gate. Never use `pending/` or push directly to master.
