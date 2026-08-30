# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote Kentridge organic roads into a reusable WorldBuilder contract: semantic intent/provenance first, deterministic terrain-aware resolution, one continuous influence for voxel grading/surface transition/ecology, reusable keep-clearance, and a bounded physical representation. Kentridge is the proving consumer; the generic module must not depend on Kentridge. Final proof is the built `KentridgePlayableSlice` application with endpoint continuity, walkability, natural shoulders/vegetation recovery, and no chunk/LOD/runtime failure.

## Discrimination result
The representation gap is resolved. `Game.WorldBuilder.Api` owns reusable road profile/intent/resolution/influence/network data; Kentridge `SettlementPlan.Routes` and macro hard connections author it. Physical lowering is one bounded `EmitTerrainCorridor` primitive per piece, not historical carve/core/ten-strip shoulders. `TerrainCorridorRasteriser` grades destructible terrain and persists the same 0..31 road influence used by semantic/ecology consumers.

Earlier CI `33281599556` exposed incoherent per-sample edge hashes and immutable authored vegetation anchors colliding with seeded organic buildings; those were repaired with coherent 64dm edge variation and bounded deterministic anchor relocation. Focused EditMode run `33284733815` then passed all 7 road regressions.

## Full-player discriminator
Run `33285741354` proved the sanctioned combined transport works: the real `KentridgePlayableSlice` player built, ran 60 seconds, captured four screenshots, and exited 0, while its PlayMode half exposed an in-scope startup-path `FixedString64Bytes` truncation for `world-road-macro:overworld-moordell->overworld-to-rossdam-s0p0`. The helper now budgets `FixedString64Bytes.UTF8MaxLengthInBytes` and preserves the segment/piece suffix; a production-catalogue PlayMode regression covers the observed ID.

## Terrain flags
The generic resolver supports Blocked/Water/Reserved/Pass flags and crossing policy. Current production terrain/Kentridge/top-down route sources provide no authoritative water/reserved/barrier map, so adapters return `None` rather than fabricate flags; deterministic fixtures prove the generic policy.

## Continuous surface-presentation remediation
Human inspection of green combined run `33286511375` rejected the Dirt→Grass shoulder because `TerrainCorridorRasteriser` hashes fractional road influence into a periodic binary material choice. The selected repair keeps the existing 0..31 authoritative influence and existing storage/vertex widths: reserve style bit `0x10` as a generic two-material presentation marker, keep the low style bits as the normal reconstruction style, use the existing coating byte/nibble as the secondary material only while the marker is set, and keep `Detail` as continuous secondary-material coverage. Unmarked surfaces preserve existing coating/style/detail semantics.

`SmoothSurface` will decode the marker, interpolate coverage per vertex, evaluate primary and secondary material responses, and blend them continuously. Every reconstruction/density consumer must mask the marker before style lookup and treat marked secondary-material metadata as zero coating displacement/decoration. Road shoulders author local terrain as primary, road Dirt as secondary, and road influence as Dirt coverage; full core samples remain direct Dirt.

## Blast radius / cost
No persisted voxel-size or `SmoothSurfaceVertex` stride growth; no new road primitives, GameObjects, cover meshes, dense world masks, or per-frame road generation. The shared marker consumes an otherwise-unused style bit and reuses existing packed channels. CPU/GPU density work remains unchanged except O(1) marker checks; fragment cost adds a second material response only for marked blend surfaces. Existing `FeatureBudget` caps and one-corridor-per-piece representation remain intact.

## Remaining gates
Implement marker-aware storage/rendering/density paths and the two production-boundary regressions. Refresh/merge current master before the final request. Then issue the single combined PlayMode road-regression + 60-second exact-scene player request via `ci-test/fixes/agent-1`; inspect test logs, player log, screenshots, endpoint continuity, shoulders/vegetation/LOD, and dynamic cost. Only after all exact-SHA gates pass may metadata be completed, the assignment move open→pending→closed, current master be merged again, and the exact feature head be pushed non-force to master.