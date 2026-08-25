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
- [x] Wings have a high arched leading edge, 4+ distinct finger spars, and deep scalloped lower edges; pass 1 made this structurally readable, though final polish is still required.
- [ ] Tail is thick at the base, sweeps across the foreground, tapers continuously, and has visible ridge/spine detail.
- [ ] Surface detail includes readable scales/plates/spines at 10 cm resolution without turning the dragon into masonry.
- [x] Production capture has no missing chunks and passes `ModelViewerSceneTests.DragonStatueConvergesThroughProductionSurfacePath` for literal-voxel pass 1c (`32795607493`).
- [ ] Final render is judged against the concept after every pass; failed passes are documented below.

## Iteration log

- [x] Existing primitive-heavy passes reviewed. Latest old production image rejected: crocodilian/goat-like head, blunt hands/feet, bead-like chest accents, overly flat rectangular wing membranes, and weak horn silhouette.
- [x] Built `DragonStatueReferenceVoxelArt`, a reference-driven 10 cm voxel pass with explicit jaw/teeth, horn tiers, throat shields, articulated digits, five wing fingers, scalloped bays, tail spines, and scale bands.
- [x] Literal-voxel pass 1c production capture: **rejected despite technical success**. It is substantially cleaner and the wings finally read, but compared with the concept it is too tall/lanky; crown horns read like giant antlers; forelimbs are too long/thin; paws are blunt; chest shields read like horizontal ribs; haunches need more mass; foreground tail becomes a thin hoop instead of a thick armored/spined tail.
- [x] Started pass 2 with `DragonStatueConceptSilhouettePass`: compact layered crown, shorter/heavier forelimbs, larger separated toes/claws, bulked haunches, thick tail through the foreground, dense tail spines, and narrower overlapping chest shields.
- [ ] Capture pass 2 and perform harsh visual review.
- [ ] Iterate anatomy/silhouette defects found in pass 2.
- [ ] Iterate tertiary voxel detail only after silhouette/anatomy pass.
- [ ] Remove temporary mesh-bake workflow/tool detour before completion.
- [ ] Final diff + CI review.
