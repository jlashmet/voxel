# Experiment 013 — transition edge gradients need spacing normalization

## Hypothesis

The slope-aware transition-normal fix is directionally correct, but clamped central differences at the face-snapshot boundary distort the gradient direction because a one-sided sample span is treated like a two-sided span.

## Action / source

Ran `VoxelEngine.Tests.EditMode.TransitionMeshJobNormalTests.SlantedFaceFieldEmitsNormalsWithTangentialComponent` through targeted CI against production source commit `3810aeb74abb07d9722dea2728e96cef7af10368` (request `de5ceab1b82fb0524b0dfe00aec80e6f15906955`, Actions run `33015287267`). Inspected the uploaded `single.xml` result.

## Result

**Confirmed product failure.** Exactly one test ran and failed at transition vertex 2. Its normal was `float3(-0.3810148, -0.8424106, 0.3810146)`: it retained a tangential component, but the tangential direction missed the expected negative density-gradient direction (`dot <= 0.8`).

At a face edge, `FaceIndex` clamps one side of a central difference onto the center sample. That makes the edge-axis difference span one sample while an interior-axis difference spans two samples. Using the raw differences therefore changes their relative magnitudes and rotates the reconstructed slope.

## Verdict

Hypothesis confirmed. The transition path must divide each finite difference by the actual clamped sample separation before combining the tangential components.

## Next

Normalize the U and V differences by their real one-sided/central spans, rerun the focused regression on the current master-merged feature head, then run the exact saved-camera replay if green.
