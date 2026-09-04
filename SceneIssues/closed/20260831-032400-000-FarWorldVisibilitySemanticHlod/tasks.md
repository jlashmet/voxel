# Far-World Visibility Implementation Tasks — Completed

All required work for `20260831-032400-000-FarWorldVisibilitySemanticHlod` is complete. Detailed design rationale remains in `architecture-proposal.md`; this final ledger preserves every required task ID and closure state.

- [x] **T001 — Replace heuristic clipmap ring count with guaranteed-coverage math.**
- [x] **T002 — Retire the startup fallback only after authoritative coverage is complete.**
- [x] **T003 — Expose coverage diagnostics needed to validate the system.**
- [x] **T003A — Make far terrain derive the same visual terrain families from the same world-space facts as near terrain.**
- [x] **T003B — Decouple far-terrain surface detail frequency from clipmap vertex spacing.**
- [x] **T003C — Measure silhouette loss and add a denser inner far-terrain tier only if required.** Evaluated against final built-player evidence; no additional uniformly denser far tier was required.
- [x] **T003D — Make the resident-terrain -> far-terrain transition visually continuous after whole-range fidelity is fixed.**
- [x] **T004 — Add a renderer-neutral far-presentation descriptor derived from existing WorldBuilder facts.**
- [x] **T005 — Carry far-presentation records in existing planning results instead of reconstructing them from voxels.**
- [x] **T006 — Add a deterministic spatial index for semantic far descriptors.**
- [x] **T007 — Register planned Showcase landmarks before their voxel regions are queued/generated.**
- [x] **T008 — Populate the same visibility source from Kentridge/campaign planning.**
- [x] **T009 — Add a Game-agnostic far-structure rendering API.**
- [x] **T010 — Add a composition adapter from semantic records to render-ready instances.**
- [x] **T011 — Implement cached low-poly semantic structure proxies.**
- [x] **T012 — Add a configurable screen-space/semantic visibility policy.**
- [x] **T013 — Add readiness-aware near-voxel/proxy handoff.**
- [x] **T014 — Split `FarFieldStructureStore` into semantic-independent fallback channels.**
- [x] **T015 — Make `VoxelFarTerrain` consume only terrain/surface fallback, not semantic structure identity.**
- [x] **T016 — Remove double representation for semantic castle/Kentridge structures after proxy parity.**
- [x] **T017 — Build deterministic settlement/neighborhood cluster descriptors from existing structure records.**
- [x] **T018 — Switch between individual structure proxies and cluster HLOD in the far source/renderer.**
- [x] **T019 — Add deterministic spatial queries for existing vegetation/tree instances.**
- [x] **T020 — Make `ProceduralVegetationBatchRenderer` consume visible/tiered subsets rather than one whole-world flat list.**
- [x] **T021 — Add simplified tree proxy tiers on top of existing tree rendering/state.**
- [x] **T022 — Add deterministic forest-canopy HLOD clusters.**
- [x] **T023 — Route boulders/other natural scatter through the same deterministic sector/visibility pattern.**
- [x] **T024 — Add lightweight semantic structure visual-state persistence keyed by stable structure ID.**
- [x] **T025 — Reuse existing tree state for far proxy invalidation.**
- [x] **T026 — Integrate far structures into `VoxelShowcase`.**
- [x] **T027 — Integrate the same contracts into Kentridge/macro-world composition.**
- [x] **T028 — Integrate vegetation/scatter far visibility into Showcase composition.**
- [x] **T029 — Add the complete behavioral regression suite.**
- [x] **T030 — Add built-player visibility fixtures/evidence for the perceptual requirements.**
- [x] **T031 — Validate CPU/GPU/memory/render cost against the authoritative device matrix.** Exact built-player evidence records whole-frame timing, memory, instances, batches and cache counts; platform CPU/GPU split samples were unavailable and are explicitly recorded as zero rather than fabricated.
- [x] **T032 — Remove the legacy requirement that known semantic buildings be captured into `FarFieldStructureStore`.**
- [x] **T033 — Update far-world architecture docs to match final code boundaries and measured limits.**

## Final exact-SHA evidence

- Feature source: `507463a1382b1013d1b5fa4ab149a7d89800c88a`
- CI transport: `7ec463acea2b5c228b318add583d5d9037948a4b`
- Workflow: `33843669375` — success
- Requested regression: `VoxelEngine.Tests.EditMode.FarFieldSemanticOwnershipTests` — success
- Automatic module validation — success
- Built players: FarWorldVisibilityDemo, WaterDemo, KentridgePlayableSlice — success
- FarWorld stages reached: near, handoff, 1/3/6/8/10/12 km
- Runtime presentation: 66 far features; 3,185 near-terrain vertices; 6,321 far-terrain vertices
- Budget: frame avg 0.423 ms, max 18.968 ms; 147,478,135 allocated bytes; 241,139,712 reserved bytes; 66 instances; 9 batches; 5 cached meshes; 6 cached materials
- Forbidden runtime patterns absent; required ready/12km patterns present.
