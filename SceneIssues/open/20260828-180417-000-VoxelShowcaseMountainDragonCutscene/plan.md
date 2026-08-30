# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain/readable winding ascent, normal grounded traversal from base to summit, a visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures plus a source-matched startup bake are required.

## Proven composition cause
Fresh-bake run `33285254695` cleared the earlier bake-time blocker (~226 s) but exposed missing Gravel at first-turn voxel `(-435,264,-316)`. The voxel is authored correctly and is not lost by the Uniform full-block carve optimization. `ShowcaseWorld` suppresses generic catalogue features in castle-owned regions; castle ownership is `x=-1..1,z=-1..1`, while the old mountain footprint occupied `x=-3..-1,z=-1..1`, so its eastern column was intentionally discarded.

## Selected composition fix
`MountainDragonCastleRegionOwnershipTests` was added before production change and is part of the final acceptance entry point. The feature-local fix translates `ShowcaseMountainDragonLayout.OriginX` exactly one region west, `-1200 -> -1712`, moving footprint X regions to `-4..-2` without changing dimensions, primitive program, carve volume, route-relative geometry, or castle suppression semantics. Absolute replay X/look-X coordinates were shifted by the same 51.2 m; semantic tests already derive coordinates from `ShowcaseMountainDragonLayout`.

## Corrected blast radius / cost
Geometric footprint remains 3x3 X/Z regions and the mountain remains entirely in the same `y=0` 512-voxel region layer: entry terrain height changes `217 -> 218`, authored base Y `218 -> 219`, summit Y `498 -> 499`. However the effective authoring cost is not unchanged: before translation, the three `x=-1` mountain regions were castle-owned and the generic mountain catalogue work there was suppressed; after translation all nine mountain footprint regions execute the landmark program. Effective mountain-region authoring therefore rises from 6 to 9 regions (+50%) even though primitive count, nominal footprint, and carve volume are unchanged. Exact request `517acdd1e7498f703b1a8355f2dc026261f33e97`, run `33287514117`, attempts 1 and 2 both hit the Unity subprocess watchdog at 241 s before focused acceptance; attempt 1 peaked at 10.5 GB RSS, below the 14 GB memory ceiling. The stale-player fallback failed only because the fresh source-matched manifest was never emitted.

## Next implementation gate
Do not weaken castle ownership, acceptance semantics, route proof, or the 240 s workflow contract. Locate the now-exercised hot path and add a focused behavioral/cost regression before any production optimization. The optimization must be output-equivalent for serialized voxel state and preserve write accounting/primitive order where observable. Re-check blast radius and measured cost after the regression and implementation.

## CI / closure
The completed red request may not be treated as acceptance and will not be replaced while queued/running. After revised source is committed and current `origin/master` is merged, reuse only the existing `ci-test/fixes/agent-4` transport for a new exact-parent final request. Require fresh source-matched bake/manifest, green exact focused acceptance, complete grounded built-player route, and human-reviewed captures. Only then complete pending metadata, move `open -> pending -> closed`, set `status=fixed`/`resolvedUtc`, merge latest master, and push exact feature head to master non-force.
