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

**Result: confirmed red for the intended behavioral assertion.** Unity executed exactly one test case. The fixture emitted transition geometry, then failed at vertex 0 because its normal was `float3(0f, 0f, 1f)`: the tangential component length was `0.0`, below the required `> 0.2`. Setup, checkout, request resolution, and Unity startup all succeeded; the failure came from the requested test itself, not compile or runner setup.

This confirms the isolated invariant violation: a slanted density field reaches `TransitionMeshJob`, but the emitted transition shading normal discards that slope.

## Next

1. Merge current `master` into `fixes/agent-4` before the production change, preserving the confirmed red regression.
2. Implement the smallest transition-normal change that derives normals from the transition density samples and remains consistent with the regular surface gradient convention.
3. Re-run this exact focused regression through `ci-test/fixes/agent-4` with a new request id.
4. Run the exact saved-camera replay and inspect the retained 1364x836 evidence at all three marked regions. A green unit test is not sufficient to close this capture.
