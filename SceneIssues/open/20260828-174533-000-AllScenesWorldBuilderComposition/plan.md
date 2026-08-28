# Plan: WorldBuilder-only scene composition

## Evidence / acceptance

- This architecture capture has no screenshots, frames, poses, or annotations, so there are no visual marked regions to replay; the captured evidence surface is production scene/bootstrap ownership.
- Audited build/showcase/lookdev paths found direct backend composition in `KentridgePlayableSlice`, `VoxelShowcase`, `WorldbuildingGalleryShowcase`, `ArchLookdev`, and `TerrainLookdev`, plus scene-owned helper generation under Showcase/Kentridge.
- The initial GUID-preserving source relocation proved useful but insufficient by itself: after the move, Kentridge still called Hightown's backend planner directly and Showcase still accepted concrete content modes. That falsifies the "location-only" hypothesis.

## Competing hypotheses

1. **Scene bootstraps still own world realization despite reusable generators. Supported.** Direct storage creation, catalogue combination, vegetation/life realization, and procedural environment authoring establish the boundary leak.
2. **The defect is only source location/naming. Rejected by post-move inspection.** The same direct generator calls remained after pure renames.
3. **All large scene scripts are violations. Rejected.** Camera/lighting/UI/input/animation/metrics are presentation; the discriminator is creation/mutation of generated world content or selection of backend generation algorithms.

## Fix / regression

- Add `WorldEnvironmentSpec`/recipes to `Game.WorldBuilder.Api`; concrete Showcase modes are resolved only after semantic WorldBuilder intent is established.
- Route both Kentridge and Hightown town authoring through `WorldBuilderTownAuthoring`, preserving distinct deterministic plans rather than collapsing to one canonical world.
- Preserve serialized scene compatibility by keeping original script GUIDs/assembly identities while moving environment-producing runtime/editor source under reusable Game composition ownership.
- Behavioral regressions execute production WorldBuilder authoring for both towns, assert distinct realized plans, and exercise semantic Showcase recipe resolution. Source scanning is supplemental only.
- Final targeted validation is the PlayMode filter under `KentridgePlayableScenePlayTests.*`; the single-test workflow automatically follows it with the repository real-player harness, which builds and launches `KentridgePlayableSlice` as the built-app gate. No synthetic scene-issue pose is invented because this capture has none.

## Blast radius / cost

- Existing seeds, feature subsets, scene YAML GUIDs, assembly names, storage budgets, catalogue outputs, and presentation settings remain compatible.
- WorldBuilder mapping is O(requested semantic features); it delegates to existing bounded generators and adds no per-frame generation loop.
- Hightown gains the same opaque WorldBuilder authoring boundary as Kentridge; backend `SettlementPlan` remains internal/friend-visible for existing realization adapters.
- Before the single final CI request, refresh current master again, verify only assigned capture/shared API/composition/tests changed, and use the exact feature candidate SHA.
