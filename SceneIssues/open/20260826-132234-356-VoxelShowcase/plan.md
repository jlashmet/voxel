# Plan — VoxelShowcase Dirt/grass seam

## Observed / acceptance
Saved camera has two marked Dirt/Moss joins. Fresh replay from exact source `44a410690af445de3f723e19472d80b7697637a3` (run `33094358010`) has resident terrain and a clean lower mark, but direct inspection still shows the upper mark as a hard rectangular grass tongue. Acceptance: both marks read as continuous Dirt/grass joins at the saved pose.

## Hypotheses / discriminators
1. **Streaming/LOD or stale bake.** Falsified: replay is resident and the bake cache fingerprints `Assets/Game/WorldBuilder`; the source-changing run produced a distinct fresh input key.
2. **Road/market seam.** Lower mark is now clean; the surviving upper mark projects to the civic/upper west overlap, so the market taper is retained but is not the remaining owner.
3. **Civic/upper correction removed the wrong thing.** Confirmed. `upper-shoulder` starts at x=82.8 m while `civic-summit` starts at x=84.8 m. The former taper synthesized Moss in that 2 m mismatch; removing it stopped manufacturing Moss but left the higher-precedence correction with no ownership of the exposed strip. The fresh replay still shows exactly that axis-aligned 2 m tongue.

## Selected fix / regression
At precedence 16, repaint only the 20 dm west-envelope mismatch (x=82.8–84.8 m across the civic/upper overlap) Dirt. Do not emit Moss, change height/occupancy, move either terrace core, or widen the repair beyond the civic envelope. The PlayMode regression samples the production world coordinates and also checks the correction remains within its 3-primitive budget and still paves the upper core.

## Blast radius / cost
One existing terrace-correction definition gains one bounded `PaintSurface` box over a 20×72 dm strip. Geometry, roads, structures, market taper, other districts/captures, and generic rasterization are unchanged; no per-frame work is added.

## Verification gate
Keep open until exact-SHA targeted CI passes `SceneIssue20260826132234356CivicUpperWestJoinReclaimsMismatchAsDirt`, player compilation succeeds, and a fresh saved-camera replay is directly inspected at both marked circles. Only that clean frame becomes `verification-final.png`.
