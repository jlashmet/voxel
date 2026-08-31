# Experiment 010 - Switchback/core gap minimal reproduction

## Why this reproduction exists
Revision 4 remained visually blockout-quality, revision 5 failed the unchanged bake watchdog, and revision 6 passed functional/cost CI but still failed mandatory human visual review. `AGENTS.md` requires isolating the failure before another production-code change after three genuine failed attempts.

This reproduction uses only the authored `MountainLandmarkSpec.RampLocalZ(startY)` formula and VoxelShowcase dimensions. It does not change production code.

## Authored formula
`RampLocalZ(startY)` computes:

```text
radiusAtHeight = MountainRadius
               - (MountainRadius - SummitRadius) * startY / MountainHeight
rampZ = CentreLocal - radiusAtHeight - PathWidth - 10
```

VoxelShowcase inputs:
- `CentreLocal = 600`
- `MountainRadius = 500`
- `SummitRadius = 80`
- `PathWidth = 30`
- `PathRise = 46`
- `MountainHeight = 280`
- `SwitchbackCount = 6`

For the near face, define:
- `pathInner = rampZ + PathWidth`
- `coreMin(y) = CentreLocal - radiusAtHeight(y)`
- `gap = coreMin - pathInner`

A positive gap means even the inner edge of the authored path lane is outside the tapered core.

## Reproduction

| level | startY | endY | radius start | radius end | rampZ | path inner | core min start | core min end | gap start | gap end |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 0 | 46 | 500 | 431 | 60 | 90 | 100 | 169 | 10 | 79 |
| 1 | 46 | 92 | 431 | 362 | 129 | 159 | 169 | 238 | 10 | 79 |
| 2 | 92 | 138 | 362 | 293 | 198 | 228 | 238 | 307 | 10 | 79 |
| 3 | 138 | 184 | 293 | 224 | 267 | 297 | 307 | 376 | 10 | 79 |
| 4 | 184 | 230 | 224 | 155 | 336 | 366 | 376 | 445 | 10 | 79 |
| 5 | 230 | 276 | 155 | 86 | 405 | 435 | 445 | 514 | 10 | 79 |

## Result
Every horizontal switchback begins with its inner edge **10 voxels outside** the coherent core and, because the core tapers while the tier stays at one fixed Z, ends **79 voxels outside** it. The path lane therefore cannot be realized as a carved/integrated mountain path without substantial freestanding support geometry.

There is a second constraint: the authored X run remains a constant 360 voxels while the core radius shrinks to 86 voxels near the top. Even moving only the Z coordinate inward cannot fit the complete upper switchback inside that tapered core. The route footprint itself must narrow with elevation or the core must become unnaturally broad.

## Hypothesis
The repeated exposed berms, retaining walls and causeway terraces are consequences of route/core topology, not primarily support-material styling. A production-quality reusable realization should taper the switchback run with elevation and place each tier inside/into the coherent mountain mass, leaving only modest natural embankment support where necessary.

## Falsifier
This hypothesis is false if a topology-preserving calculation can keep the complete authored walking lane inside the tapered core across each tier while retaining the constant 360-voxel upper run, or if an integrated tapered-route realization still requires the same exposed support volumes and produces the same reviewed silhouette.

## Constraint for the next implementation
Do not iterate another cosmetic support-pair shape. Re-author the reusable route/core relationship, share the same geometry helpers with route evidence/waypoints, preserve normal grounded traversal/headroom and the winding-ascent acceptance, and add a regression that proves upper tiers narrow/integrate with the mountain while support-cost proxy does not increase.
