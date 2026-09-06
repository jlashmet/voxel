# Experiment 037 — streaming coverage radius root cause

## Trigger
The strict built-player evidence sequence failed to advance after Moordell in two materially different executable states:

1. Retry 5 on pre-restoration renderer source `7e6d30858677f2504763e891289293c9507cfd9f`.
2. Post-master-sync source `5185b5acddd3d35ec29730ea73d30ae796a705bf`, where run `33800775569` passed repository-derived module validation, the requested perimeter-foundation regression, and the standalone player process.

Because the same `HasCompletePublishedNearSurfaceCoverage()` symptom survived the renderer restoration/master integration, another speculative fix or identical retry is not allowed. This experiment isolates the geometry contract first.

## Evidence
Run `33800775569` proves the macro catalogue is present (`definitions=480`) and Moordell becomes content-ready around 85 seconds. Its residency diagnostic reports `loadRadius=3`, `horizontalColumns=29`, `residentInRadius=29`, and no feature vertical extras. Nevertheless the elevated survey remains blocked by strict renderer publication coverage. Representative runtime output is `inner=153.6m`, `streamed=153.6m`, `residentGround=110.2m`, `coverage=False`.

`KentridgeMacroWorldResidencyCostDiagnostic` independently documents ShowcaseWorld's horizontal admission lattice: a column is in the radius iff `dx*dx + dz*dz <= radius*radius`. At radius 3 this is exactly 29 resident X/Z columns.

The playable slice configured the renderer near-surface ring as `loadRadiusRegions * RegionMetres`, or `3 * 51.2 = 153.6m`. That treats a discrete circle of region *centres* as though it guaranteed a continuous metric disk to the same radius around every possible demand point inside the centre region. It does not.

For a demand point at the +X/+Z edge of its centre region, the closest excluded radius-3 lattice cell begins two complete region widths away. Therefore the largest continuous disk that the discrete admission rule guarantees for every within-cell demand position is `(R - 1) * RegionMetres`. For R=3 this is `102.4m`. The observed `residentGround=110.2m` is consistent with a demand point that is not exactly at the worst-case cell corner.

The high survey camera exposes the outer part of the nominal 153.6m renderer ring. Chunks there can be visible while their source columns are outside the guaranteed resident disk, keeping `MissingVisibleSolidChunks` nonzero even after authored settlement columns are settled.

## Independent regression
`KentridgeStreamingCoveragePolicyTests` does not restate the production formula. It enumerates excluded lattice cells, computes point-to-square distance from a worst-case demand point, and verifies the production policy for radii 1 through 4. It separately asserts radius 3 yields 102.4m and is strictly smaller than the nominal 153.6m centre radius.

## Selected fix
Keep ShowcaseWorld streaming radius 3, the 29-column lattice, generation budgets, far-field streamed radius, device budgets, and the renderer's strict completeness predicate unchanged. In Kentridge scene composition only, set the renderer's near-surface ring to `KentridgeStreamingCoveragePolicy.GuaranteedNearSurfaceRadiusMetres(loadRadiusRegions, RegionMetres)` after the playable slice installs its normal rendering world.

This makes the promise to `HasCompletePublishedNearSurfaceCoverage()` match what residency can actually guarantee; the far field remains responsible outside that conservative near ring.

## Rejected alternatives
- Do not weaken or bypass `HasCompletePublishedNearSurfaceCoverage()`; that changes acceptance.
- Do not widen `m_LoadRadiusRegions`; that changes streaming cost/budgets rather than repairing the mismatched contract.
- Do not hardcode the observed 110.2m radius; it is position-dependent evidence, not a semantic invariant.
- Do not modify shared renderer publication semantics; the mismatch is introduced by Kentridge composition choosing an incompatible metric ring.
- Do not issue another identical CI retry; the feature now has a materially different root-cause-backed executable fix and regression.

## Next gate
Run the new exact feature SHA through `ci-test/fixes/agent-6`, targeting the independent radius regression with the same 180-second SceneIssue replay. Require repository-derived module validation, strict built-player coverage progression, later macro targets/captures, and no runtime exceptions before closing any remaining visual acceptance.
