# Plan — Structures module API/runtime boundary

## Baseline

Starting feature/master commit SHA: `c73ab9d123ad29a1f1f1215552519a303c16d5fe` (`Add SceneIssue for Structures API/runtime boundary`). Its tree SHA is `8fa7a2bc061d0b41b583a0ba43c29573c8d3ab9e`; the earlier plan accidentally recorded that tree SHA as the starting commit.

At baseline, `Assets/Game/Structures/Runtime/Game.Structures.Runtime.asmdef` directly referenced `VoxelEngine.Structures.Runtime`, and `CastleCaveAuthoring.cs` directly used its `CaveAuthoring`/`CaveAuthoringResult` types.

## Evidence / dependency inventory

The castle cave call chain is `CastleAuthoringBuild.Step()` stage 7 -> `CastleDungeonAuthoring.Author` -> `CastleCaveAuthoring.Author` -> engine cave authoring. Castle seed salt, compatibility `CaveConfig`, underground request/anchor, and `GameMaterialIds` palette mapping remain Game.Structures policy; validation/network generation remain VoxelEngine Runtime.

The dependency-removal compile probe on SHA `31742c23b3948fb9a8fabfc823b3435ebd55c42a` showed cave authoring was not the only private dependency: cathedral/church/shed/temple component emitters and structural handoff result types also crossed the Runtime boundary. Those were converted to API-owned semantic/config-driven capabilities rather than restoring the dependency.

After those seams were implemented, SHA `9396f980bd9da5c84eb60f306fd6ce8f22cdfe80` exposed stale callers/composition only. Per the two-failure rule that compiler set was isolated before further fixes. Subsequent caller fixes reached SHA `4221dd226a601a91bda6b9ef5c245c683f9a4194`.

Exact-head run `33467572832` on that source failed compilation only in `Assets/Tests/EditMode/CastleCaveMigrationTests.cs`: two calls still used the pre-injection `CastleCaveAuthoring.Author(session, plan, at)` signature. This is a distinct stale integration-test caller, not a new architecture defect. The test now constructs the real `VoxelEngine.Structures.Runtime.CaveAuthoringService` and passes it through the API capability, preserving the shared generator and deterministic comparison. The same run also showed that explicitly supplying this feature ID to `showcase-player-capture.sh --scene-issue` is invalid because the architecture feature has no capture payload. The next request therefore leaves `scene_issue` empty and relies on the production-diff-derived module validation/Kentridge gate required by repository policy.

## Selected ownership / fix

`VoxelEngine.Structures.Api` owns API value contracts and injected semantic capabilities. `VoxelEngine.Structures.Runtime` owns generic deterministic execution. `Game.Structures` owns game archetype/castle policy and maps configs/materials into those API capabilities. Composition/bootstrap constructs concrete Runtime services and injects them.

The cave seam is API-owned `CaveAuthoringResult` + `ICaveAuthoring`; `CaveAuthoringService` delegates to the existing Runtime validation/core, and the capability is threaded through `CastleAuthoringBuild -> CastleDungeonAuthoring -> CastleCaveAuthoring`.

The compile probe also demonstrated a narrow reusable `IStructureComponentAuthoring` seam for shared opening/roof/stair/column/buttress emitters. One Runtime service delegates to the existing emitters; Game archetypes receive semantic config rather than naming concrete Runtime types or copying algorithms. Structural composition inspection records consumed by Game are API-owned values.

The repository architecture validator structurally parses repository asmdefs, classifies module roots from paths, permits same-owner/API/Composition/Test/Editor/exact Foundation categories, rejects ordinary production cross-module implementation references with actionable paths, and resolves GUID-form repository references before classification.

## Remaining gates

Submit the current exact feature SHA through `ci-test/fixes/agent-2` with focused architecture, cave boundary, migration integration, and structural handoff tests. Confirm the required focused tests and repository-derived module validation/Kentridge gate execute successfully, inspect the final diff/blast radius, record the green SHA, complete `issue.json`, close the folder, merge current `origin/master`, revalidate if the merge changes affected work, and promote the exact head to `master` non-force.
