# Plan: WorldBuilder-only scene composition

## Evidence / acceptance

- This architecture capture has no screenshots, frames, poses, or annotations; there are no visual marked regions to replay. The evidence surface is the production scene/bootstrap code.
- Build scenes are `KentridgePlayableSlice` and `WorldbuildingGalleryShowcase`; the issue also explicitly covers showcase/lookdev bootstraps.
- Current bypasses are concrete: `KentridgePlayableSlice` selects Hightown/corridor voxel catalogues and life population directly; `VoxelShowcase`/`WorldbuildingGalleryShowcase` construct and start `ShowcaseWorld` directly; `ArchLookdev`/`TerrainLookdev` create voxel storage and author environment content; `ArchReferenceGrowth*`, `GalleryLifePopulation`, `ShowcaseTreePopulation`, and `VoxelFarTerrain` contain scene-local environment generation.

## Competing hypotheses

1. **Scene bootstraps still own world realization despite reusable generators. Supported.** Direct storage creation, backend catalogue combination, vegetation/life realization, and procedural environment authoring prove the boundary leak.
2. **The defect is only source location/naming. Rejected.** Moving the same algorithms without changing the API leaves scenes choosing low-level generation details.
3. **All large scene scripts are violations. Rejected.** Camera/lighting/UI/input/animation/metrics remain valid scene-local presentation; the discriminator is whether code creates or mutates generated world/gameplay-environment content.

## Selected fix / regression

- Add semantic WorldBuilder scene-composition specs that preserve seed, feature subsets, locations, and authored intent without exposing backend catalogue/storage choices.
- Add reusable engine-bound composition realization over existing Showcase/WorldGen/Structures/Vegetation modules; scene bootstraps become configuration/orchestration callers.
- Move remaining environment-generation implementations out of `Assets/Scenes` into reusable composition ownership where practical, preserving `.meta` GUIDs so serialized scenes remain intact.
- Add a focused EditMode behavioral regression through the production composition API proving distinct representative recipes (small showcase without castle vs full gallery, plus Kentridge-region composition) resolve through shared WorldBuilder operations. Supplement with an architecture guard preventing low-level world authoring from returning to scene source.

## Blast radius / cost

- Preserve existing deterministic seeds, feature flags, storage budgets, catalogue outputs, and scene presentation settings; no world-truth or rendering budget is loosened.
- The new layer delegates to existing bounded generators rather than adding per-frame work. Composition planning is O(number of requested semantic features); runtime generation cost remains the existing scene cost.
- Before final CI, compare the feature diff only to the assigned capture plus shared composition/API/tests, refresh from current master, and run one exact targeted EditMode fixture.