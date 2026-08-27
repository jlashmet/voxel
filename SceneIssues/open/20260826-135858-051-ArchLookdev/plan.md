# Plan

## Captured evidence / acceptance
- `screenshot-001.png` has no marked regions (`circles: []`), so the whole-frame note is the defect: the tuned hero arch must reach production Kentridge.
- Saved `Hero Arch Camera` replay identifies the target as a tall recessed masonry bay with projecting segmented voussoirs and stout piers. Approximate lookdev controls: span 28, pier 64, ring 7, 13 voussoirs, depth 12, shoulder 4, top margin 4.
- Ownership discriminator: `ArchLookdev` routes through `ArchBayAuthoringPipeline`/`ArchFeatureDefinition`; Kentridge landmarks used `ArchitectureVoxelPatterns.FramedArchedOpening`, a separate one-piece surround/carve path.

## Hypotheses / results
1. **Lookdev construction never reaches production Kentridge. Supported:** production and lookdev use separate authoring paths.
2. **Production already uses the same construction but presentation/parameters hide it. Falsified:** Kentridge emitted the simpler primitive path.
- Exact request `4655349e97d904ded223cf562b011503d446db17` reached the real runner but Unity compile-aborted before regression/replay: `ArchitectureVoxelPatterns.cs:352` could not resolve `SurfaceStyles`. Neighboring `ArchitectureShapeProgramBuilder` resolves the same type via `VoxelEngine.Storage.Api`; commit `cdfbce730207ee478d22e7750dfba96569afa212` adds only that missing import.

## Fix / regression / remaining gates
- Reuse Kentridge's existing shape-program catalogue to add the hero entrance vocabulary: preserve the arched clearance/surround, add twelve non-destructive radial masonry joints for a 13-piece voussoir rhythm, and keep window-scale glazed arches on their existing continuous treatment.
- Behavioral regression: `VoxelEngine.Tests.PlayMode.KentridgeInteriorScaleTests.ProductionCatalogue_LandmarkEntrancesCarryHeroVoussoirSeams` proves production warehouse, mansion, and church entrance programs retain an arch carve and exactly twelve hero masonry seams.
- Remaining: green exact-SHA targeted CI, clean native-resolution saved-pose replay, direct comparison with `screenshot-001.png`, `verification-final.png`, pending metadata, close, current-master merge, non-force master push.

## Blast radius / cost
- Only Kentridge landmark entrances using the shared arched-opening pattern gain the treatment; glazed windows, generated houses, Hightown, castle authoring, and unrelated catalogues remain unchanged.
- Cost is bounded generation-time primitive growth (12 surface-detail capsules per selected landmark entrance), no per-frame work; preserve existing primitive budgets, door anchors, and clearances.
