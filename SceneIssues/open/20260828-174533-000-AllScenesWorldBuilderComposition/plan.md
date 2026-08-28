# Plan: WorldBuilder-only scene composition

## Evidence / hypotheses

- Architecture capture has no screenshots, frames, poses, or annotations; there are no visual marked regions to replay. Evidence is production scene/bootstrap ownership plus runtime CI.
- Audit covered every production/showcase `.unity` scene and the former `Assets/Scenes/Kentridge` / `Assets/Scenes/Showcase` bootstrap source. Generation bypasses included direct storage/catalogue, terrain, structure, vegetation/life, far-terrain and procedural-mesh construction; presentation-only camera/lighting/UI/input/metrics were separated by the discriminator “creates or mutates generated gameplay environment content.”
- Hypothesis 1 — scene bootstraps own generated-world realization despite reusable generators — supported. Hypothesis 2 — source location/naming alone is the defect — rejected. Hypothesis 3 — every large scene script is a violation — rejected by presentation-only counterexamples.
- Selected boundary: scenes may choose seeds, subsets, placement intent and presentation, but generated environment intent enters through WorldBuilder semantic recipes/town authoring; reusable Game/Voxel composition owns concrete storage/catalogue/generator choices.

## Fix / regression

- Added `WorldEnvironmentSpec` recipes and shared Showcase resolution while preserving distinct detailed-structure, fortified-landmark and gallery compositions; unsupported semantic combinations fail instead of silently selecting the wrong backend.
- Routed Kentridge and Hightown town authoring through `WorldBuilderTownAuthoring`; preserved serialized script GUIDs/assembly identity while moving scene-owned generation implementations under reusable Game composition ownership.
- EditMode regression executes production town recipes, proves Kentridge/Hightown remain distinct, verifies organic Kentridge traversal, exercises semantic Showcase plans, and guards `Assets/Scenes` from concrete generation backends.
- Run `33201049016` exposed route-blind Kentridge traversal: modern Kentridge has inferred routes and zero legacy streets. `SettlementStreetTraversalFacts` now graphs routes/intersections while preserving legacy streets; regression requires pub-to-site reachability without restoring streets.
- Run `33202598302` built/launched Kentridge and passed generated-world reachability, then failed only an unrelated opening-camera presentation assertion; camera choreography was not changed.
- Focused regression `VoxelEngine.Tests.PlayMode.WorldBuilderProductionScenePlayTests.KentridgePlayableScene_PublishesWorldBuilderEnvironmentWithoutPresentationCoupling` loads the real build-settings scene and verifies published near-surface coverage, Kentridge/Hightown plans, corridor/theme/life composition, and resident pub access regions.
- Run `33206424685` proved that focused regression green, but its player step explicitly skipped because the new test class lacked a real-player profile. Discriminator: CI test correctness was green; harness routing was incomplete. The existing Kentridge player-capture profile now also recognizes `WorldBuilderProductionScenePlayTests`, forcing the same real macOS player build/launch instead of skip.

## Blast radius / cost / final gate

- Seeds, scene YAML, script GUIDs, authored content choices and backend costs are preserved. Semantic planning is O(feature count); traversal adds bounded pairwise exact-intersection preprocessing over short settlement paths and no per-frame work. Harness change only expands test-filter routing to the existing Kentridge player profile.
- No other capture is modified and `.github/test-request.json` remains absent from feature-branch changes.
- Current master is merged. Create one fresh exact-SHA final request; promote only when both the focused regression and actual built Kentridge player launch are green for that source.
