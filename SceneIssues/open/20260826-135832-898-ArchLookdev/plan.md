# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in the saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported left/crown masses with overlapping English-ivy leaves and a few drapes; blossoms must read as small layered bouquets; right masonry stays sparse.
- Preserve the bounded hero: 128 leaves, 30 flower heads, 3 combined draws, <=4,096 vertices, two semantic ground ferns.
- Green tests are not visual proof. Experiments 010–012 passed geometry gates but were rejected from their player frames as blobs/rope/star cutouts.

## Proven causes / discriminator
1. **Generic stamps / missing render data — rejected.** Authored meshes render in the player.
2. **Camera parenting — confirmed earlier.** World-space lifecycle anchoring fixes actual player placement/rebuilds.
3. **Experiment 013 composition is sufficient — rejected.** Exact request `4df21b03...` / run `33139132235` failed with a surviving stem extent `0.0827498`; its replay frame shows the same diagonal line, isolated repeated leaves, and oversized flat five-point blossoms.
4. **Experiment 014 — current.** Collapse every topology stem plus a stem-color fallback; rebuild leaf centres as dense overlapping pier/crown masses with depth and drapes; reconstruct the existing flower topology into smaller rounded three-head bouquets.

## Behavioral regression
`ArchReferenceGrowthEnglishIvyPassTests.EnglishIvyPassBuildsStemFreeMassesAndRoundedBouquetsAcrossRebuild` runs the production stack and requires: visible lush stems before the final pass, zero post-pass stem extent, larger bounded left foliage vs sparse right, non-flat leaf/flower depth, tighter bouquet spread, same mesh identities/topology/3 draws/<=4,096 vertices, and deterministic rebuild reapplication.

## Blast radius / cost
ArchLookdev presentation only; shared vegetation/world truth unchanged. Same 3 meshes/draws and vertex count; one-shot CPU mesh mutation only, with no per-leaf/flower GameObjects or steady-state geometry work.

## Remaining gates
Implement experiment 014, refresh current master, issue one exact-SHA final PlayMode request with the original 45-second replay, and inspect the real-player saved pose. If both test and visual gate pass, record verification, populate pending metadata/open→pending, then fixed/resolvedUtc pending→closed, merge current master and non-force push the exact feature head to master.
