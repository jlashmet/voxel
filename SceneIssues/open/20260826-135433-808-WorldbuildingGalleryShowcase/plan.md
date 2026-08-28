# Plan — 20260826-135433-808 WorldbuildingGalleryShowcase

## Evidence / acceptance
- One marked region: `screenshot-001.png`, center `(0.4802, 0.6678)`, radius `0.0690`. Human review against `reference-grass-target.jpg` requires dense continuous pixel-art meadow, irregular layered silhouette, multiple green regions, no repeated three-blade icons/dark bars, plus local player bend/recovery.
- `VegetationPlacement` owns whether/what grows and emits authoritative `VegetationInstance`; presentation must not reject semantic Grass.

## Hypotheses / discriminators
1. **Grass renderer macro coverage caused holes — confirmed/fixed.** `ProceduralGrassBatch` no longer drops semantic Grass; seed varies only local blade density.
2. **Standalone never reached the grass shader — confirmed/fixed.** First final replay (`f682626...`) passed editor tests but logged `Vegetation shader was not found: VoxelEngine/ProceduralVegetationGrass`; the shader was compiled but stripped, so it is now Always Included.
3. **Shader retention alone removes the marked icon — rejected.** Corrected run `33201072735` passed 2/2 tests and has no missing-shader error, yet `verification-final.png` still shows the same blocky tuft at the mark.
4. **A non-Grass semantic accent owns that icon — confirmed by source.** Gallery uses generic `VegetationPlacement.Default`; only `Grass` enters the packed renderer. Foliage `Shape 0` explicitly reconstructs ordinary tuft/aquatic species as the same camera-facing three-rooted-blade sprite. That obsolete path survived the semantic-Grass migration.

## Selected fix + regression
- `a04822ae...`: render every semantic Grass placement as 5–15 deterministic packed ribbons; `GrassLookdevTests` covers former coverage holes and deterministic density.
- `a16c7710...`: retain the dedicated grass shader in standalone players.
- `5e2bcd1f...`: ordinary tuft/aquatic accents now use shape `0.75`, preserving their semantic kinds and shared foliage batching but bypassing legacy Shape-0 billboard reconstruction. `Grass` remains dedicated shape `5`.
- `b3007d87...`: `OrdinaryMeadowTuftsDoNotUseLegacyThreeBladeSpriteShape` locks that routing. Existing `ProceduralGrassBillboardTests` cover local player displacement and recovery.

## Blast radius / cost / gate
- No ecology/density change and no added instances, vertices, materials, or draw calls; only non-Grass tuft/aquatic presentation switches from collapsed billboard reconstruction to its already-built multi-card source geometry. Grass remains 5–15 ribbons (50–150 verts / 40–120 tris per semantic instance), one draw per occupied 32 m chunk.
- Gate: one fresh exact-SHA `GrassLookdevTests.*` request with built Gallery replay; require green tests, no runtime shader error, and visual removal of the marked repeated icon before promotion/closure.
