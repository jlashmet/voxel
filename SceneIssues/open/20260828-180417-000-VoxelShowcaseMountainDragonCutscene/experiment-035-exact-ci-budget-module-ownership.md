# Experiment 035 — exact CI budget / module ownership

## Observation
Exact request `194af927f0e6f5234852499ddcc921e6d38ea481`, run `33985235532` attempt 2, selected feature source `1213743e568a04d6dc4c43e53e33b37198ce89f5`. The persistent Unity test phase completed with status 0 after 409 seconds. Real-player validations for Application, Audio, CaveWorldBuilder, Kentridge, MountainDragon, Showcase SecretDiscovery, CharacterMotor, Hud, Input, InventoryPresentation, ProgressionPresentation, Residency, SessionPresentation, Structures, Vfx, WorldBuilder, and FarWorld then completed. The GitHub job reached its 20-minute timeout while Water validation was running. The `always()` SceneIssue step subsequently found the cancelled Unity process still alive and `unity-run.sh` correctly refused a second editor; artifact ZIP creation also raced Water cleanup. This run is infrastructure/non-accepting, not a Mountain Dragon behavioral assertion failure.

## Discriminator
The automatic validation plan reported fallback paths under `Assets/Editor/CI`, `Assets/Game/Cutscenes/**`, and `Assets/VoxelEngine/Composition/**`. By design, any unowned production path selects every discovered module. The issue therefore had an ownership/planning defect that deterministically expanded validation beyond the job budget.

## Selected correction
- Treat `Assets/Editor/CI/**` as non-production CI infrastructure, with a planner regression that keeps other Editor runtime paths production-visible.
- Give `Assets/Game/Cutscenes` repository-owned EditMode tests plus a module-local standalone scene that invokes the production timed dialogue runtime and production dialogue overlay.
- Give `Assets/VoxelEngine/Composition` repository-owned EditMode coverage for startup-bake provenance. The changed Composition code is a headless data/contract layer; its player-visible consumer remains independently exercised by the Rendering/FarWorld module player and production VoxelShowcase integration.

The next exact run must report no fallback paths, finish repository-derived validation within the CI budget, and reach the feature SceneIssue replay. No workflow timeout or acceptance budget is weakened.
