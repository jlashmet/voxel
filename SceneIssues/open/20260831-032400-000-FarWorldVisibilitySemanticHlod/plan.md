# Far-World Visibility Implementation Plan

## Acceptance and ownership

Keep terrain, semantic structures, settlements, forests, landmark natural features, and ordinary scatter visually coherent from the resident voxel boundary through the configured horizon without making distant voxel regions resident. World truth remains deterministic CPU data; WorldBuilder stays renderer-neutral; VoxelEngine Rendering consumes render-ready contracts; scene thresholds/named-content policy remain in composition. Built-player evidence, not editor screenshots, is required for visible acceptance.

Required final proof includes: 12 km guaranteed terrain coverage through camera snap phases; never-resident landmark visibility; stable semantic/proxy/cluster handoffs; deterministic settlement/vegetation clustering and persistent state propagation; whole-range terrain material/lighting continuity; and measured CPU/GPU/memory/draw cost against the device matrix.

## Current hypotheses / discriminators

- **Coverage:** the old `innerRadius * 2` heuristic under-covered because it ignored actual spacing and camera snap loss. T001 now selects the minimum ring count from worst-case snapped extent.
- **Fallback retirement:** sampled heights are not equivalent to published authoritative coverage because the startup fallback occupies the final ring mesh. T002 keeps that mesh fallback-owned until every lower required ring is current/published and the final ring has current samples ready for atomic publication.
- **Diagnostics:** validation needs publication truth, not configured potential coverage. T003 reads current contiguous authoritative publication and fallback state without feeding those diagnostics back into world/render authority.
- **Far terrain fidelity:** correct renderer-neutral terrain-family/detail inputs before adding geometry density; add an inner density tier only if built-player evidence still shows silhouette loss.

## Material results

- Exact-SHA run `33801450701` passed module validation and standalone-player replay for source `4b9622ace9e4ca27ecf5da25900524341cffd8fe`, clearing the previous shared Rendering blocker.
- T001 now derives spacing, half-extent, snap loss, per-phase/guaranteed coverage, and minimum ring count. Shipped ~409.6 m / 96-cell / 12 km requires six rings; five guarantee only ~9.63 km. `VoxelFarTerrainCoverageTests` covers snap phases and the max-ring failure case.
- T001 exact-SHA run `33806714712` completed successfully for source `82ba46b7450435d278482fa90391fa5cba381f50`. The CI transport was not replaced while that request was queued/running.
- T002 root cause is isolated: the old completion path rebuilt the final ring mesh and disabled fallback solely when that ring's job finished, allowing an intermediate publication hole. `FarTerrainStartupCoverage` now models contiguous publication/retirement, and regressions prove early final-ring readiness cannot retire fallback and the legal transition cannot shrink coverage.
- Runtime integration tracks authoritative publication separately from sampled heights, protects the fallback-owned final mesh from normal rebuild/refresh, and atomically publishes the final ring only after all lower current rings plus guaranteed final coverage are ready.
- T003 adds `VoxelFarTerrain.CoverageSnapshot` plus `CoverageDiagnostics` and per-ring spacing diagnostics. The snapshot reports requested radius, current guaranteed contiguous authoritative radius, configured ring count, and actual startup-fallback state; it is read-only presentation/debug data. Focused coverage tests prove configured six-ring/12 km diagnostics before publication.
- `VoxelShowcase.DescribeFarTerrain()` now formats requested radius, authoritative radius, ring count/spacings, and fallback state into the existing scene diagnostic surface. An intermediate connector edit accidentally rewrote the full file from truncated content; commit `da14c4d2bc437dd1747de271f634db30f2b9b770` restored the exact prior blob, and the reapplied diagnostics commit `6fbee714aaa52038a7aa436c1b048f851c3ff473` was verified to change only the intended `DescribeFarTerrain()` lines.

## Current head and remaining gates

T001 has exact-SHA green evidence. T002 and T003 implementation are complete but remain unchecked until a new exact-head validation passes. The next validation must use only `ci-test/fixes/agent-7` and must target the feature head including this plan update. Do not close until all T001-T033 requirements, built-player visual proof, and budget gates are complete.
