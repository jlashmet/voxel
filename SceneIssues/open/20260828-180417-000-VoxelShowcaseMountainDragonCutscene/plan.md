# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal to a usable summit, the explicitly allowed cube dragon visibly supported, and proximity dialogue `Hello, I'm Mr. Dragon.` WorldBuilder/shared modules own geometry and interaction. `AGENTS.md` requires player-visible work to be `production-quality`; only dragon art has the issue-specific placeholder allowance. Closure also requires a source-matched checked-in startup bake and exact built-player evidence.

## Design correction after visual review
The existing Mountain Dragon implementation had the ownership relationship backwards. `MountainLandmarkSpec` owned path/switchback geometry, supports and headroom carving, so the rendered result read as road terraces/support structures arranged into a mountain-shaped obstacle rather than a natural mountain. Human review rejected that result; further cosmetic support/terrace tuning would repeat the same architectural failure.

The corrected design is **natural landform first, road second**:

1. **Reusable mountain landform owns only mountain shape.** A parameterized WorldBuilder mountain description defines footprint/aspect, height, summit character, deterministic seed, macro-shape/ridge/asymmetry, and bounded roughness. `MountainLandformSurface` is the common physical authority for queries and voxel realization.
2. **Reusable climate/presentation profile owns semantic surface treatment.** Altitude/slope bands select semantic rock/ground-cover/snow-like roles independently of mountain shape; concrete material ids remain caller-owned. Independent climate reuse passed run `33462667493`.
3. **Existing road system owns ascent routing and terrain modification.** `WorldRoadProfile` owns grade/cut/fill, `WorldRoadResolver` consumes `IWorldRoadTerrain`, `WorldRoadNetwork` is the shared route authority, and `WorldRoadNetworkVoxelCatalogue` lowers through generic `EmitTerrainCorridor`. Mountain Dragon now uses those owners; it has no private road resolver/ramp system.
4. **Showcase owns only composition policy.** `ShowcaseMountainDragonLayout` chooses the natural mountain parameters, climate, spiral control intent, road profile, placement and destination. `ShowcaseCatalogue` composes landform + shared road lowering + independent summit placeholder. Encounter proximity derives from the final resolved road point.
5. **Reuse is demonstrated outside Showcase.** `MountainClimateReuseTests` already proves presentation-policy reuse. `MountainRoadIntegrationTests` adds a generic non-Showcase mountain/fallback terrain/road fixture and asserts successful bounded ascent, rejection of an over-grade case, and shared terrain-corridor lowering with no `EmitRamp` fallback.

This keeps one owner per responsibility: mountain = landform, climate = material policy, road = route/cut/fill/traversal corridor, Showcase = scene composition.

## Current implementation state
Production composition no longer calls the legacy `WorldBuilderMountainLandmarkCatalogue`; the old path tiers, ramp/support masses and headroom carving are therefore removed from the Mountain Dragon production path. A narrow reusable `MountainLandformRoadTerrain` composes the natural mountain surface with normal terrain outside the authored footprint. A WorldBuilder-facing `WorldBuilderRoadVoxelCatalogue` adapter hides backend voxel settings while delegating all physical road realization to the existing generic road catalogue. The red cube marker is separated into a summit-placeholder catalogue so dragon art policy does not leak into landform or road ownership.

The existing focused Mountain Dragon validation support has been rewritten to stage and inspect the same natural mountain surface and resolved production road geometry rather than legacy tier internals. It checks route grade, mountain cut/fill correspondence, material climb, and summit approach before staging the focused visual scene.

## Existing road-system evidence
The current repository already has the required general road machinery:
- `Game.WorldBuilder.Api.WorldRoadProfile` defines carriageway/transition widths, maximum grade, and maximum cut/fill.
- `IWorldRoadTerrain` supplies terrain height/flags to `WorldRoadResolver`.
- `ResolvedWorldRoad` and `WorldRoadNetwork` are the shared 3D route/influence authority.
- `WorldRoadNetworkVoxelCatalogue` lowers resolved roads into generic `EmitTerrainCorridor` geometry with distinct surface and grading radii.
- `TerrainCorridorRasteriser` / `ContinuousTerrainCorridorRasteriser` own physical terrain-corridor realization.

