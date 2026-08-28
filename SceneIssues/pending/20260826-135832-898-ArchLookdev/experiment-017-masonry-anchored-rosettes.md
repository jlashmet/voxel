# Experiment 017 — masonry-anchored foliage and rosette bouquets

## Captured runtime evidence
- Experiment 016 source `f8d79180bf784e2626284dfce67b7753009ae7f0`; exact request `75815c0e97f37a0a742bf352f79a242d9dfd4435`; run `33145729562` is green, including the production rebuild regression and 45-second player capture.
- Direct inspection of that run's `RealPlayer/verification-final.png` rejects the result: a dominant leaf/flower mass floats inside the arch opening, isolated leaf blobs sit on the piers, and the pale flower geometry reads as one white patch with orange dots rather than layered bouquets.
- Therefore compactness/roundness alone is insufficient. Green CI is not visual proof.

## Competing hypotheses
1. **The green experiment-016 geometry is sufficient — rejected visually.** It satisfies topology/compactness but violates masonry support in the saved pose.
2. **Further relative centroid compression will fix placement — rejected.** The inherited high-path centroid itself is inside the opening, so compressing around it preserves the wrong support frame.
3. **Current.** Recompose the same leaf/head topology around stable ArchLookdev world-space masonry anchors: lower-left pier, upper-left pier, and left crown/shoulder. Keep irregular overlap/drape depth locally; place all three flower bouquets on those same supported zones; tune petal material/color so individual rounded heads remain distinguishable.

## Behavioral gate / cost
Regression must still recover 128 leaves and preserve mesh identity/3 draws/<=4096 vertices/rebuild determinism, but now also bounds each left mass and bouquet centroid to the masonry-side world envelope and rejects any left mass centroid drifting into the central opening. No new vertices, renderers, GameObjects, or per-frame work.