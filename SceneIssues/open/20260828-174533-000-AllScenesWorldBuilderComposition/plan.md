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
- Exact-source run `33201049016` exposed a product defect: organic Kentridge had zero legacy streets and inferred routes, but `SettlementStreetTraversalFacts` only graphed streets and rejected route-backed site access. Product fix `5c87db87921c4a8b25dbaaf0b9866cecb98ce375` adds deterministic route/intersection traversal while retaining legacy street support.
- Subsequent exact-source run `33202598302` built/launched the real Kentridge player successfully and progressed through generated settlement publication/reachability; its only PlayMode failure was the separate opening-camera height expectation (`~1.72m` above focus versus `>2.5m`). Camera choreography is explicitly outside this architecture issue and was not changed.
- Focused behavioral regression `VoxelEngine.Tests.PlayMode.WorldBuilderProductionScenePlayTests.KentridgePlayableScene_PublishesWorldBuilderEnvironmentWithoutPresentationCoupling` now loads the real build-settings scene and stops at the architecture boundary: published near-surface coverage, Kentridge/Hightown plans, region theme/corridor/life composition, and resident pub entrance/interior/exterior regions.

## Blast radius / cost / final gate

- Seeds, scene YAML, script GUIDs, authored content choices and existing backend costs are preserved. Semantic planning is O(feature count); traversal adds bounded pairwise exact-intersection preprocessing over short settlement path segments and no per-frame work.
- No other capture is modified and `.github/test-request.json` remains absent from feature-branch changes.
- Merge current `origin/master`, then create exactly one fresh final targeted-CI transport from the exact candidate SHA. Promote only after the focused production regression and required built-player gate are green for that exact source.
