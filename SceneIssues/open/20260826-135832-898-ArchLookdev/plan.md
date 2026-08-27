# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in the saved `Hero Arch` pose. The note says both flowers and ivy remain unlike `References/arch_reference.png` despite prior changes.
- `ArchLookdev` auto-attaches `ArchReferenceGrowth`; the captured runtime still expressed hero growth as shared semantic vegetation stamps.
- History shows the same strategy already tried ivy climber rendering, leafy clustering, color variation, flower shader readability, head scale and placement. The new capture says those passes still miss.
- Reference-match notes require close-up trailing ivy, broad leaves/flowers, lush asymmetric left/crown growth and sparse right-side masonry.

## Hypotheses / result
1. **Generic vegetation stamps limit the close-up — confirmed by history/runtime ownership.** Shared card meshes must serve world-scale species and cannot directly author the target's individual lobed leaves and clustered blossoms.
2. **Only density/color/scale is wrong — rejected.** Those variables were already changed repeatedly without satisfying the capture.

## Fix / blast radius / cost
- ArchLookdev only: replace hero ivy/flower stamps with deterministic combined meshes—128 lobed leaves with thin stems and 30 clustered broad-petal flower heads with distinct centres.
- Keep two small ground ferns on the existing semantic renderer; shared vegetation shaders/catalogue/placement and other scenes are unchanged.
- Cost contract: 3 hero draws, <=4,096 authored vertices, one CPU mesh build on enable, no per-leaf/flower GameObjects and no per-frame mesh generation.
- PlayMode regression proves representation, asymmetric coverage, lifecycle restoration and cost bounds through production `ArchReferenceGrowth`.
- Exact request `ea56e952` never exercised product behavior: Unity stopped at CS1625 because test cleanup yielded inside `finally`. Cleanup is corrected with synchronous destruction; this is a harness compile failure, not a product attempt or visual result.
- Merged current master `23691e5f4b0a6cef7b8c6a89b441534cc9ffd7fa` at merge source `80871566047e25ce88eede93946718f40dd7498c`; final CI targets this plan-only successor.
- Remaining gates: green exact-SHA PlayMode CI + original saved-pose replay and direct visual inspection; reject if it still reads as repeated strips/stars.
