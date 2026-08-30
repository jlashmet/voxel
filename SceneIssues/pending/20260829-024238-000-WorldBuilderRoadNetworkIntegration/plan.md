# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote roads into a reusable WorldBuilder contract: semantic intent/provenance, deterministic terrain-aware resolution, one continuous influence for grading/surface/ecology, reusable keep-clearance, and bounded physical lowering. Kentridge is the proving consumer. Final proof is the built `KentridgePlayableSlice` application showing endpoint continuity, player-height walkability, natural shoulders/vegetation recovery, and no chunk/LOD/runtime failure.

## Results / selected fix
The representation gap is resolved. `Game.WorldBuilder.Api` owns reusable road intent/profile/resolution/network data; Kentridge routes and macro connections author it. `TerrainCorridorRasteriser` lowers one bounded `EmitTerrainCorridor` primitive per piece and persists the same 0..31 road influence used by surface/ecology consumers.

CI exposed and repaired incoherent edge variation, seeded-building conflicts with preferred vegetation anchors, long `FixedString64Bytes` road names, and an overload ambiguity. Focused EditMode run `33284733815` passed 7/7.

Human inspection of green run `33286511375` rejected periodic Dirt↔Grass shoulder selection. The repair keeps local terrain primary through fractional shoulders and stores Dirt as secondary presentation material plus continuous coverage in existing packed surface bits. `SmoothSurface` blends complete material response. Density/style paths mask the blend marker so it cannot alter geometry; ordinary coating behavior remains unchanged. Two production-boundary PlayMode regressions cover fractional shoulder round-trip and zero blend-metadata displacement.

## Current discriminator
Run `33296050037` on repaired source passed both PlayMode blend regressions and the 60-second real player with zero harness assertions/exceptions. Its screenshots still fail the issue’s visual proof: startup/opening consumes most of the window and the default autowalk reaches a building wall, leaving no endpoint survey, two-shoulder slope view, seam view, or vegetation-recovery proof.

Hypothesis A: the remaining blocker is the generic capture-less Kentridge evidence profile. Faster opening, earlier player traversal, then a fixed late survey will expose the existing road correctly. Hypothesis B: improved framing will expose a remaining product defect. The smallest discriminator is to change only that unattended evidence profile; recorded-pose SceneIssues remain untouched.

## Blast radius / cost
Product representation remains bounded: no voxel-size or 32-byte `SmoothSurfaceVertex` growth, extra road GameObjects/meshes/dense masks, or per-frame generation. Blend geometry adds O(1) marker checks; marked fragments add one bounded secondary-material evaluation. Evidence-profile changes affect only command-line real-player validation.

## Remaining gates
Update the capture-less Kentridge profile to finish dialogue sooner, start traversal earlier, and switch to a fixed survey before the 60-second limit. Reuse only `ci-test/fixes/agent-1` for one exact-source combined PlayMode + scene request. Inspect full-resolution evidence for endpoint continuity, both shoulders on uneven/sloped ground, vegetation recovery, chunk/LOD seams, runtime exceptions, and runtime/residency cost. If framing reveals a product defect, fix it; otherwise complete metadata and open→pending→closed bookkeeping, merge current master, and push the exact feature head to master non-force.
