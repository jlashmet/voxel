# Plan

## Observed behavior / acceptance
`WorldbuildingGalleryShowcase` currently authors a generic shed/church/temple/cave collection directly from showcase composition code. The issue has no captured frames or marked regions; evidence is the six complete screenshot prefix sets under `References/MountingForce/map-images/`. Required end state: six labeled, walkable districts (Kentridge, Hightown, Moordell, Rossdam, Fairy Village, Orc Village), each with a palette display plus residential, commercial, civic/communal, and landmark/infrastructure examples through reusable WorldBuilder/shared authoring.

Screenshot evidence inspected by prefix: Kentridge includes church, well, warehouse, inn and houses; Hightown includes church, mayor house, shops, pub and under-church/cave spaces; Moordell includes inn/pub, shops, grave and low rugged buildings; Rossdam includes king chamber, shops and fortified/royal vocabulary; Fairy Village includes treehouse, cave, inn/pub and nature-integrated shops/houses; Orc Village includes armor/magic/weapon shops, pub and rough fortified village forms.

## Competing hypotheses / discriminator
1. **Gallery-composition defect only:** rearranging/recoloring existing gallery primitives is sufficient. Falsified: current gallery directly authors generic structures and would violate the issue's shared-WorldBuilder boundary and silhouette/role differentiation requirement.
2. **Existing town realization can be reused for all six:** register missing names and instantiate each town. Falsified: `WorldBuilderTownAuthoring` only registers Kentridge/Hightown and `WorldBuilderVoxelCatalogue` only realizes Kentridge.

## Selected fix
Add a reusable WorldBuilder town-architecture program describing six style identities, semantic material families, silhouettes, reference evidence, and the four required structure roles. Add shared voxel district authoring that consumes those programs and a material-role palette, producing labels/swatches, distinct residential/commercial/civic/landmark assemblies, street treatment and style props. `ShowcaseWorld` will only select styles, grounded district origins and game-material mappings.

Add a focused EditMode behavioral regression through `WorldBuilderTownArchitecture.Resolve` proving all six programs resolve, expose distinct material families/silhouettes, retain screenshot evidence, and contain all four required roles. Scene validation must use the exact built-application `WorldbuildingGalleryShowcase` harness.

## Blast radius / cost
No new shader/material IDs: reuse existing game material identities, so shader/material-table cost is unchanged. Geometry is bounded to six compact districts and authored only during gallery bake/generation; no gameplay/world truth or device budgets change. Validate generated write count indirectly through bounded program dimensions and final scene harness; preserve existing gallery bake path.

Current base: `f803d0ad93a6b8c36bfb2909f2e663e04cb96ebc`.
Remaining gates: implement, focused regression green on exact feature SHA, built-app gallery harness green with six-district evidence, pending metadata/move, close, merge latest master, non-force push master.
