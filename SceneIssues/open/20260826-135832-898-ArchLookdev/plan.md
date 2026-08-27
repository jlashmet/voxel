# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in the saved `Hero Arch` pose. The note says both flowers and ivy remain unlike `References/arch_reference.png` despite prior changes.
- `ArchLookdev` auto-attaches `ArchReferenceGrowth`; history shows generic semantic stamps already received ivy clustering/color and flower readability/scale passes without satisfying the capture.
- Reference intent is close-up trailing ivy with broad leaves and clustered flowers: lush asymmetric left/crown growth, sparse right masonry.
- Pre-merge source `a9cf4118` passed its structural test/replay workflow, but direct replay inspection showed an essentially bare arch. The bespoke meshes existed; they were parented to the moved Hero Arch Camera even though their vertices use world-space arch coordinates.

## Hypotheses / results
1. **Generic vegetation stamps limit close-up quality — confirmed.** Replace only hero ivy/flowers with bounded art-directed combined meshes.
2. **Only density/color/scale is wrong — rejected.** Those constants were already iterated repeatedly.
3. **Bare bespoke replay is camera-space displacement — confirmed.** Camera parenting applies the saved pose a second time; this, not mesh absence, explains the visual result.

## Fix / blast radius / cost
- ArchLookdev hero: 128 lobed ivy leaves with thin stems and 30 clustered broad-petal heads with distinct centres; two ground ferns remain semantic.
- One-shot ArchLookdev pre-cull anchor detaches only `Arch Reference Hero Growth` to world identity, then unsubscribes. No steady-state callback cost.
- Cost contract: 3 hero draws, <=4,096 authored vertices, one CPU mesh build on enable, no per-leaf/flower GameObjects or per-frame mesh generation. Shared vegetation/other scenes unchanged.
- PlayMode regression reproduces the captured camera transform and proves world anchoring, representation, asymmetric coverage, lifecycle restoration, and cost bounds.
- Earlier request `ea56e952` was a test-harness compile failure; corrected pre-merge run `33047792106` exposed the camera-space product defect visually.
- Remaining gates: refresh/merge current master, green exact-SHA PlayMode CI, clean native-resolution original-pose replay, direct comparison to the reference intent; reject if flowers/ivy are still absent, strip/star-like, or placeholder quality.
