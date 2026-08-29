# Experiment 009 — startup camera relocation

## Hypothesis
The exact replay camera can move after `VoxelFarTerrain` creates its zero-sampling startup fallback. If that fallback remains centered on the old camera, one unresolved 4 km corner can cross the new capture view and look like the marked grass/teal shelf until an unrelated async ring publishes.

## Action / source
Production `a5f0f474e0f9f91e64c3c3ca017c52f6a3ebc150` tracks the fallback camera XZ and rebuilds the startup-only 8-vertex/8-triangle mesh after camera motion crosses the existing threshold. The rebuild excludes both the contiguous published-ring footprint and one current critical-ring footprint. Regression `8fbd62ab3474dbe90dbc37ba7d27623cbaecebaa` completes the outstanding worker without publishing it, relocates the camera 900 m, yields one production `LateUpdate`, then inspects this component's exact fallback mesh.

## Discriminator
Before another ring publishes, the fallback bounds must recenter on the moved camera, must not cover either the new current-camera footprint or the already-published critical footprint, and must still cover unresolved horizon space outside them.

## Result / verdict
The implementation and regression encode the capture-specific ownership invariant without depending on worker throughput or global mesh-name lookup. Final verdict remains gated on fresh exact-SHA targeted CI plus the 60 s built-player replay of every original marked region.
