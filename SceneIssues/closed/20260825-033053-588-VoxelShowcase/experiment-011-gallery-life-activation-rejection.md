# Experiment 011 — Gallery-life activation rejection

## Hypothesis
The trees visible from the assigned VoxelShowcase capture are the semantic scatter produced by `GalleryLifePopulation`.

## Action
- Traced the only scene harness that creates `GalleryLifePopulation`.
- Compared `WorldbuildingGalleryShowcase.OnEnable` with the assigned `VoxelShowcase.OnEnable` lifecycle.

## Result
`WorldbuildingGalleryShowcase` explicitly creates a separate world-object host, adds `GalleryLifePopulation`, and calls `Populate(...)` around the gallery centre. The standard `VoxelShowcase` lifecycle used by this capture does not create or populate that component; it constructs the ordinary `ShowcaseWorld`, far terrain, renderer, and player only.

## Conclusion
Rejected. The gallery semantic scatter is not the tree population in the assigned `VoxelShowcase` scene. The remaining source to trace is the ordinary showcase/world-generation wilderness vegetation north of the saved camera, specifically whatever produces visible tree geometry without publishing corresponding semantic `TreeInstance` damage/collision state.
