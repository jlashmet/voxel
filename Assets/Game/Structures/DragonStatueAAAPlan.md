# Dragon Statue AAA Iteration Plan

## Goal

Make Model Viewer **Dragon A** visually match the established voxel dragon concept at a professional/AAA showcase bar using the authoritative 10 cm voxel grid and the normal production surface renderer, with the exact same authored object used by World Builder placement.

## Reference read

The target is the generated studio concept used in this work: a powerful seated/crouched dragon with a graceful S-neck, low angular skull, layered swept horns, open mouth, broad overlapping chest armor, massive but anatomically articulated limbs, four separated claws per foot, a thick foreground tail, and two large raised bat wings with arched leading edges, curved structural fingers, warm membranes, and deep scalloped trailing edges. The primary silhouette must feel elegant and predatory rather than squat, gorilla-like, skeletal, curtain-winged, or assembled from primitives.

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

- [ ] Head reads as a dragon at thumbnail scale: compact wedge muzzle, visible jaw opening, brows/eyes, cheek fins, and swept crown horns that do not read as antlers.
- [ ] Neck has a graceful S-curve, broad shoulder transition, and layered ventral armor rather than a vertical tube.
- [ ] Forelimbs have shoulder/elbow/wrist articulation, connected hands, and four separated digits with long claws without dominating the torso.
- [ ] Rear legs/haunches carry the seated weight and feet have four separated toes/claws with a compact feline/reptilian silhouette rather than elephant feet.
- [ ] Wings frame the body with a broad arched leading edge, curved/fanned structural fingers, warm readable membranes, deep V/U scallops, and no curtain/venetian-blind/rib-cage read.
- [ ] Tail is thick at the base, makes one elegant open foreground sweep fully visible in the hero framing, tapers continuously, and has visible ridge/spine detail without forming a closed ring.
- [ ] Ventral armor reads as 5–7 large overlapping pointed shields flowing from jaw to belly, not many horizontal ribs/slabs.
- [ ] Surface detail includes readable scales/plates/spines at 10 cm resolution without turning the dragon into masonry.
- [x] Production capture has no missing chunks and passes `ModelViewerSceneTests.DragonStatueConvergesThroughProductionSurfacePath` for v5 (`32797911327`) and v6 (`32798951633`).
- [x] Model Viewer Dragon A is authored by `DragonStatueWorldBuilderObject` through `DecorationVoxelStampBackend`, with bounds owned by the detailed production sculpt.
- [ ] Production capture contains no detached digits, floating remnants, accidental isolated strips, or obvious clear/rebuild seams.
- [ ] Hero camera framing contains the entire tail, wing tips, claws, and horns with intentional breathing room.
- [ ] Final render is judged against the concept after every pass; failed passes are documented below.

## Iteration log

- [x] Existing primitive-heavy passes reviewed. Latest old production image rejected: crocodilian/goat-like head, blunt hands/feet, bead-like chest accents, overly flat rectangular wing membranes, and weak horn silhouette.
- [x] Built `DragonStatueReferenceVoxelArt`, a reference-driven 10 cm voxel pass with explicit jaw/teeth, horn tiers, throat shields, articulated digits, five wing fingers, scalloped bays, tail spines, and scale bands.
- [x] Literal-voxel pass 1c production capture: **rejected despite technical success**. It is substantially cleaner and the wings finally read, but compared with the concept it is too tall/lanky; crown horns read like giant antlers; forelimbs are too long/thin; paws are blunt; chest shields read like horizontal ribs; haunches need more mass; foreground tail becomes a thin hoop instead of a thick armored/spined tail.
- [x] V3/V4/V5 established the canonical authored-object path and progressively replaced the worst silhouette defects.
- [x] V5 production capture (`32797911327`) harsh review: **rejected**. The dominant visible wing is a tall rectangular slab with nearly parallel vertical ribs; the opposite wing contributes almost nothing to the silhouette. The neck is too straight and thin relative to the torso. The front hands are visibly disconnected. The right-side rear foot is malformed. Chest plates are broad horizontal bands. The tail is a short rounded loop. Crown horns read as antlers. Floating remnants remain. Body masses are too gorilla-like.
- [x] Source-path audit found viewer/world-builder divergence. Fixed it: World Builder and Model Viewer Dragon A now route through the same detailed production authoring and bounds; Dragon B explicitly remains the old organic fallback.
- [x] V6 hero-silhouette rebuild implemented with full neck/head/wing/tail/distal-limb ownership, four front digits, a larger open tail, and rebuilt chest armor.
- [x] V6 targeted production run `32798951633`: **green technically, rejected visually**. Improvements: forelimb continuity is fixed, four front claws read, the tail has substantial mass, and the World Builder path converges correctly. Remaining visual failures are still severe: visible wing remains a near-rectangular black curtain; only one wing meaningfully frames the body; torso and forelimbs remain gorilla-like; head is undersized and horse/croc-like; crown still reads as antlers; ventral armor reads as many narrow ribs; rear foot remains weak/elephant-like; the foreground tail is clipped by hero framing; sparse inherited details/remnants still prevent a clean premium silhouette.
- [x] Critical strategy correction after V6: stop stacking corrective passes. V3→V6 inheritance is now itself an art-quality risk. V7 will be a **single-owner clean hero authoring from an empty object**, built directly from the reference. No old head, wing, limb, tail, armor, or surface geometry will survive beneath it.
- [ ] V7 clean hero authoring: compact broad body; shorter articulated forelimbs; powerful haunches; graceful S-neck; larger low wedge head; short swept layered horns; two deliberately asymmetric hero-visible wings; warm membrane bays built with explicit inward notches instead of carving a rectangular sheet; 5–7 large ventral shields; one full foreground tail sweep with blade tip and dorsal spines.
- [ ] Route `DragonStatueDetailedVoxelAuthoring` exclusively to V7, retaining V3–V6 only as historical source until final cleanup.
- [ ] Increase/retune Dragon A hero framing so the complete V7 silhouette is visible.
- [ ] Capture V7 through production Model Viewer and perform harsh reference comparison.
- [ ] Iterate clean V7 anatomy/silhouette until accepted before tertiary detailing.
- [ ] Add/rework tertiary scales, dorsal spines, moss/weathering, wing membrane accents, and material breakup only after silhouette/anatomy is accepted.
- [ ] Remove temporary mesh-bake workflow/tool detour before completion; it currently forces all 7 test assemblies in affected CI.
- [ ] Final diff + targeted CI + affected CI review, distinguishing branch-preexisting architecture failures from dragon regressions.
