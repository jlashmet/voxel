# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in the saved 1928×836 `Hero Arch` pose. Ivy must read as distinct asymmetric left/crown masonry-supported masses with broad overlapping English-ivy leaves and a few hanging drapes; blossoms must be clearly readable bouquets, while right masonry stays sparse.
- The bounded authored hero remains 128 leaves plus stems and 30 flower heads in 3 combined draws; two small ground ferns remain semantic.
- Green tests are not visual proof. Experiments 010 and 011 passed CI but remained blobs/rope. Experiment 012 also passed exact request `b2bbd95f7349542a2ea526664d3ecdeb260138ac` / run `33138311319`; its inspected player frame still showed a diagonal garland of radial maple/star cutouts with sparse flat blossoms, so it was rejected.

## Proven causes / current discriminator
1. **Generic stamps insufficient — confirmed.** Hero foliage uses art-directed combined meshes.
2. **Missing shader/mesh — rejected.** Player includes/renders the authored meshes.
3. **Camera parenting displaced world-authored geometry — confirmed.** Transform-lifecycle world anchoring fixes real-player placement/rebuilds; render callbacks were rejected.
4. **Scale-only, centre redistribution, and deep radial notches are sufficient — rejected visually.** They remained procedural/repetitive or made the leaves read as stars.
5. **Experiment 013 — current.** A final one-shot pass mutates the same meshes after lush composition: broad five-lobed English-ivy outline, collapsed inter-cluster connector quads, separated lower/mid/crown mass shifts, shallow leaf depth/color hierarchy, and tighter foreground bouquets using the same 30 heads.

## Behavioral regression
`ArchReferenceGrowthEnglishIvyPassTests.EnglishIvyPassRemovesGarlandConnectorsAndBuildsBouquetsAcrossRebuild` proves the visible connector becomes degenerate, bouquet spread materially tightens, leaves retain bounded readable size plus shallow depth, the same meshes/3-draw/<=4,096-vertex budget are preserved, and the production rebuild deterministically reapplies the pass.

## Blast radius / cost
- ArchLookdev presentation only; shared vegetation/world truth unchanged.
- Same 3 hero draws and <=4,096 vertices; one extra one-shot CPU mesh mutation component, no per-leaf/flower GameObjects and no steady-state geometry work.
- Master was merged through `d1b9dfd7` before experiment 013; refresh again before its exact final request.

## Remaining gates
Refresh current master, run fresh exact-SHA targeted PlayMode CI for the experiment-013 regression with the original 45-second scene replay, and reject unless direct saved-pose inspection clears the star/garland/flat-bouquet failure. If accepted, record durable text verification, populate pending metadata and move open→pending, then set fixed/resolvedUtc and move pending→closed, merge current master and non-force push the exact feature head to master.
