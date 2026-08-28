# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in the saved 1928×836 `Hero Arch` pose. Ivy must read as distinct asymmetric left/crown masonry-supported masses with pointed overlapping leaves and a few hanging drapes; blossoms must be clearly readable clustered flowers, while right masonry stays sparse.
- The bounded authored hero remains 128 leaves plus stems and 30 flower heads in 3 combined draws; two small ground ferns remain semantic.
- Green tests are not visual proof. Experiment 010 passed CI but read as oversized blobs. Experiment 011 source `dbd7d478...` also passed exact request `bf94047c...` / run `33136892735`, yet direct reference comparison still showed a diagonal rope of rounded cards and tiny flowers.

## Proven causes / current discriminator
1. **Generic stamps insufficient — confirmed.** Hero foliage uses art-directed combined meshes.
2. **Missing shader/mesh — rejected.** Player includes/renders the authored meshes.
3. **Camera parenting displaced world-authored geometry — confirmed.** Transform-lifecycle world anchoring fixes real-player placement/rebuilds; render callbacks were rejected.
4. **Scale-only and centre-redistribution passes are sufficient — rejected visually.** Both remained procedural/repetitive at the saved pose.
5. **Experiment 012 — current.** Source `182d2939...` keeps topology/lifecycle/budget fixed but rewrites the final leaf silhouette to deeper pointed ivy, separates height-dependent left masses onto masonry, adds deterministic drapes/depth layers, and enlarges/separates foreground flower heads.

## Behavioral regression
`ArchReferenceGrowthLushPassTests.LushPassBuildsLayeredCanopiesAndReadableFlowerClustersAcrossRebuild` measures production canopy spread, bounded leaf radii, flower-head radius, 150 visible petals, unchanged 3-draw/<=4,096-vertex budget, and deterministic rebuild behavior.

## Blast radius / cost
- ArchLookdev presentation only; shared vegetation/world truth unchanged.
- Same 3 hero draws and <=4,096 vertices; one-shot CPU mesh mutation, no per-leaf/flower GameObjects and no steady-state geometry work.
- Current master `f2fa719b` is merged with no Arch overlap.

## Remaining gates
Run fresh exact-SHA targeted PlayMode CI with the original 45-second scene replay. Reject unless direct reference comparison shows pointed separated ivy masses/drapes and readable clustered blossoms. If accepted, record `verification-replay.txt`, populate pending metadata and move open→pending, then set fixed/resolvedUtc and move pending→closed, merge current master and non-force push the exact feature head to master.
