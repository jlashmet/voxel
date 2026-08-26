# Experiment 12 — transition-face normals preserve surface slope

## Hypothesis

The thin bright/dark strips and serrated-looking bands that survive both LOD ownership filtering and detail-band expansion are caused by transition geometry using a constant face-axis normal instead of the density-field slope. On slanted terrain this makes the one-cell transition slab shade as a coherent stripe even when its geometry is otherwise correctly stitched.

## Minimal reproduction

Added `Assets/Tests/EditMode/TransitionMeshJobNormalTests.cs`, a one-cell production-faithful `TransitionMeshJob` fixture with a slanted 3x3 face density field. The test uses the real Transvoxel transition tables and asserts that every emitted transition vertex retains a material tangential normal component pointing with the negative density gradient.

This isolates transition shading from scene streaming, renderer ownership, materials, camera/frustum logic, and LOD distance policy.

## Current production behavior

`TransitionMeshJob` currently emits every transition vertex with `Normal = normalize(-wAxis)`, so all geometry on a stitched face receives the same face-axis normal regardless of the supplied face density field. Regular `TransvoxelTopologyJob` instead derives vertex normals from the sampled density gradient.

The production face snapshot already samples the finer-neighbour face at half this ring's stride; `TransitionMeshJob` receives those density samples but currently discards their tangential gradient when assigning normals.

## Red verification

Targeted CI request commit: `d97156aeb96c17c283e47a97f246f8b03dbd51c4`

Requested test:
`VoxelEngine.Tests.EditMode.TransitionMeshJobNormalTests.SlantedFaceFieldEmitsNormalsWithTangentialComponent`

Actions run: `32997768169`

At the time this experiment record was created, the run was queued behind another repository job on the self-hosted macOS runner. Do not alter production code until this request completes and the failure is confirmed to be the intended normal-gradient assertion rather than compile/setup failure.

## Next

1. Confirm the isolated test fails on unchanged production `TransitionMeshJob` for the expected missing tangential normal component.
2. Implement the smallest transition-normal change that derives normals from the transition density samples and remains consistent with the regular surface gradient convention.
3. Re-run this exact focused regression through `ci-test/fixes/agent-4` with a new request id.
4. Run the exact saved-camera replay and inspect the retained 1364x836 evidence at all three marked regions. A green unit test is not sufficient to close this capture.
