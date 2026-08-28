# Experiment 005 — deterministic post-build world anchor

## Hypothesis
The hero mesh is correct but disappears because callback delivery never applies its world-space contract in the standalone player. The development replay proved the saved camera pose while the one-shot `ARCH_REFERENCE_ANCHOR` marker was absent, so neither the legacy nor SRP render hook is a reliable ownership boundary here.

## One change
Remove render/scene callback delivery from `ArchReferenceGrowthWorldSpace` and invoke the same `AnchorCamera` operation synchronously at the end of `BuildHeroPresentation`, immediately after the three combined mesh children exist. Geometry, depth, materials, density, camera, and environment remain unchanged.

## Regression
The PlayMode regression must no longer invoke `AnchorCamera` itself. After one frame it must find `Arch Reference Hero Growth` already detached at world identity, reproducing the captured camera-host ownership without test-only repair.

## Validation
1. Exact-SHA PlayMode regression passes with no manual anchor call.
2. Standalone player log contains the one-shot anchor marker before replay pose verification.
3. Original saved Hero Arch replay visibly shows the authored ivy/flower mass. If the marker is present but the mass is buried/fragmentary, the next hypothesis is surface depth rather than lifecycle.
