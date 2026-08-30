# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote Kentridge organic roads into a reusable WorldBuilder contract: semantic intent/provenance first, deterministic terrain-aware resolution, one continuous influence for voxel grading/surface transition/ecology, reusable keep-clearance, and a bounded physical representation. Kentridge is the proving consumer; the generic module must not depend on Kentridge. Final proof is the built `KentridgePlayableSlice` application with endpoint continuity, walkability, natural shoulders/vegetation recovery, and no chunk/LOD/runtime failure.

## Discrimination result
The representation gap is resolved. `Game.WorldBuilder.Api` owns reusable road profile/intent/resolution/influence/network data; Kentridge `SettlementPlan.Routes` and macro hard connections author it. Physical lowering is one bounded `EmitTerrainCorridor` primitive per piece, not historical carve/core/ten-strip shoulders. `TerrainCorridorRasteriser` grades destructible terrain and persists the same 0..31 road influence used by semantic/ecology consumers.

Earlier CI `33281599556` exposed incoherent per-sample edge hashes and immutable authored vegetation anchors colliding with seeded organic buildings; those were repaired with coherent 64dm edge variation and bounded deterministic anchor relocation. Focused EditMode run `33284733815` then passed all 7 road regressions. Combined run `33285741354` exposed and led to repair of the long macro-road `FixedString64Bytes` naming failure.

## Continuous surface-presentation remediation
Human inspection of green combined run `33286511375` rejected the Dirt→Grass shoulder because fractional road influence was converted into periodic binary material selection. The repair now keeps local terrain authoritative through fractional shoulders and carries road Dirt as secondary presentation material with the same 0..31 coverage.

The existing packed surface contract is reused: style bit `0x10` marks generic two-material presentation, low style bits retain reconstruction, the coating nibble carries the secondary material only in blend mode, and `Detail` carries continuous coverage. `SmoothSurface` interpolates coverage per vertex and blends full primary/secondary albedo, mapped normal, variation, and roughness. Shared CPU/GPU density paths and faceted/classification paths mask the marker for style lookup and treat marked secondary-material metadata as zero coating displacement. Unmarked coating/style/detail behavior remains unchanged.

Two PlayMode regressions are implemented: one samples an actual fractional `TerrainCorridorRasteriser` shoulder and round-trips its coverage through packed storage without vertex-stride growth; the other invokes the production CPU density displacement path and proves a blend using the Snow byte is geometry-neutral while ordinary Snow still displaces.

## Blast radius / cost
No persisted voxel-size or `SmoothSurfaceVertex` stride growth; no new road primitives, GameObjects, cover meshes, dense world masks, or per-frame road generation. The marker reuses existing bits. CPU/GPU geometry adds O(1) marker checks; fragment cost adds one bounded second material evaluation only on marked blend surfaces. Existing `FeatureBudget` caps and one-corridor-per-piece representation remain intact.

## Current source / remaining gates
Current master `61d03336390ed9079498b183217cbf0ecf0abcd2` was conflict-free against agent-1 changes and is merged as the second parent of `7eaad4b037596ac39ac3b9eac64c4dfefae34b57`. After this plan update, use the resulting exact feature SHA for the single combined PlayMode surface-blend regression + 60-second exact-scene player request via `ci-test/fixes/agent-1`. Inspect test logs, player log, screenshots, endpoint continuity, both shoulders on uneven/sloped ground, vegetation recovery, chunk/LOD seams, and runtime/residency cost. Only after all exact-SHA gates pass may metadata be completed, the assignment move open→pending→closed, current master be merged again, and the exact feature head be pushed non-force to master.