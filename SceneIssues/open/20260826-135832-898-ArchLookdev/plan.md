# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in the saved 1928×836 `Hero Arch` pose. Ivy must form lush asymmetric left/crown masses with broad overlapping leaves; blossoms must read as clustered flowers with sparse right masonry, matching the tracked reference intent.
- Generic semantic stamps were a confirmed close-up quality limit. The bounded authored replacement uses 128 ivy leaves plus stems and 30 flower heads in 3 combined hero draws; two ground ferns remain semantic.
- Green tests are not visual proof. Earlier green runs rendered bare foliage until world-space ownership was fixed; later green run `33133265304` rendered the hero but experiment 010 still read as oversized repeated blobs with tiny sparse flowers.

## Proven causes / rejected paths
1. **Generic stamps are insufficient — confirmed.** Hero foliage uses art-directed combined meshes.
2. **Missing shader/mesh — rejected.** Mesh counts/bounds and player shader inclusion were proven.
3. **Camera parenting displaced world-authored geometry — confirmed.** A transform-lifecycle world anchor fixes the real player, including rebuilds; render callbacks were rejected.
4. **Simple silhouette inflation is sufficient — rejected.** Experiment 010 passed CI but failed direct reference comparison.
5. **Layered local composition is the current discriminator.** Experiment 011 keeps topology/budget fixed while redistributing leaf centres into local canopies and rebuilding larger separated five-petal flower clusters.

## Behavioral regression
`ArchReferenceGrowthLushPassTests.LushPassBuildsLayeredCanopiesAndReadableFlowerClustersAcrossRebuild` measures production mesh canopy spread, individual leaf radius ceiling, flower-head radius, 150 visible petals, unchanged 3-draw/<=4,096-vertex budget, and deterministic rebuild behavior.

## Blast radius / cost
- ArchLookdev presentation only; shared vegetation/world truth unchanged.
- 3 hero draws, <=4,096 vertices, one-shot CPU mesh mutation after build, no per-leaf/flower GameObjects and no steady-state geometry work.
- Current master through the binary-evidence workflow clarification is merged; no product overlap.

## Remaining gates
Run fresh exact-SHA targeted PlayMode CI with the assigned 45-second scene replay. Reject unless direct reference comparison shows layered ivy and readable clustered blossoms. Produce clean quality-40 JPEG evidence at exactly 771×334 with overlays hidden, commit pending metadata/move, then after green CI set fixed/resolvedUtc, move to closed, merge current master and non-force push the exact feature head to master.
