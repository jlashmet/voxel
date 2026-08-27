# Plan

## Captured evidence
- `screenshot-001.png` has no marked regions (`circles: []`), so the whole-frame note is the defect: the tuned hero arch must reach production Kentridge.
- Replaying the saved `Hero Arch Camera` pose through targeted CI shows the lookdev target as a tall masonry bay with a recessed opening, visibly segmented/projecting voussoirs, stout piers, and weathered/grown-over stone. The captured controls are approximately span 28, pier 64, ring 7, 13 voussoirs, depth 12, shoulder 4, top margin 4.
- Code ownership matches the note: `ArchLookdev` calls `StructuresComposition.BuildArchLookdev`, which routes to `ArchBayAuthoringPipeline`/`ArchFeatureDefinition`. Kentridge landmarks instead call `ArchitectureVoxelPatterns.FramedArchedOpening`, which emits one arch prism surround plus two carves.

## Competing hypotheses / discriminator
1. **Isolated lookdev ownership**: Kentridge never consumes the authored hero-arch construction. **Supported** by the separate production call paths above.
2. **Shared construction, wrong parameters/overlay**: Kentridge already consumes the same arch but presentation makes it drift. **Falsified** because its current entrance is a different one-piece primitive path, not `ArchFeatureDefinition`/the lookdev construction.

## Fix and regression
- Add the smallest reusable production bridge that lets Kentridge author the hero-arch vocabulary through its existing shape-program catalogue, then use it for the landmark arched entrances already selected by Kentridge. Do not copy scene-only policy into the town.
- Add a behavioral regression through the production Kentridge catalogue/program that proves a landmark entrance emits the hero treatment (segmented ring/projection plus preserved walkable carve), rather than asserting source text/constants.
- Re-run the saved ArchLookdev replay and the focused regression on the exact feature SHA.

## Blast radius / cost
- Consumers: Kentridge landmark entrance programs using the shared arched-opening pattern; generated houses, Hightown, castle authoring, and unrelated feature catalogues remain unchanged.
- Cost is bounded generation-time work per selected landmark entrance; no per-frame/runtime-update cost. Keep primitive growth within existing `MaxPrimitives`/feature budgets and preserve door anchors and clearances.
