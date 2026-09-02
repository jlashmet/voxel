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
The rejected path/support-defined mountain was replaced by natural-landform-first composition. Showcase ridge strength is 300 permille after experiment 016 isolated secondary ridge crossings as the repeated 60/50 dm cut-fill cause; shared 280 permille grade and 42 dm cut-fill policy is unchanged. The authoritative production road drives evidence. Experiment 017 isolated early `resolved-49` acceptance and uses only its existing per-waypoint `arrivalRadius: 0.35`; experiment 018 then aligned `mid-turn` to authoritative resolved point 50. Because the same `resolved-49 -> mid-turn` stall persisted after both materially different fixture repairs, experiments 019/020 freeze traversal changes until built-player telemetry discriminates the physical failure mode.

## Current exact-source result
Exact run `33639437668` tested feature source `46c10a5505fb80709c3b7d294ed66ff8cea27f6b`. Runner preflight failed because an unrelated interactive Unity editor/AssetImportWorker under `/Users/jlashmet/code/voxel` remained alive, so automatic module validation was skipped. The standalone SceneIssue step still built the real player successfully (peak 8413 MB, 90 s), replayed the 95-waypoint route, reached grounded `resolved-49`, and again timed out before `mid-turn`. No experiment-019 `WAYPOINT_REPLAY diagnostic` samples appeared even though the diagnostic source was compiled into the player. Experiment 020 therefore isolates diagnostic activation itself: the observer is now attached explicitly from the already-active replay harness after the production motor/route are bound. No movement/world behavior changes.

## Remaining gates
1. Exact current feature SHA via only `ci-test/fixes/agent-4`: require `WAYPOINT_REPLAY diagnostic activated` + samples, planner regression/required module gates when runner preflight is clean, and standalone replay. Classify the repeated turn stall before any traversal repair.
2. Check raster/build/bake blast radius and bump startup-bake provenance.
3. Human-review exact production approach, road integration, traversal, summit support, and dialogue.
4. Promote exact accepted `ShowcaseWorld.bytes` + manifest; record size/hash/signature and clean-checkout consumption.
5. Complete every `tasks.md`/`issue.json` criterion, close directly, merge latest master again, revalidate affected exact head as required, and non-force push that exact feature head to `master`.
