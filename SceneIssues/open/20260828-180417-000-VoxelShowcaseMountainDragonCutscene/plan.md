# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal to a usable summit, the explicitly allowed cube dragon visibly supported, and proximity dialogue `Hello, I'm Mr. Dragon.` WorldBuilder/shared modules own geometry and interaction. `AGENTS.md` requires player-visible work to be `production-quality`; only dragon art has the issue-specific placeholder allowance. Closure also requires a source-matched checked-in startup bake and exact built-player evidence.

## Design correction after visual review
The existing Mountain Dragon implementation has the ownership relationship backwards. `MountainLandmarkSpec` currently owns `PathRun`, `PathRise`, `SwitchbackCount`, path geometry, support masses, headroom carving, and path surface emission. The resulting built-player evidence reads as road terraces/support structures arranged into a mountain-shaped obstacle rather than a natural mountain. Human review explicitly rejected that result; further cosmetic support/terrace tuning would repeat the same architectural failure.

The corrected design is **natural landform first, road second**:

1. **Reusable mountain landform owns only mountain shape.** A parameterized WorldBuilder mountain description defines footprint/aspect, height, summit character, deterministic seed, macro-shape/ridge/asymmetry, and bounded roughness. It exposes a deterministic surface query and emits the same surface as voxel landform geometry. It contains no road, switchback, player, dragon, or Showcase policy.
2. **Reusable climate/presentation profile owns semantic surface treatment.** Altitude/slope bands select semantic rock/ground-cover/snow-like roles independently of mountain shape. Callers can build materially different climates without forking geometry. Material ids remain caller-owned.
3. **Existing road system owns ascent routing and terrain modification.** `WorldRoadProfile` already owns grade and cut/fill limits; `WorldRoadResolver` consumes `IWorldRoadTerrain`; `WorldRoadNetwork` is the shared resolved-route authority; `WorldRoadNetworkVoxelCatalogue` lowers that geometry through the generic terrain-corridor rasterizer. Mountain composition will provide its landform surface to the existing resolver, then lower the resolved road with the existing road catalogue. No mountain-specific ramp/support system will remain.
4. **Showcase owns only composition policy.** VoxelShowcase chooses one mountain shape/climate, one winding set of road control points, a road profile, placement, summit destination, and dragon/dialogue parameters. Traversal evidence/waypoints derive from the resolved road rather than duplicating route math.
5. **Reuse must be demonstrated independently.** A non-Showcase fixture will build at least two materially different parameter sets (for example a broad alpine massif and a narrower dry/asymmetric peak) and prove deterministic surface/voxel correspondence. Road integration will be exercised against the same generic surface contract.

This design keeps one owner per responsibility: mountain = landform, climate = material policy, road = route/cut/fill/traversal corridor, Showcase = scene composition.

## Existing road-system evidence
The current repository already has the required general road machinery:
- `Game.WorldBuilder.Api.WorldRoadProfile` defines carriageway/transition widths, maximum grade, and maximum cut/fill.
- `IWorldRoadTerrain` supplies terrain height/flags to `WorldRoadResolver`.
- `ResolvedWorldRoad` and `WorldRoadNetwork` are the shared 3D route/influence authority.
- `WorldRoadNetworkVoxelCatalogue` lowers resolved roads into generic `EmitTerrainCorridor` geometry with distinct surface and grading radii.
- `TerrainCorridorRasteriser` / `ContinuousTerrainCorridorRasteriser` own physical terrain-corridor realization.

Therefore this ticket should not create a parallel "mountain road" abstraction. Only a narrowly reusable terrain/surface adapter is acceptable if required to let the resolver see authored landforms instead of base `TerrainQuery` alone.

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

## Reconstruction / latest CI history
The feature was safely reconstructed on current master rather than reusing the stale divergent branch. Run `33447340071` then failed compilation because revision-8/9 mountain topology dependencies and a direct Cutscenes API reference were omitted from that reconstruction; source commit `a109bf20b90adb1c45af6c02ee95c43d8477454f` restored those proven dependencies.

The first retry transport did not trigger because `.github/test-request.json` was byte-identical to the previous transport tip and the workflow correctly has a path filter. A uniquely identified request triggered run `33449145780`. That run compiled and its standalone SceneIssue replay succeeded, but the requested focused filter matched zero tests (`testcasecount=0`), so the run completed failure and is not acceptance evidence.

No agent-4 CI is currently queued or running. `origin/master` was last checked at `c73ab9d123ad29a1f1f1215552519a303c16d5fe` and is already an ancestor of the reconstructed feature. The obsolete PR #182/#192 merge-conflict blocker no longer applies.

## Implementation order
1. Replace the path-coupled `MountainLandmarkSpec` with a reusable parameterized mountain landform/surface contract. The same deterministic shape function must drive both surface queries and emitted landform geometry.
2. Add reusable climate/presentation configuration without embedding material ids or scene coordinates in generic shape code.
3. Build a generic adapter/composer that resolves an existing `WorldRoadIntent` against a mountain surface (and base terrain outside the landform footprint where necessary), then lowers the resulting `WorldRoadNetwork` through the existing road voxel catalogue. Generalize the road terrain-input seam only if the current API demonstrably cannot express this.
4. Recompose Mountain Dragon: natural mountain + existing road + summit/dragon placeholder + existing proximity/cutscene modules. Remove old switchback supports/headroom/path emission from mountain ownership.
5. Make exact traversal evidence consume the resolved road geometry; keep player motor policy in the already-proven public replay seam.
6. Add independent reuse regressions for different shapes/climates plus road cut/fill/grade behavior, deterministic surface/voxel correspondence, summit support, blast radius, and bounded primitive/build cost.
7. Use only `ci-test/fixes/agent-4` for exact-source gates. Never replace a queued/running run.
8. Human-review exact production `VoxelShowcase` approach/base/switchback/upper/summit/dialogue captures. The mountain must first read as one coherent natural landmass; the road must read as a route carved into/across it. Automated green is insufficient.
9. From the final visually accepted run, record exact bake/runtime cost and payload provenance; promote the exact `ShowcaseWorld.bytes` + matching manifest and validate clean-checkout consumption.
10. Only after every acceptance criterion/checklist item is green: update issue metadata, move only this issue `open -> closed`, merge then-current `origin/master`, revalidate exact final head as required, and non-force push that exact feature head to `origin/master`.

## Non-goals / boundaries
- Do not preserve old switchback/support APIs merely for compatibility if their only consumer is this flawed Mountain Dragon composition; remove or obsolete them only where the feature diff proves they are assignment-owned.
- Do not create a second road renderer/resolver/carver.
- Do not refactor Kentridge or unrelated road presentation policy.
- Do not weaken the 240 s / 14 GiB bake guard, feature budgets, visual bar, or original acceptance.
