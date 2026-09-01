# Plan — Structures module API/runtime boundary

## Baseline

Starting feature/master commit SHA: `c73ab9d123ad29a1f1f1215552519a303c16d5fe` (`Add SceneIssue for Structures API/runtime boundary`). Its tree SHA is `8fa7a2bc061d0b41b583a0ba43c29573c8d3ab9e`; the earlier plan accidentally recorded that tree SHA as the starting commit.

At baseline, `Assets/Game/Structures/Runtime/Game.Structures.Runtime.asmdef` directly referenced `VoxelEngine.Structures.Runtime`, and `CastleCaveAuthoring.cs` directly used its `CaveAuthoring`/`CaveAuthoringResult` types.

## Evidence / dependency inventory

The castle cave call chain is `CastleAuthoringBuild.Step()` stage 7 -> `CastleDungeonAuthoring.Author` -> `CastleCaveAuthoring.Author` -> engine cave authoring. Castle seed salt, compatibility `CaveConfig`, underground request/anchor, and `GameMaterialIds` palette mapping remain Game.Structures policy; validation/network generation remain VoxelEngine Runtime.

The required compile probe removed the private asmdef reference and ran on feature SHA `31742c23b3948fb9a8fabfc823b3435ebd55c42a` through request `0168bc7499e5e1b9f9e3dbb066cbea218f4a7ce0` (workflow `33459718833`). It failed compilation and falsified hypothesis 1: cave authoring was not the only live private dependency. The compiler identified the same boundary defect in `CathedralAuthoring`, `CathedralWorldbuildingAuthoring`, `ChurchAuthoring`, `ShedAuthoring`, `TempleAuthoring`, and `StructuralDecorationHandoffAdapter` (generic openings/roofs/buttresses/stairs/columns plus structural composition result types). This is product evidence, not infrastructure; the Runtime asmdef dependency will not be restored.

After implementing those seams, the exact feature SHA `9396f980bd9da5c84eb60f306fd6ce8f22cdfe80` was submitted through request `5e7c38e8f5f7f7919128181c0f7601ea9a4a665e` (workflow `33462763230`). It again failed with compiler errors. Per the two-failure rule, the next change was blocked until the minimal remaining cause was isolated from the CI artifact. The compiler-error set is stale callers only: Structures tests still invoking the old component-authoring signatures, Showcase composition still invoking those old signatures, and two old `CastleAuthoringBuild` constructors. No new private Runtime dependency or algorithm defect was implicated. The fix is therefore caller/composition wiring, not another boundary redesign.

## Selected ownership / fix

`VoxelEngine.Structures.Api` owns API value contracts and injected semantic capabilities. `VoxelEngine.Structures.Runtime` owns all generic deterministic execution. `Game.Structures` owns game archetype/castle policy and maps its configs/materials into those API capabilities. Composition/bootstrap constructs concrete Runtime services and injects them.

The cave seam is API-owned `CaveAuthoringResult` + `ICaveAuthoring`; `CaveAuthoringService` delegates to the existing Runtime validation/core, and the capability is threaded through `CastleAuthoringBuild -> CastleDungeonAuthoring -> CastleCaveAuthoring`.

The compile probe demonstrated a second narrow reuse seam: `IStructureComponentAuthoring`, with config-driven request values for the already-shared opening/roof/stair/column/buttress emitters. One Runtime service delegates to the existing single-owner emitters; Game archetypes receive that capability rather than naming five concrete Runtime types or copying their algorithms. Structural composition inspection records consumed by Game are API-owned values rather than Runtime namespace types.

Tests wire the real Runtime service only in test composition. Showcase wires `StructureComponentAuthoringService` and `CaveAuthoringService` only in its Composition namespace, preserving the rule that ordinary `Game.Structures.Runtime` sees API contracts only.

The repository architecture validator structurally parses repository asmdefs, classifies module roots from paths, permits same-owner/API/Composition/Test/Editor/exact Foundation categories, rejects ordinary production cross-module implementation references with actionable paths, and now resolves GUID-form repository references before classification.

## Remaining gates

Finish the isolated stale-caller wiring and re-run focused compile/tests through `ci-test/fixes/agent-2`. Then classify the repository-wide validator result, run focused Structures tests plus repository-derived module validation/Kentridge as selected by CI, review blast radius, record the exact green SHA, close only after every required checkbox is proven, merge current master, and promote non-force.
