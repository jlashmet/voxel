# Tasks — reopened WorldbuildingGalleryTownArchitectureShowcase

- [x] Preserve prior six-town implementation as accepted baseline rather than discarding it.
- [x] Audit final 18-view artifact for visual distinctness.
- [x] Audit `TownArchitectureProgram`, `WorldBuilderTownArchitecture`, `WorldBuilderTownArchitectureVoxelAuthoring`, and existing town-authoring entry points for extensibility.
- [x] Identify the serious gap: adding a seventh ordinary style currently requires central enum/switch/backend edits and a likely town-named authoring method.
- [ ] Refactor style identity/registration so new town styles are not limited to six compile-time IDs.
- [ ] Decouple silhouette, roof, opening, and detail composition so valid combinations are data/strategy driven instead of one-to-one enum matching.
- [ ] Replace named `Resolve`/canonical-seed switch registration with an extensible registry/catalogue mechanism.
- [ ] Replace voxel backend's six-town silhouette dispatch with composable reusable massing/roof/facade/detail/landmark strategies.
- [ ] Preserve reusable shared detail primitives and all six existing town outputs/contracts.
- [ ] Add a seventh synthetic proof town using a novel combination of existing capabilities without a new central switch case or `Author<ProofTown>` method.
- [ ] Prove the proof town generates residential, commercial, civic/communal, and landmark/infrastructure roles with deterministic seeded variation.
- [ ] Add focused regressions proving extensible registration/composition and preservation of all six existing style contracts.
- [ ] Run exact built `WorldbuildingGalleryShowcase` and capture wide/player/close evidence for all six existing towns plus the proof town.
- [ ] Directly inspect rendered evidence for distinctness, detail retention, circulation/grounding/intersections, and no visual regression.
- [ ] Measure world-build work, memory/allocation, render/draw implications, and blast radius.
- [ ] Run final exact-SHA targeted CI and built-player validation through the canonical SceneIssue workflow.
- [ ] Complete pending metadata, move open -> pending -> closed only after every acceptance criterion is green, then merge/push per workflow.
