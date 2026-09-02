# Plan

## Acceptance
Built `VoxelShowcase` must show one substantial grounded natural mountain with a readable shared-road ascent, normal grounded traversal from accessible exterior terrain to a usable summit, a visibly supported allowed cube dragon, and exact proximity dialogue `Hello, I'm Mr. Dragon.` Final evidence must be exact-SHA standalone-player output, production-quality by `AGENTS.md`, source-matched to the checked-in startup bake, with unchanged 240 s / 14 GiB guards.

## Ownership
- `MountainLandformSpec` / `MountainLandformSurface`: deterministic semantic landform.
- `MountainClimateProfile`: semantic presentation independent of concrete material ids.
- `WorldRoadIntent` / `WorldRoadResolver` / `WorldRoadNetwork` / `EmitTerrainCorridor`: canonical routing and physical road realization.
- `ShowcaseMountainDragonLayout`: scene composition only (mountain/climate/spiral/road/placement/destination).
- Independent landform, climate, and road fixtures already prove reuse/determinism/bounds.

## Selected implementation
The rejected path/support-defined mountain was replaced by natural-landform-first composition. Showcase ridge strength is 300 permille after experiment 016 isolated secondary ridge crossings as the repeated 60/50 dm cut-fill cause; shared 280 permille grade and 42 dm cut-fill policy is unchanged. The authoritative 94-point production road drives evidence. Experiment 017 isolated the later replay stall: `resolved-49` was accepted 1.23 m early by the route-wide 1.25 m radius, causing the next steering chord to cross the switchback inside edge. Only that waypoint now uses the existing semantic `arrivalRadius: 0.35` fixture seam.

## Current merged-head result
`master` at `b1b69290...` was successfully merged into the feature branch (`2eb3b16d...`), clearing the former merge blocker. Exact run `33635226482` then failed before product validation for two independent causes: (1) repository module discovery double-owned `Game.WorldBuilder.Voxel` because both `Assets/Game/WorldBuilder` and nested `.../Voxel` are module roots; (2) standalone build was refused by the runner with 2804 MB free vs 4096 MB required. The ownership defect is now fixed by assigning runtime asmdefs to their nearest module root, with an independent nested-module regression. Disk space remains infrastructure and must be retried on the same transport after the product/tooling fix.

## Remaining gates
1. Exact current feature SHA via only `ci-test/fixes/agent-4`: planner regression, required module/player gates, standalone Mountain Dragon replay; inspect `WAYPOINT_REPLAY` logs/screenshots.
2. Check raster/build/bake blast radius and bump startup-bake provenance.
3. Human-review exact production approach, road integration, traversal, summit support, and dialogue.
4. Promote exact accepted `ShowcaseWorld.bytes` + manifest; record size/hash/signature and clean-checkout consumption.
5. Complete every `tasks.md`/`issue.json` criterion, close directly, merge latest master again, revalidate affected exact head as required, and non-force push that exact feature head to `master`.
