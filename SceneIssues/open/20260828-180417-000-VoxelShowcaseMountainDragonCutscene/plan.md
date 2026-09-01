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
5. **Reuse is demonstrated outside Showcase.** `MountainLandformTests` prove deterministic same-spec output, materially different shape families, and exact mass-to-voxel-catalogue correspondence. `MountainClimateReuseTests` proves one landform with independent climates plus materially different shape/climate combinations. `MountainRoadIntegrationTests` is the independent generic mountain/fallback terrain/road consumer and checks legal bounded ascent, rejection of an over-constrained ascent, and generic `EmitTerrainCorridor` lowering with no `EmitRamp` fallback.

This keeps one owner per responsibility: mountain = landform, climate = material policy, road = route/cut/fill/traversal corridor, Showcase = scene composition.

## Current implementation state
Production composition no longer calls the legacy `WorldBuilderMountainLandmarkCatalogue`; the old path tiers, ramp/support masses and headroom carving are therefore removed from the Mountain Dragon production path. A narrow reusable `MountainLandformRoadTerrain` composes the natural mountain surface with normal terrain outside the authored footprint. A WorldBuilder-facing `WorldBuilderRoadVoxelCatalogue` adapter hides backend voxel settings while delegating all physical road realization to the existing generic road catalogue. The red cube marker is separated into a summit-placeholder catalogue so dragon art policy does not leak into landform or road ownership.

The focused Mountain Dragon validation support stages and inspects the same natural mountain surface and resolved production road geometry rather than legacy tier internals. Module-validation metadata now follows the redesigned production files and retains the exact focused PlayMode filter.

Run `33468581318` exposed two real source defects after compilation reached the redesigned path: the independent road fixture asserted implementation sampling density (`>8` points) even though the generic resolver legally simplified the route to two endpoints, and the production Mountain Dragon spiral failed its own 42 dm cut/fill contract (`60dm` required at point 33). The fixture now asserts semantic grade/cut-fill bounds instead of point density. Showcase keeps the same 1.5-turn ascent and unchanged 280 permille grade / 42 dm cut-fill contracts but uses 25 half-step semantic controls instead of 13 coarse 45-degree controls so the shared resolver follows the ridged natural contour rather than cutting across it.

The secondary built-player `LandmarkWorldPosition` nulls in that run were downstream of `VoxelShowcase.OnEnable` failing during the unresolved ascent; no harness/null masking change was made.

The checked-in SceneIssue evidence route is still legacy switchback data with hard-coded old Y offsets. It cannot count as final traversal/visual evidence. Once the redesigned road resolves on the exact final source, regenerate that route from the production resolved road and retain the required approach/base/lower/mid/upper/summit/dialogue captures.

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

## Latest CI history / blocker
Run `33464154384` built the real player successfully from source `2ccc2751...`; its EditMode invocation then hit the CI runner's 240-second watchdog, and replay evidence captured only one frame where that old request expected two. This is infrastructure/evidence-harness failure, not a compiler failure.

Run `33465874998` was invalid road evidence because the CI transport had accidentally been parented to older CI request commit `00e9aab...`; the requested test did not exist in that source and matched zero tests.

Correctly source-parented runs then exposed and drove narrow fixes:
- `33468298272`: `ShowcaseCatalogue` had ambiguous API/runtime `FeatureCatalogueComposer`; fixed by using the Structures API boundary only.
- `33468432862`: legacy `MountainDragonProductionAcceptanceTests` still called removed `CreateLandmark`; migrated to the current landform/road/placeholder production contract.
- `33468581318`: independent road suite executed 2/3 before the point-density assertion failed; standalone player exposed the production 60 dm vs 42 dm cut/fill failure described above.

Exact-source road retry `33469216133` is queued from CI commit `8656913e...`, whose parent is feature source `c2bfa596...`. Repeated checks show `runner_id=0` and no repository workflow in progress; the self-hosted macOS runner is currently unavailable. Per assignment rules this queued request must not be replaced. This is the current external prerequisite blocker for execution evidence.

## Remaining implementation/validation order
1. Preserve queued run `33469216133`; when it completes, diagnose only its completed result. Do not replace it while queued/running.
2. If the redesigned ascent resolves, run exact-head module validation so the existing deterministic/shape/climate/correspondence tests and independent road tests become current evidence. If the same production cut/fill symptom still fails after this materially different contour-control fix, isolate a minimal production-route repro/root cause before another fix.
3. Regenerate the SceneIssue evidence route from the final resolved production road; remove legacy switchback/Y assumptions and preserve normal grounded traversal/capture requirements.
4. Check primitive/raster/build cost and startup-bake provenance; keep global budgets and 240 s / 14 GiB guards unchanged.
5. Merge then-current `origin/master` before the final visual request as required by this issue, resolve only assignment-relevant conflicts, and establish the final exact source SHA.
6. Run exact-source production `VoxelShowcase` built-player evidence. Human-review approach, road entrance, representative lower/mid/upper ascent, summit support, grounded traversal and exact dialogue. Automated green is insufficient.
7. From the final visually accepted run, record exact bake/runtime cost and payload provenance; promote the exact `ShowcaseWorld.bytes` + matching manifest and validate clean-checkout consumption.
8. Only after every acceptance criterion/checklist item is green: update issue metadata, move only this issue `open -> closed`, merge any newly advanced `origin/master`, revalidate exact final head as required, and non-force push that exact feature head to `origin/master`.

## Non-goals / boundaries
- Do not create a second road renderer/resolver/carver.
- Do not refactor Kentridge or unrelated road presentation policy.
- Do not reintroduce the legacy mountain-owned ramp/support design merely to preserve its APIs.
- Do not weaken the 240 s / 14 GiB bake guard, feature budgets, visual bar, original acceptance, or exact built-player evidence requirements.
