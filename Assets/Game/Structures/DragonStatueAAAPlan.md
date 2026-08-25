# Dragon Statue AAA Iteration Plan

## Goal

Make Model Viewer **Dragon A** visually match the established voxel dragon concept at a professional/AAA showcase bar using the authoritative 10 cm voxel grid and the normal production surface renderer.

## Reference read

The target is the generated studio concept used in this work: an upright seated dragon with a long S-neck, low wedge skull, layered swept horns, open mouth, broad chest armor, massive haunches, four separated claws per foot, a thick foreground tail, and two large raised bat wings with several visible finger spars and deep scalloped membrane edges.

## Constraints

- Authoritative geometry remains canonical CPU-authored voxel cells; no mesh renderer or GPU-derived authoritative state.
- Model Viewer must render through the normal production voxel surface path.
- 10 cm voxels are the modeling resolution; prefer explicit voxel-scale secondary/tertiary forms over broad corrective blobs.
- Keep Dragon B as the fallback/legacy sculpt.
- Do not accept a pass merely because it is recognizable as a dragon; compare silhouette and anatomy to the concept.
- Reuse the existing feature branch `feature/sdf-dragon-statue` and CI branch `ci-test/dragon-model-viewer`.

## Acceptance criteria

- [ ] Head reads as a dragon at thumbnail scale: low wedge muzzle, visible jaw opening, brows/eyes, cheek spines, multiple swept crown horns.
- [ ] Neck has a clear S-curve and layered ventral armor rather than a vertical tube.
- [ ] Forelimbs have shoulder/elbow/wrist articulation and four separated digits with long claws.
- [ ] Rear legs/haunches carry the seated weight and feet have four separated toes/claws.
- [ ] Wings have a high arched leading edge, 4+ distinct finger spars, and deep scalloped lower edges; they do not read as rectangles or tarps.
- [ ] Tail is thick at the base, sweeps across the foreground, tapers continuously, and has visible ridge/spine detail.
- [ ] Surface detail includes readable scales/plates/spines at 10 cm resolution without turning the dragon into masonry.
- [ ] Production capture has no missing chunks and passes `ModelViewerSceneTests.DragonStatueConvergesThroughProductionSurfacePath`.
- [ ] Final render is judged against the concept after every pass; failed passes are documented below.

## Iteration log

- [x] Existing primitive-heavy passes reviewed. Latest production image is rejected: crocodilian/goat-like head, blunt hands/feet, bead-like chest accents, overly flat rectangular wing membranes, and weak horn silhouette.
- [ ] Build a single reference-driven Dragon A authoring pass that owns the full sculpture rather than stacking destructive fixes.
- [ ] Capture production render and perform harsh visual review.
- [ ] Iterate anatomy/silhouette defects found in capture.
- [ ] Iterate tertiary voxel detail only after silhouette/anatomy pass.
- [ ] Remove temporary mesh-bake workflow/tool detour before completion.
- [ ] Final diff + CI review.
