# Plan

## Acceptance / ownership
- Canonical production is the existing `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + descendant-aware `FeatureRegionBuild` path; no parallel structural solver.
- Deterministic compatibility/facing/clearance/support/capacity/required semantics, bounded recursion/cost, inspectable graphs, authoritative independently bounded child voxels, decoration handoff, independent fixtures, four showcase families, and production `CharacterMotor` traversal are implemented.
- Gallery presentation/refinement remains composition/evidence only; shared structural semantics stay in `VoxelEngine.Structures` and remain scene/material-ID agnostic.

## Material results / selected approach
- Mechanical run `33314706183` passed focused PlayMode and all three built-player traversals, but manual review rejected blockout visuals.
- Run `33323976945` isolated presentation-budget failure: one oversized bridge context catalogue exceeded conservative voxel cost. Context was partitioned into bounded authoritative voxel catalogues without changing global budgets.
- Run `33324919718` was mechanically green; full-resolution review still rejected bridge gorge/framing, planar castle, weak cliff support read, and facade framing. Those demonstrated defects drove the bounded authoritative-voxel refinement plus tighter evidence framing/preload.
- Run `33325668040` never exercised refined source because the shared runner failed at the editor-wait gate; treated as infrastructure.
- Exact-source run `33330327732` passed focused PlayMode but the real player exposed a product regression: castle route 1 stopped at the front face of `castle-refined-gatehouse`. The refinement’s 12-voxel base spanned the canonical 32-voxel gate opening. Root cause is isolated to composition geometry, not `CharacterMotor` or structural semantics.
- Selected geometry fix: split only the refined gate base around the canonical opening and add focused PlayMode coverage that authors the final refinement then traverses route 1 with the production motor. No tolerance, route, solver, or budget changes.
- Run `33331508390` then failed at compile because that regression reused an `internal` scene-runtime traversal audit. The test assembly already references `VoxelEngine.Showcase`; the selected wiring fix is to make the showcase-specific audit helper public so focused tests and the real-player harness execute one traversal implementation without moving motor logic into lower composition layers.
- Refreshed `fixes/agent-5` onto master `5865c6e04f93c7d2ba0f10258909f38115424607`; the two incoming Kentridge files were preserved unchanged. The branch-only common `SceneIssues/README.md` newline delta was restored to the master blob.

## Next discriminator
Reuse `ci-test/fixes/agent-5` for one exact-SHA request from the corrected feature head. The focused regression class must compile and pass with the final refinement authored, then the real `WorldbuildingGalleryShowcase` player must pass all three traversals/negative contracts and emit all eight frames. Inspect every full-resolution frame directly; only `production-quality` passes.

## Cost / blast radius
- No global structural/device budget or `CharacterMotor` tolerance is weakened.
- Gate fix removes filled voxels from the existing bounded refinement footprint and adds one primitive while remaining below its declared `MaxPrimitives=24`.
- The traversal-audit visibility change exposes only a showcase/evidence helper and does not alter shared structural APIs or runtime behavior.
- Largest authored piece footprint remains within `MaxFootprintVoxels=1280`; each split/refinement catalogue remains below the 16,777,216 conservative voxel ceiling.
- Final run must record bridge/castle planning time, children/primitives/voxel budget, regions/instances/writes, authoring/presentation time, memory and render-region proxy.

## Remaining gates
- Green exact-SHA focused + built-player run and three traversals/negative contracts.
- Production-quality review of all eight structural frames.
- Final assignment-only diff/cost review; all required `tasks.md` boxes complete.
- Pending metadata/open→pending, then fixed/resolvedUtc pending→closed only after validation; refresh master again and non-force push exact feature head to `origin/master`.
