# Plan

## Captured evidence / acceptance
- `screenshot-001.png` has no marked regions (`circles: []`); the whole-frame defect is that the tuned ArchLookdev hero arch is not used by production Kentridge.
- Saved `Hero Arch Camera` replay identifies the target as a tall recessed masonry bay with stout piers and a readable segmented voussoir ring.
- Ownership discriminator: ArchLookdev uses `ArchBayAuthoringPipeline`/`ArchFeatureDefinition`; Kentridge landmarks used `ArchitectureVoxelPatterns.FramedArchedOpening`, a separate simpler construction path.

## Hypotheses / discriminator
1. **Lookdev construction never reaches Kentridge. Supported:** production and lookdev used separate authoring paths.
2. **The same construction exists but parameters/presentation hide it. Falsified:** production emitted the simpler surround/carve path.
- Two early exact-CI compile failures exposed missing `VoxelEngine.Storage.Api` scope and were fixed before runtime validation.

## Fix / regression / evidence
- Kentridge's shared shape-program catalogue now preserves the arched clearance/surround while adding twelve non-destructive radial masonry joints for a 13-piece hero-voussoir rhythm; window-scale glazed arches retain their continuous treatment.
- Regression: `VoxelEngine.Tests.PlayMode.KentridgeInteriorScaleTests.ProductionCatalogue_LandmarkEntrancesCarryHeroVoussoirSeams` covers warehouse, mansion, and church production entrance programs.
- Product/test fix: `b925d43c62aba29855c3d32033bcf401a6ef8264`.
- Exact request `f77d9f1658cbcd1690036074f1460a508ff18dad` passed `ci/single-test` and real-player replay. The replay was inspected against the original pose: recessed opening, stout supports, segmented masonry rhythm, clearance, and intersections are readable with no debug/replay overlay.
- Final evidence follows current canonical policy: `verification-final.jpg`, quality 40, 771x334 (exactly 40% of 1928x836).

## Blast radius / cost
- Only Kentridge landmark entrances using the shared arched-opening pattern gain the treatment; glazed windows, generated houses, Hightown, castle authoring, and unrelated catalogues remain unchanged.
- Runtime cost is unchanged; generation adds only twelve bounded surface-detail capsules per selected landmark entrance. Replay readback is development-only.
