# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote Kentridge organic roads into a reusable WorldBuilder contract: semantic intent/provenance first, deterministic terrain-aware resolution, one continuous influence for voxel grading/surface transition/ecology, reusable keep-clearance, and a bounded physical representation. Kentridge is the proving consumer; the generic module must not depend on Kentridge. Final proof is the built `KentridgePlayableSlice` application with endpoint continuity, walkability, natural shoulders/vegetation recovery, and no chunk/LOD/runtime failure.

## Discrimination result
The representation gap is resolved. `Game.WorldBuilder.Api` owns reusable road profile/intent/resolution/influence/network data; Kentridge `SettlementPlan.Routes` and macro hard connections author it. Physical lowering is one bounded `EmitTerrainCorridor` primitive per piece. `TerrainCorridorRasteriser` grades destructible terrain and persists the same 0..31 road influence used by semantic/ecology consumers.

Earlier CI `33281599556` exposed incoherent per-sample edge hashes and immutable authored vegetation anchors colliding with seeded organic buildings; those were repaired with coherent 64dm edge variation and bounded deterministic anchor relocation. Focused EditMode run `33284733815` then passed all 7 road regressions. Combined run `33285741354` exposed and led to repair of the long macro-road `FixedString64Bytes` naming failure.

## Continuous surface-presentation remediation
Human inspection of green combined run `33286511375` rejected the Dirt→Grass shoulder because fractional road influence was converted into periodic binary material selection. The repair now keeps local terrain authoritative through fractional shoulders and carries road Dirt as secondary presentation material with the same 0..31 coverage.

The existing packed surface contract is reused: style bit `0x10` marks generic two-material presentation, low style bits retain reconstruction, the coating nibble carries the secondary material only in blend mode, and `Detail` carries continuous coverage. `SmoothSurface` interpolates coverage per vertex and blends full primary/secondary albedo, mapped normal, variation, and roughness. Shared CPU/GPU density paths and faceted/classification paths mask the marker for style lookup and treat marked secondary-material metadata as zero coating displacement. Unmarked coating/style/detail behavior remains unchanged.

Two PlayMode regressions sample an actual fractional corridor shoulder and prove packed coverage round-trip plus geometry-neutral blend metadata while ordinary Snow coating still displaces.

## Current discriminator
Final request `33294139897` on source `b87a8f00b21bc2064818f0f2ca3db3644c6e3975` was admitted normally but failed before tests/player build with `VoxelCell.cs(151,37) CS0121`. Commit `3c586c51b472f6c34461cfe939e8eca1051801a5` repaired only that overload ambiguity by explicitly selecting the integer `Math.Clamp` overload and casting back to byte; packed bits and runtime behavior are unchanged. The durable checklist now records that repair, so the next discriminator is the single combined exact-source PlayMode + built-player request.

## Blast radius / cost
No persisted voxel-size or `SmoothSurfaceVertex` stride growth; no new road primitives, GameObjects, cover meshes, dense world masks, or per-frame road generation. The marker reuses existing bits. CPU/GPU geometry adds O(1) marker checks; fragment cost adds one bounded second material evaluation only on marked blend surfaces.

## Remaining gates
Current master `61d03336390ed9079498b183217cbf0ecf0abcd2` is already an ancestor of `fixes/agent-1`. Reuse only `ci-test/fixes/agent-1` for the combined PlayMode regression + 60-second exact-scene player request on the final feature source. Inspect test/player logs and screenshots for endpoint continuity, both shoulders on uneven/sloped ground, vegetation recovery, chunk/LOD seams, runtime exceptions, and bounded cost. Only after all exact-SHA gates pass may metadata be completed and the assignment move open→pending→closed before the final current-master merge/push sequence.
