# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- The capture has no circles: acceptance is global in the saved `Hero Arch` pose. The note says both flowers and ivy remain unlike the reference despite prior changes.
- `ArchLookdev` auto-attaches `ArchReferenceGrowth`; that component currently art-directs positions but still submits hero growth as generic `VegetationInstance` stamps through the shared vegetation renderer.
- History confirms repeated same-strategy attempts: ivy was changed to climber rendering, then clustered into leafy masses, then given color variation; flowers received a shader readability pass. The new capture says that family of fixes still misses.
- The reference-match notes call for close-up trailing ivy, broad leaves and flowers, with lush asymmetric growth on the left/crown and sparse right-side masonry.

## Hypotheses / discriminator
1. **Generic vegetation stamps are the limiting representation — selected.** Their shared card meshes must serve world-scale species and cannot directly author the reference's individual lobed leaves and clustered flower heads. Falsifier: a bounded scene-owned hero mesh still reads like repeated strips/stars in the saved pose.
2. **Only density/color/scale is wrong — rejected.** Those variables were already changed repeatedly without satisfying the capture; another constant-only pass repeats failed experiments.

## Fix / blast radius / cost
- Replace only ArchLookdev's hero ivy/flower stamps with deterministic combined mesh presentation: lobed leaves plus thin stems, and clustered broad-petal flowers with distinct centers.
- Keep the two small ground ferns on the existing semantic renderer. Do not change shared vegetation shaders, catalogue, placement, or other scenes.
- Budget: <= 4 hero draw calls, <= 4,096 authored hero vertices, deterministic CPU build once on enable; no per-leaf GameObjects or per-frame mesh generation.
- Add a PlayMode behavioral regression through `ArchReferenceGrowth` that proves dense asymmetric hero leaves/flowers, bounded mesh/draw cost, and only ground ferns remain semantic.
- Final gate: exact-SHA regression green plus replay of the original saved pose with direct visual inspection against the reference intent.
