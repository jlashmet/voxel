# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Observed reference / acceptance
The reference is a tall ornate three-register house: pale stone lower storey; round-arched central portal and narrow side windows; plaster/timber middle register with one compact blue-shuttered arch; steep swept blue portrait gable with a smaller arch and dense flower box; transverse roof shoulders; compact crest; tall left masonry chimney; flower boxes/ivy; blue-gold banner and bracketed sign. Final target/audits must be **production-quality** and very close in silhouette, proportions, openings, detail hierarchy, materials, and presentation, with no holes, floating pieces, overlaps, or z-fighting.

## Material results / root causes
Iterations 1–8 repaired roof ordering, shell holes, high-window depth, duplicate crest mass, and the straight portrait roof. Iteration 9/9b established the concave swept profile and closed roof/plaster seams. Iteration 10 restored the upper-gable flower box. Iteration 11 widened the gable spring line. Iteration 12 feature `800f5c5d1333cad1169b69f589de5b3928bd7f9e`, exact request `772e9e73eeb89bb790f37599e140a803d8302f2b`, run `34166993216`, artifact `single-test-34166993216` completed successfully. Exact reference plus t=10/t=20/t=30 inspection confirms the left chimney is now a tall continuous stone stack and the gable/flower-box/rear-shell corrections remain intact. Visual closure is still rejected as **prototype/blockout quality**.

Two current hypotheses for the dominant mismatch:
1. **Facade opening/framing scale** (favored): the middle arched opening plus shutters and cross framing occupy most of the storey, while the reference keeps a compact central arch surrounded by substantial plaster and layered timber.
2. **Camera/FOV exaggeration**: perspective could amplify the opening, but the front-left evidence shows the authored opening and timber spans are intrinsically oversized, so camera is not the primary cause.

## Selected fix / next discriminating experiment
Hold camera, lighting, materials, site, roof/gable, chimney, upper-gable window/flower box, hanging details, lower-storey openings, and rear shell fixed. In the late production finish pass, refill the oversized middle opening/framing region and rebuild one smaller central arched window with narrower blue shutters, restrained timber surround/belts, and the existing production palette. Add/extend a behavioral regression that proves the final compact opening is authored after destructive facade repair. Re-run exact-SHA standalone target/front-left/rear-right proof.

## Ownership / remaining gates
`Assets/Game/WorldBuilder` owns reusable authoring/refinement and module-local validation; site/camera/light remain validation composition. `Assets/Game/Materials` owns presentation; Rendering remains semantic-free. Continue exact-SHA compare/correct cycles until production-quality, then finish every `tasks.md` item, reconcile current `origin/master`, close `open/`→`closed/`, and promote only by PR + auto-merge with the required `affected` gate. Never use `pending/` or push the exact feature head directly to master.
