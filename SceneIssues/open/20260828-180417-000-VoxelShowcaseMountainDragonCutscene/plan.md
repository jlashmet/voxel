# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain/readable winding ascent, normal grounded traversal from base to summit, a visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures plus a source-matched startup bake are required.

## Proven cause
Fresh-bake run `33285254695` cleared the bake-time blocker (~226 s) but exposed missing Gravel at first-turn voxel `(-435,264,-316)`. The voxel is authored correctly and is not lost by the Uniform full-block carve optimization. `ShowcaseWorld` suppresses generic catalogue features in castle-owned regions; castle ownership is `x=-1..1,z=-1..1`, while the old mountain footprint occupied `x=-3..-1,z=-1..1`, so its eastern column was intentionally discarded.

## Selected fix
`MountainDragonCastleRegionOwnershipTests` was added before production change and is part of the final acceptance entry point. The feature-local fix translates `ShowcaseMountainDragonLayout.OriginX` exactly one region west, `-1200 -> -1712`, moving footprint X regions to `-4..-2` without changing dimensions, primitive program, carve volume, route-relative geometry, or castle suppression semantics. Absolute replay X/look-X coordinates were shifted by the same 51.2 m; semantic tests already derive coordinates from `ShowcaseMountainDragonLayout`.

## Blast radius / cost
The footprint remains three X columns by three Z columns. Entry terrain height changes only `217 -> 218`, so authored base Y changes `218 -> 219` and summit Y `498 -> 499`, still inside the same `y=0` 512-voxel region layer. Startup radius remains eight regions; no new region layer, primitive, or carve volume is introduced. Keep the successful Uniform carve fast path unchanged.

## Current candidate
Production/evidence candidate: `dc45187bb68c15f9175b2fda232d846b747652ff`. Current master `d4b31a700bae19b08ef765e874e26026620bde0e` is already merged.

## Remaining gates
Update task metadata, re-check master immediately before the single final CI request, then use only `ci-test/fixes/agent-4`. Require fresh source-matched bake/manifest, green exact focused acceptance, complete grounded built-player route, and human-reviewed captures. Only then complete pending metadata, move `open -> pending -> closed`, set `status=fixed`/`resolvedUtc`, merge latest master, and push exact feature head to master non-force.
