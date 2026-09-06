# PropShowcase tasks

## Discovery and architecture
- [x] Fetch current master and inspect canonical prop/decor/world-object catalogues and realization code.
- [x] Inventory deterministic 529-entry in-scope production set without a second showcase identity registry.
- [x] Add the narrowest read-only enumeration/query boundaries and reusable production consumer needed by the showcase.
- [x] Keep Structures, SceneRuntime, and Materials as the owning modules with module-local validation surfaces.

## Browser and production preview
- [x] Implement deterministic catalogue model, stable friendly labels, current selection, scrollable left panel, and right live preview.
- [x] Register `Assets/Scenes/PropShowcase.unity` and render every selected item through its production realization path.
- [x] Replace previous selection rather than accumulate previews; preserve production materials/coatings/backends/world-object semantics.
- [x] Use production-compatible floor/reference lighting and bounds/semantic-front framing across tiny, medium, large, wall, floor, ceiling, thin, procedural, voxel-stamp, emissive, movable/container, and interactive representatives.
- [x] Publish truthful voxel-backed `LOADING` then `READY` state only after production surface publication.
- [x] Provide diagnostic error state without fallback cubes, parallel geometry, ad-hoc shaders, or duplicate content authority.

## Demonstrated visual defects
- [x] Fix Merchant Sign thin-surface presentation with production raised frame/emblem detail and verify fresh built evidence.
- [x] Fix horizontal Trapdoor mount and detailed Door/SecretDoor/Trapdoor production geometry while preserving interaction semantics.
- [x] Reject the first two Forge Hearth attempts when exact player evidence still read as a solid/blockout masonry face.
- [x] Isolate the repeated Hearth symptom before another fix; prove `DecorationPlacement.Facing` was dropped by Hearth authoring rather than publication/meshing.
- [x] Make canonical Hearth authoring follow semantic facing for all four horizontal cardinals.
- [x] Add `Game.Structures.Tests.ForgeHearthFacingAuthoringTests` as the focused production-authoring regression.
- [x] Directly inspect final exact built evidence and accept Merchant Sign, Door/Trapdoor, Forge Hearth/effects, grounding, materials, and representative construction at production-quality bar.

## Module validation and CI
- [x] Structures owns `Validation/PropShowcaseProductionValidation.*` through real production authoring/presentation.
- [x] SceneRuntime owns PropShowcase runtime/material validation and switching/resource scenarios.
- [x] Materials owns `PropMaterialCompositionTests` plus module-local standalone validation.
- [x] Keep top-level PropShowcase as integration evidence only; rely on repository-derived module ownership and canonical Kentridge integration gate.
- [x] Repair demonstrated CI orchestration defects: isolated PlayMode teardown, top-level-scene ownership fallback, and per-scene artifact collisions.
- [x] Run final exact feature SHA `b470db110e4a8edb3029e881656ac508bb20e057` through request `8e5d78f5b2dcdae1fd8e2a92d85ff5bd278a13ba`, workflow `34017941950`.
- [x] Verify requested Hearth regression, all repository-derived module tests/scenes, PropShowcase standalone replay, and canonical Kentridge standalone integration all pass.

## Resource/cost evidence
- [x] Repeat the sampled production selection set across three frame-separated cycles.
- [x] Record startup/switch timing, owned objects/components, global mesh/material counts, allocator totals, and resident geometry.
- [x] Prove retirement of deferred/inactive objects and native mesh accounting through focused regressions.
- [x] Review final same-endpoint measurements: cycles remain at 5 objects / 3 renderers / 3 colliders / 1 light / 0 particles / 4 global meshes / 53 global materials; Unity allocated delta is +143,960 bytes across three cycles.
- [x] Stress 110 switches with current/peak owned preview count 1, mean 2.744 ms and max 4.677 ms; no stale state or runtime exceptions.

## Acceptance checklist
- [x] Built PropShowcase opens with usable left catalogue and right live production preview.
- [x] All 529 in-scope entries are represented exactly once without a drifting showcase-only identity registry.
- [x] Selecting listed entries renders corresponding production realization.
- [x] Switching removes prior geometry, colliders, lights, particles, world-object state, and owned resources cleanly.
- [x] Framing, grounding, materials, lighting, and construction are production-useful across representative sizes/shapes/backends.
- [x] Catalogue parity, presentation, switching/readiness, Hearth-facing, material, and resource regressions pass.
- [x] Required module-local standalone scenes/scenarios pass through production paths.
- [x] Durable exact built evidence passes direct production-quality visual review.
- [x] Final exact-SHA targeted CI gate passes.
- [x] Complete `issue.json` closure fields and move only this SceneIssue directly from open to closed.
- [x] Merge current `origin/master` into `fixes/agent-9`, push feature branch, open/update final PR, enable auto-merge, pass required `affected`, and verify closed assignment on master. (Performed as the post-closure promotion sequence.)
