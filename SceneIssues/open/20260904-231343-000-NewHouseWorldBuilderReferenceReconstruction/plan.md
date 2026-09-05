# New House WorldBuilder Implementation Plan

## Acceptance
Reconstruct the supplied medieval cottage reference through the production WorldBuilder/material/rendering path. The final built-player target view must preserve the reference's major silhouette, steep multi-gable roof, chimney, Tudor timber/plaster façade, recessed openings, entry, flower boxes/ivy, stepping-stone approach, and material contrast. Garage/driveway requirements from the imported generic checklist are `N/A — absent from supplied reference`.

## Ownership / architecture
- Runtime owner: `Assets/Game/WorldBuilder`.
- Reusable house geometry: `Assets/Game/WorldBuilder/Voxel/NewHouseReferenceAuthoring.cs` using `IStructureAuthoringSession`; reference-specific site is a separate authoring method.
- Game material identity/projection remains in `Assets/Game/Materials`; Rendering receives only opaque material/texture-layer data.
- Renderer extension is generic optional additional surface texture layers, with its own module-local validation surface.
- Reference camera/light/site policy stays in the WorldBuilder validation scene, not in reusable house geometry.

## Chosen approach
Centralize house dimensions in `NewHouseReferenceConfig`, author primary massing/roof/openings/detail with existing structure primitives, map supplied textures to stable game-owned house materials, and bind those textures through the existing renderer asset. Repeated windows, roof runs, timber lines, flower boxes, and landscape forms use helpers rather than duplicated scene geometry.

Focused validation surfaces:
- `Assets/Game/WorldBuilder/Validation/NewHouseReferenceReconstruction/NewHouseReferenceReconstruction.unity`
- `Assets/VoxelEngine/Rendering/Validation/TextureLayers/SurfaceTextureLayersDemo.unity`

Supplemental EditMode tests cover translation-independent reuse, site separation, material registration/projection, and generic renderer-layer behavior.

## Blast radius / cost
Touched modules are `Game.Materials`, `Game.WorldBuilder`, and `VoxelEngine.Rendering`. No alternate renderer, storage authority, or structure API was introduced. Runtime cost is the existing authored voxel writes plus six optional opaque texture-array layers; renderer capacity remains bounded by the existing material limit.

## Current commit / evidence
Current feature branch commit before final documentation/visual iteration: `bd25d2e8c189d9c22f57a6e46f01efdfbcb92314`.

Exact-SHA run `33945621865` passed all feature-specific EditMode coverage and 1784/1785 module tests after the stable material-ID regression was fixed. Its sole PlayMode failure is being baseline-isolated because it originates from URP `DebugManager` calling legacy `UnityEngine.Input`, outside this feature's changed paths.

## Remaining gates
1. Finish baseline classification of that unrelated PlayMode failure without changing acceptance.
2. Obtain durable built-player captures from both owned validation scenes.
3. Compare the house capture directly with the supplied reference; fix demonstrated structural/material defects only.
4. Complete every imported checklist/acceptance item, record exact-SHA evidence, and close open→closed.
5. Refresh from `origin/master`, open the final PR, and enable auto-merge; never push the feature head directly to `master`.
