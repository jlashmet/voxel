# Far-World Visibility Implementation Plan

## Acceptance and ownership

Keep terrain, semantic structures, settlements, forests, landmark natural features, and ordinary scatter visually coherent from the resident voxel boundary through the configured horizon without making distant voxel regions resident. World truth remains deterministic CPU data; WorldBuilder stays renderer-neutral; VoxelEngine Rendering consumes render-ready contracts; scene thresholds/named-content policy remain in composition. Built-player evidence, not editor screenshots, is required for visible acceptance.

Required final proof includes: 12 km guaranteed terrain coverage through camera snap phases; never-resident landmark visibility; stable semantic/proxy/cluster handoffs; deterministic settlement/vegetation clustering and persistent state propagation; whole-range terrain material/lighting continuity; and measured CPU/GPU/memory/draw cost against the device matrix.

## Current hypotheses / discriminators

- **Coverage:** the old `innerRadius * 2` ring heuristic can under-cover because it ignores actual spacing and camera snap loss. Discriminator: compute outer-ring half-extent minus worst-case one-cell snap loss and require that value to cover the requested radius.
- **Far terrain fidelity:** material/detail mismatch may dominate perceived smoothness; correct renderer-neutral terrain-family/detail inputs before adding geometry density. Add an inner density tier only if built-player evidence still shows silhouette loss.
- **Semantic visibility:** known structures/trees/scatter should use deterministic lightweight descriptors/proxies/clusters rather than distant voxel residency or terrain-vertex coincidence.

## Material results

- Exact-SHA run `33801450701` passed automatic module validation and standalone-player SceneIssue replay for source `4b9622ace9e4ca27ecf5da25900524341cffd8fe`, clearing the previously external Rendering GPU/arena blocker for already-integrated T025-T028 work.
- Checklist reconciliation found T001 genuinely incomplete: `VoxelFarTerrain.RingCount` still used the old doubling heuristic.
- T001 implementation now derives ring spacing, half-extent, snap loss, per-phase coverage, guaranteed coverage, and minimum required ring count. For the shipped ~409.6 m inner radius / 96-cell / 12 km target, five rings guarantee only ~9.63 km while six guarantee ~19.25 km.
- New module-local `VoxelFarTerrainCoverageTests` covers representative and worst-case snap phases, minimum six-ring selection, helper values, and an explicit max-ring uncovered result.

## Current head and remaining gates

Current feature head: `b72c4e76990bfc003ce366c52456132a7c0cdfa8` (T001 implementation + regression metadata). T001 remains unchecked until exact-SHA CI passes. After T001 validation, continue strictly with T002, then T003/T003A... in checklist order, recording genuine blockers and independently advancing only non-blocked acceptance work. Do not close until all T001-T033 requirements, built-player visual proof, and budget gates are complete.