Therefore this ticket does not create a parallel mountain-road abstraction. `MountainLandformRoadTerrain` is only the narrowly reusable terrain composition seam required to let the resolver see the authored landform plus base terrain outside it.

## Prior evidence retained
Earlier revisions remain useful regression evidence but are not the target design:
- `33310677691`: structural acceptance and 17/17 normal-movement replay under unchanged 240 s / 14 GiB guards.
- `33318216711`: revision-6 functional/cost green; human visual rejection as prototype/blockout quality.
- `experiment-010-switchback-core-gap-minimal-repro.md`: isolated the old path/core coupling failure before revision-8/9.
- `33359276877`: old centered-headroom/support regression green.
- `33363384438`: independent traversal-profile fixture green.
- `33391220613`: public deterministic waypoint-replay seam green.
- `33357975697`: generic raster fast-path reuse green.
- `33406812093`: focused validation-scene shader hygiene green; not production visual acceptance.
- `33371715298`: binary handoff path proven at 15,105,067 bytes, SHA-256 `bd3f3e666da4d2ec687313ad1a08992a88bbf87430f2ffde96240774ab5ae62c`, signature `0xA799B5B8`, bake 159.963 s; not visually accepted.
- `33462667493`: independent `MountainClimateReuseTests` green for source `4b4abc3e...`.

## Latest CI history
Run `33464154384` built the real player successfully from source `2ccc2751...`; its EditMode invocation then hit the CI runner's 240-second watchdog, and replay evidence captured only one frame where that old request expected two. This is infrastructure/evidence-harness failure, not a compiler failure, so production code was not changed to accommodate it.

Current reusable road regression source was `df5749e5...` when request `33465874998` was queued through the sole authorized `ci-test/fixes/agent-4` transport. That run must complete before the CI request is changed again. Subsequent checklist/plan documentation commits do not change production behavior; final acceptance will still require exact final-head gates after all source/evidence work and the required master merge.

## Remaining implementation/validation order
1. Complete the independent road regression gate and fix only demonstrated product/test defects; retry only proven infrastructure failure through the same transport.
2. Finish independent two-shape/two-climate and deterministic/surface-correspondence evidence if existing landform tests do not already prove every checklist clause.
3. Check primitive/raster/build cost and startup-bake provenance; keep global budgets and 240 s / 14 GiB guards unchanged.
4. Ensure the intended focused Mountain Dragon test filter is discoverable, then run the exact focused validation scene through the same CI transport.
5. Merge then-current `origin/master` before the final visual request as required by this issue, resolve only assignment-relevant conflicts, and establish the final exact source SHA.
6. Run exact-source production `VoxelShowcase` built-player evidence. Human-review approach, road entrance, representative lower/mid/upper ascent, summit support, grounded traversal and exact dialogue. Automated green is insufficient.
7. From the final visually accepted run, record exact bake/runtime cost and payload provenance; promote the exact `ShowcaseWorld.bytes` + matching manifest and validate clean-checkout consumption.
8. Only after every acceptance criterion/checklist item is green: update issue metadata, move only this issue `open -> closed`, merge any newly advanced `origin/master`, revalidate exact final head as required, and non-force push that exact feature head to `origin/master`.

## Non-goals / boundaries
- Do not create a second road renderer/resolver/carver.
- Do not refactor Kentridge or unrelated road presentation policy.
- Do not reintroduce the legacy mountain-owned ramp/support design merely to preserve its APIs.
- Do not weaken the 240 s / 14 GiB bake guard, feature budgets, visual bar, original acceptance, or exact built-player evidence requirements.
