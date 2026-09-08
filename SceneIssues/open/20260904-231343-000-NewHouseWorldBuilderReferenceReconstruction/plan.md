# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Observed reference / acceptance
The reference is a tall ornate three-register house: pale stone lower storey; deep round-arched central portal with a pronounced voussoir ring and recessed timber door; narrow lower side windows; plaster/timber middle register with one compact blue-shuttered arch; steep swept blue portrait gable with a smaller arch and dense flower box; transverse roof shoulders; compact crest; tall left masonry chimney; flower boxes/ivy; blue-gold banner and bracketed sign. Final target/audits must be **production-quality** and very close in silhouette, proportions, openings, detail hierarchy, materials, and presentation, with no holes, floating pieces, overlaps, or z-fighting.

## Material results / root causes
Iterations 1–8 repaired roof ordering, shell holes, high-window depth, duplicate crest mass, and the straight portrait roof. Iteration 9/9b established the concave swept profile and closed roof/plaster seams. Iteration 10 restored the upper-gable flower box. Iteration 11 widened the gable spring line. Iteration 12 feature `800f5c5d1333cad1169b69f589de5b3928bd7f9e`, request `772e9e73eeb89bb790f37599e140a803d8302f2b`, run `34166993216` fixed the tall left chimney but remained prototype/blockout quality.

Iteration 13 feature `b045e276b727f785d78d6f9069ca36e0b27df83e`, exact request `ac63e59647ab7d72baaabd3c4aa16647dfbe11b1`, run `34174645322`, artifact `single-test-34174645322` completed successfully. Exact reference plus t=10/t=20/t=30 inspection confirms the compact middle-storey arch/shutters materially improve facade hierarchy while the gable, chimney, upper flower box, side openings, and rear shell remain intact. Visual closure is still rejected as **prototype/blockout quality**.

Two current hypotheses for the dominant mismatch:
1. **Ground-floor portal depth/surround geometry** (favored): the render still reads as a flat rectangular timber door in a stone wall, while the reference is visually anchored by a deep round stone portal with a thick protruding voussoir ring and recessed door.
2. **Camera/framing hides the arch**: perspective may reduce the arch read slightly, but the exact front target itself shows no strong projecting stone ring, so authored geometry is the primary cause.

## Selected fix / next discriminating experiment
Hold camera, lighting, materials, site, roof/gable, chimney, middle/high openings, side windows, flower boxes, hanging details, and rear shell fixed. In the late production finish pass, rebuild only the shallow central lower-entry patch: retain a recessed timber door, add a clearly projecting round stone surround/voussoir ring and jambs, and restore compact door hardware. The repair must remain shallow of the rear shell and must not erase the two lower side windows. Add a focused production-path behavioral regression proving the entry refill is followed by the protruding arch/jamb geometry and recessed door. Re-run exact-SHA standalone target/front-left/rear-right proof.

## Ownership / remaining gates
`Assets/Game/WorldBuilder` owns reusable authoring/refinement and module-local validation; site/camera/light remain validation composition. `Assets/Game/Materials` owns presentation; Rendering remains semantic-free. Continue exact-SHA compare/correct cycles until production-quality, then finish every `tasks.md` item, reconcile current `origin/master`, close `open/`→`closed/`, and promote only by PR + auto-merge with the required `affected` gate. Never use `pending/` or push the exact feature head directly to master.
