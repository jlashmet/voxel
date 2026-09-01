# Plan — Structures module API/runtime boundary

## Baseline

Starting feature/master commit SHA: `c73ab9d123ad29a1f1f1215552519a303c16d5fe` (`Add SceneIssue for Structures API/runtime boundary`). Its tree SHA is `8fa7a2bc061d0b41b583a0ba43c29573c8d3ab9e`; the earlier plan accidentally recorded that tree SHA as the starting commit.

At baseline, `Assets/Game/Structures/Runtime/Game.Structures.Runtime.asmdef` directly referenced `VoxelEngine.Structures.Runtime`, and `CastleCaveAuthoring.cs` directly used its `CaveAuthoring`/`CaveAuthoringResult` types.

## Evidence / dependency inventory

The castle cave call chain is `CastleAuthoringBuild.Step()` stage 7 -> `CastleDungeonAuthoring.Author` -> `CastleCaveAuthoring.Author` -> engine cave authoring. Castle seed salt, compatibility `CaveConfig`, underground request/anchor, and `GameMaterialIds` palette mapping remain Game.Structures policy; validation/network generation remain VoxelEngine Runtime.

The required compile probe removed the private asmdef reference and ran on feature SHA `31742c23b3948fb9a8fabfc823b3435ebd55c42a` through request `0168bc7499e5e1b9f9e3dbb066cbea218f4a7ce0` (workflow `33459718833`). It failed compilation and falsified hypothesis 1: cave authoring was not the only live private dependency. The compiler identified the same boundary defect in `CathedralAuthoring`, `CathedralWorldbuildingAuthoring`, `ChurchAuthoring`, `ShedAuthoring`, `TempleAuthoring`, and `StructuralDecorationHandoffAdapter` (generic openings/roofs/buttresses/stairs/columns plus structural composition result types). This is product evidence, not infrastructure; the Runtime asmdef dependency will not be restored.

## Selected ownership / fix

`VoxelEngine.Structures.Api` owns API value contracts and injected semantic capabilities. `VoxelEngine.Structures.Runtime` owns all generic deterministic execution. `Game.Structures` owns game archetype/castle policy and maps its configs/materials into those API capabilities. Composition/bootstrap constructs concrete Runtime services and injects them.

The cave seam is API-owned `CaveAuthoringResult` + `ICaveAuthoring`; `CaveAuthoringService` delegates to the existing Runtime validation/core, and the capability is threaded through `CastleAuthoringBuild -> CastleDungeonAuthoring -> CastleCaveAuthoring`.

The compile probe demonstrated a second narrow reuse seam: `IStructureComponentAuthoring`, with config-driven request values for the already-shared opening/roof/stair/column/buttress emitters. One Runtime service delegates to the existing single-owner emitters; Game archetypes receive that capability rather than naming five concrete Runtime types or copying their algorithms. Structural composition inspection records consumed by Game must likewise be API-owned values rather than Runtime namespace types.

The repository architecture validator structurally parses repository asmdefs, classifies module roots from paths, permits same-owner/API/Composition/Test/Editor/exact Foundation categories, and rejects ordinary production cross-module implementation references with actionable paths. GUID-form repository references still need explicit resolution before final acceptance.

## Remaining gates

Finish converting the compile-probe leaks and external callers; move shared structural composition output records to Api; add recording `ICaveAuthoring` regression; resolve GUID asmdef references in the validator and classify full-scan findings; run a second focused compile/test request only after the product fixes are complete. If that second materially different fix still fails the same compile acceptance symptom, isolate the remaining caller/root cause set before any third fix. Then run focused Structures tests, repository-derived module validation/Kentridge as selected by CI, review blast radius, record exact green SHA, close, merge current master, and promote non-force.
