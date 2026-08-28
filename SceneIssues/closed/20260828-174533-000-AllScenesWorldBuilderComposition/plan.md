# Plan: WorldBuilder-only scene composition

## Evidence / hypotheses

- Architecture capture has no screenshots, frames, poses, annotations, or marked visual regions. Evidence is production scene/bootstrap ownership plus runtime CI.
- Audit covered every production/showcase `.unity` scene and former `Assets/Scenes/Kentridge` / `Assets/Scenes/Showcase` bootstrap source. Generation bypasses included direct storage/catalogue, terrain, structure, vegetation/life, far-terrain and procedural-mesh construction; presentation camera/lighting/UI/input/metrics were separated by the discriminator “creates or mutates generated gameplay environment content.”
- Hypothesis 1 — scene bootstraps own generated-world realization despite reusable generators — supported. Hypothesis 2 — source location/naming alone is the defect — rejected. Hypothesis 3 — every large scene script is a violation — rejected by presentation-only counterexamples.
- Selected boundary: scenes may choose seeds, subsets, placement intent and presentation, but generated environment intent enters through WorldBuilder semantic recipes/town authoring; reusable Game/Voxel composition owns concrete generator choices.

## Fix / regression

- Added `WorldEnvironmentSpec` recipes and shared Showcase resolution while preserving detailed-structure, fortified-landmark and gallery compositions; unsupported semantic combinations fail explicitly.
- Routed Kentridge/Hightown through `WorldBuilderTownAuthoring`; preserved serialized GUIDs/assembly identity while moving generation implementations out of `Assets/Scenes`.
- EditMode coverage exercises production town/Showcase recipes, distinct compositions, organic Kentridge traversal, and supplements behavior with an `Assets/Scenes` ownership guard.
- Run `33201049016` exposed stale street-only traversal; `SettlementStreetTraversalFacts` now graphs inferred routes/intersections plus legacy streets. Run `33202598302` proved reachability then failed only unrelated camera presentation, which remained untouched.
- Focused production regression `WorldBuilderProductionScenePlayTests.KentridgePlayableScene_PublishesWorldBuilderEnvironmentWithoutPresentationCoupling` loads the real scene and verifies published WorldBuilder environment/circulation/life state without camera choreography.
- Run `33206424685` proved that regression green but exposed missing standalone-player profile routing. The existing Kentridge capture profile now recognizes the new test class.

## Blast radius / cost / result

- Seeds, scene YAML, GUIDs, authored choices and backend costs are preserved. Semantic planning is O(feature count); traversal adds bounded pairwise exact-intersection preprocessing over short settlement paths with no per-frame work. Harness change only broadens filter routing to the existing Kentridge player profile.
- Final exact source `dc48feab316195de1c677ec605fd9f29f42f6ef8` passed run `33210383419`: focused PlayMode regression 1/1 and real macOS Kentridge player build/launch, nine screenshots through ~92s, no runtime exception/crash/fatal markers.
- Feature contains no `.github/test-request.json`; only this capture is promoted. Merge latest `origin/master` after closure bookkeeping, then non-force push the exact feature head to master.
