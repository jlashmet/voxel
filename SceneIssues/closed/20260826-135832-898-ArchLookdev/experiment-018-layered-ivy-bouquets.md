# Experiment 018 — layered ivy silhouettes and asymmetric bouquets

## Captured runtime evidence
- Experiment 017 source `5fe1c7d82960e39245f0720d726f21f81dcdd922`; exact request `2d6cee06d709605080ce8b0bbe15876ef2adb752`; run `33146173563` is green, including the exact rebuild regression and 45-second real-player replay.
- Direct inspection of that run's `RealPlayer/verification-final.png` still rejects the result: the foliage is finally on the left masonry, but oversized translated leaves merge into two dense horizontal shelves/blobs; the crown is not a readable ivy mass; and the 30 flowers repeat as small icon-like scallops with orange dots instead of layered bouquets.

## Competing hypotheses
1. **World anchoring was the remaining cause — rejected.** Experiment 017 fixes the floating opening mass but leaves a bad silhouette.
2. **Moving the same oversized cards again will fix it — rejected.** The captured shelves are caused by leaf scale/packing, not only centroid position.
3. **Current.** Rewrite each existing 17-vertex leaf around its supported centre with a smaller varied English-ivy silhouette, deterministic rotation, front-layer depth, irregular vertical/sloped packing, and per-leaf green variation. Re-layout all existing flower heads directly within three asymmetric bouquet footprints so they no longer repeat the same three-head icon.

## Behavioral gate / cost
The regression must still recover exactly 128 leaf cards, hold the three left foliage/bouquet centroids to masonry envelopes and out of the opening, require separated zones, bounded varied leaf radius plus vertical/sloped cluster footprints, varied flower-head scale/spacing and depth, deterministic rebuild, unchanged mesh identity, 3 draws, and <=4,096 vertices. ArchLookdev only; no new vertices/renderers/GameObjects and no per-frame work.