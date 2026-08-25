# Dragon Statue AAA Iteration Plan

## Goal

Make Model Viewer **Dragon A** visually match the established voxel dragon concept at a professional/AAA showcase bar using the authoritative 10 cm voxel grid and the normal production surface renderer, with the exact same authored object used by World Builder placement.

## Reference read

The target is the generated studio concept used in this work: a powerful seated/crouched dragon with a long S-neck, low angular skull, layered swept horns, open mouth, broad chest armor, massive but anatomically articulated limbs, four separated claws per foot, a thick foreground tail, and two large raised bat wings with curved structural fingers and deep scalloped membrane edges. The primary silhouette must feel elegant and predatory rather than squat, gorilla-like, skeletal, or assembled from primitives.

## Constraints

- Authoritative geometry remains canonical CPU-authored voxel cells; no mesh renderer or GPU-derived authoritative state.
- Shape construction remains deterministic implicit/SDF-style authoring sampled into the canonical 10 cm voxel grid.
- Model Viewer must render through the normal production voxel surface path.
- Model Viewer Dragon A and the World Builder dragon object must invoke the same authoring entry point; there may not be a separate viewer-only hero sculpt.
- 10 cm voxels are the modeling resolution; prefer deliberate voxel-scale secondary/tertiary forms over broad corrective blobs.
- Keep Dragon B as the fallback/legacy sculpt.
- Do not accept a pass merely because it is recognizable as a dragon; compare silhouette and anatomy to the concept.
- Reuse the existing feature branch `feature/sdf-dragon-statue` and CI branch `ci-test/dragon-model-viewer`.
- Every visual pass is judged from a fresh production capture artifact, not from source-code intent.

## Acceptance criteria

- [ ] Head reads as a dragon at thumbnail scale: low wedge muzzle, visible jaw opening, brows/eyes, cheek fins, and swept crown horns that do not read as antlers.
- [ ] Neck has a graceful S-curve, broad shoulder transition, and layered ventral armor rather than a vertical tube.
- [ ] Forelimbs have shoulder/elbow/wrist articulation, connected hands, and four separated digits with long claws.
- [ ] Rear legs/haunches carry the seated weight and feet have four separated toes/claws without tail-clear damage.
- [ ] Wings frame the body with a broad arched leading edge, curved/fanned structural fingers, deep scalloped lower edges, and no venetian-blind/rib-cage read.
- [ ] Tail is thick at the base, makes one elegant open foreground sweep, tapers continuously, and has visible ridge/spine detail without forming a closed ring.
- [ ] Ventral armor reads as overlapping pointed shields flowing from jaw to belly, not a stack of horizontal slabs.
- [ ] Surface detail includes readable scales/plates/spines at 10 cm resolution without turning the dragon into masonry.
- [x] Production capture has no missing chunks and passes `ModelViewerSceneTests.DragonStatueConvergesThroughProductionSurfacePath` for v5 (`32797911327`).
- [ ] Production capture contains no detached digits, floating remnants, accidental isolated strips, or obvious clear/rebuild seams.
- [ ] Model Viewer Dragon A is authored by `DragonStatueWorldBuilderObject` through `DecorationVoxelStampBackend`, and its bounds match the detailed sculpt.
- [ ] Final render is judged against the concept after every pass; failed passes are documented below.

## Iteration log

- [x] Existing primitive-heavy passes reviewed. Latest old production image rejected: crocodilian/goat-like head, blunt hands/feet, bead-like chest accents, overly flat rectangular wing membranes, and weak horn silhouette.
- [x] Built `DragonStatueReferenceVoxelArt`, a reference-driven 10 cm voxel pass with explicit jaw/teeth, horn tiers, throat shields, articulated digits, five wing fingers, scalloped bays, tail spines, and scale bands.
- [x] Literal-voxel pass 1c production capture: **rejected despite technical success**. It is substantially cleaner and the wings finally read, but compared with the concept it is too tall/lanky; crown horns read like giant antlers; forelimbs are too long/thin; paws are blunt; chest shields read like horizontal ribs; haunches need more mass; foreground tail becomes a thin hoop instead of a thick armored/spined tail.
- [x] V3/V4/V5 established a clean canonical authored-object path and progressively replaced the worst silhouette defects.
- [x] V5 production capture (`32797911327`) harsh review: **rejected**. It is still far below AAA. The dominant visible wing is a tall rectangular slab with nearly parallel vertical ribs; the opposite wing contributes almost nothing to the silhouette. The neck is too straight and thin relative to the torso. The front hands are visibly disconnected because the V5 cleanup erases the distal forelimbs and only rebuilds palms/digits. The right-side rear foot is malformed/damaged. Chest plates are broad horizontal bands rather than nested armor. The tail is a short rounded loop tucked beside the body instead of the reference's long elegant foreground sweep. Crown horns are still oversized and read as antlers. Several isolated remnants/floating strips are visible around the head/wing envelope. Body masses are readable but too blobby and gorilla-like through the shoulders/forelimbs.
- [x] Source-path audit found a second correctness problem: Model Viewer Dragon A directly authors `DragonStatueDetailedVoxelAuthoring`, while `DragonStatueWorldBuilderObject` still routes through `DragonStatueSculptAuthoring + DragonStatueDetailPass` and uses the older, smaller `DragonStatueAuthoring` bounds. The viewer and placeable object can therefore disagree. Treat this as a blocking defect, not cleanup.
- [ ] V6 hero-silhouette rebuild: replace head/horn envelope cleanly; rebuild the neck/shoulder transition; rebuild complete forelimbs and distal rear feet; replace both exposed wings with curved multi-segment finger fans; replace tail with one open tapered foreground sweep; re-author pointed ventral plates; explicitly clear old remnants before each rebuilt region.
- [ ] Route World Builder dragon placement and Model Viewer Dragon A through the same `DragonStatueDetailedVoxelAuthoring` path and update bounds to the detailed sculpt.
- [ ] Capture V6 through production Model Viewer and perform harsh visual review.
- [ ] Iterate anatomy/silhouette defects from the V6 capture before adding tertiary detail.
- [ ] Add/rework tertiary scales, dorsal spines, moss/weathering, and material accents only after silhouette/anatomy is accepted.
- [ ] Remove temporary mesh-bake workflow/tool detour before completion.
- [ ] Final diff + targeted CI + broader affected CI review.
