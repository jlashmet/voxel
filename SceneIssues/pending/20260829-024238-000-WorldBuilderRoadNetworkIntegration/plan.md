# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote roads into a reusable WorldBuilder contract: semantic intent/provenance, deterministic terrain-aware resolution, one continuous influence for grading/surface/ecology, reusable keep-clearance, and bounded physical lowering. Kentridge is the proving consumer. Final proof is the built `KentridgePlayableSlice` application showing endpoint continuity, player-height walkability, natural shoulders/vegetation recovery, and no chunk/LOD/runtime failure.

## Result
The representation gap is resolved. `Game.WorldBuilder.Api` owns reusable road intent/profile/resolution/network data; Kentridge routes and macro connections author it. `TerrainCorridorRasteriser` lowers one bounded `EmitTerrainCorridor` primitive per piece and persists the same 0..31 road influence used by presentation/ecology consumers. Fractional shoulders keep local terrain primary and carry Dirt as secondary presentation material with continuous coverage in existing packed surface bits; `SmoothSurface` blends the complete material response while density/style paths keep blend metadata geometry-neutral.

CI-driven repairs covered coherent edge variation, seeded-building conflicts with preferred vegetation anchors, bounded long road names, and the packed-coverage clamp compile ambiguity. EditMode run `33284733815` passed 7/7 semantic/physical road regressions.

## Final discriminator
Exact request `ecdcafbb50810e5944858b2035b9e47d7bfb7c95`, based directly on feature source `ca9174082c2f3ecea78dfee0e503ff17fb6711a2`, produced green run `33296430341`: 2/2 PlayMode surface-blend regressions passed and the standalone Kentridge player ran 60.4 seconds with zero harness assertions/runtime exceptions. Human inspection accepted the evidence: t=39/49s exercises player traversal; t=49s shows the Dirt corridor with both grassy recoveries on uneven terrain and no checker/banded shoulder; t=59s fixed survey shows continuous town/outer roads across streamed terrain with the source-backed route overlay and no visible road break/LOD seam.

## Blast radius / cost
No persisted voxel-size or 32-byte `SmoothSurfaceVertex` stride growth, new road GameObjects/cover meshes/dense masks, or per-frame road generation. Definitions remain footprint/budget bounded with one analytic corridor and zero legacy road boxes. Final PlayMode peak RSS was 5591 MB with 0 MB swap growth. The survey reached 64 resident regions while preserving visible road continuity; later player samples remained above ~60 fps and commonly 100–160 fps. The evidence-profile adjustment affects only capture-less unattended Kentridge SceneIssue validation; recorded poses and normal gameplay are unchanged.

## Remaining work
Only promotion bookkeeping remains: close this validated pending assignment, reconcile `fixes/agent-1` with the latest `origin/master`, and push the exact feature head to `origin/master` non-force. If master advances, fetch/merge/retry before the final push.
