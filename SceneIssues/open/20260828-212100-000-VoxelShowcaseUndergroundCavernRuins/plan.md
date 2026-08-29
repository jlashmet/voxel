# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver the full production sequence: natural walkable mouth, long gentle direction-changing descent, huge irregular cavern with multiple geological formations, reachable aged masonry ruin, exactly two grounded readable statues, localized supported torch/lantern lighting, and deep darkness. Closure requires focused regression, exact built-player traversal through normal gameplay movement/collision/streaming, direct AAA visual review of presented frames, and bounded cost/blast radius.

`SceneIssues/feature-readme.md` is absent; `AGENTS.md`, `SceneIssues/README.md`, the assignment contract, and `quality-review.md` remain authoritative.

## Evidence and discriminated hypotheses
Historical request `867d1e63` / run `33254822417` was structurally green but failed visual review: clipped descent evidence, cylindrical destination, box-first ruin, and primitive statues. The selected finish added reusable lobes, layered masonry, articulated statues, silhouette formations, and moving-player traversal evidence.

Final request `2c0f86dc` / run `33269217520` attempt 1 exposed a product failure: the focused PlayMode test stalled at waypoint 34/38 at `(-50.81,-71.20,-37.80)` targeting `(-49.00,-71.20,-37.80)`. The built player likewise reached waypoint 30 but never completed the route during the 95-second capture. Attempt 2 was cancelled before steps and is only infrastructure evidence.

The motor/grade hypothesis is falsified: waypoint 34 is on the same terminal floor grade and the earlier protected route already covered this primary span. Geometry inspection supports the finish-overwrite hypothesis: the rear irregular lobe shell overlaps the final primary approach near x=-490, while the post-finish `UndergroundCavernCirculationProtection.Reassert` derives its start only from ruin bounds and begins well inside the cavern, leaving that approach unrepaired.

## Selected fix
Keep cave core, route semantics, motor, renderer/light transport, eight-light cap, 55M write budget, and visual authoring unchanged. Make circulation protection reusable across the whole destination by deriving its start from cavern bounds plus a small width-derived rear overlap, then carve continuously through the ruin approach. Pass both cavern and ruin bounds from `ShowcaseWorld`; add a focused bounds/route regression so the contract cannot regress to ruin-only protection.

Blast radius is limited to the opt-in cavern circulation helper and its sole production caller. Cost is one bounded destination corridor carve, replacing the shorter existing post-finish carve; quantify the delta in final exact-SHA metrics.

## Remaining gates
Implement/regress the approach repair, issue one fresh exact-SHA request on the assigned CI ref, require focused PlayMode and mapped real-player traversal/capture green, inspect every useful frame for the full reveal and AAA bar, record final cost evidence, then move only this assignment through pending/closed and promote the exact validated head non-force.