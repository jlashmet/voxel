# Plan — Structures module API/runtime boundary

## Baseline

Starting feature/master commit SHA: `c73ab9d123ad29a1f1f1215552519a303c16d5fe` (`Add SceneIssue for Structures API/runtime boundary`). Its tree SHA is `8fa7a2bc061d0b41b583a0ba43c29573c8d3ab9e`; the earlier plan accidentally recorded that tree SHA as the starting commit.

The resumed feature branch is one documentation commit ahead at `d5812787ee8e3ebe2a7094b80c63a2e2adae8227`, with parent `c73ab9d123ad29a1f1f1215552519a303c16d5fe`. Current `master` is still `c73ab9d123ad29a1f1f1215552519a303c16d5fe` as of the resumed inventory.

At baseline, `Assets/Game/Structures/Runtime/Game.Structures.Runtime.asmdef` directly references `VoxelEngine.Structures.Runtime`, and `Assets/Game/Structures/Runtime/CastleCaveAuthoring.cs` aliases/uses `VoxelEngine.Structures.Runtime.CaveAuthoring` and `VoxelEngine.Structures.Runtime.CaveAuthoringResult`.

## Observed behavior / dependency inventory

The offending assembly dependency was introduced by commit `c651ae380a29a658b160e5e2eb73bd0be9a09476`, whose only changed file was `Game.Structures.Runtime.asmdef`; the source-level cave aliases now make that assembly reference live rather than stale metadata.

The concrete in-module call chain is:

`CastleAuthoringBuild.Step()` stage 7 -> `CastleDungeonAuthoring.Author(IStructureAuthoringSession, CastlePlan)` -> `CastleCaveAuthoring.Author(IStructureAuthoringSession, CastlePlan, int3)` -> static `VoxelEngine.Structures.Runtime.CaveAuthoring.Author(...)`.

`CastleCaveAuthoring` owns the castle seed salt, compatibility `CaveConfig`, semantic underground request/anchor, and `GameMaterialIds` -> `CaveMaterialPalette` mapping. Those remain Game.Structures policy. `VoxelEngine.Structures.Runtime.CaveAuthoring` owns validation and delegates the actual algorithm to `CaveNetworkAuthoringCore`; that execution remains Runtime-owned.

The minimum injection seam is therefore one `ICaveAuthoring` capability threaded through the existing castle build/dungeon/cave path. External constructor/bootstrap callers of `CastleAuthoringBuild` still need to be enumerated before changing public constructor signatures; compile after removing the private assembly reference is the required backstop for missed source dependencies.

## Acceptance

- `Game.Structures.Runtime` depends on `VoxelEngine.Structures.Api`, not `VoxelEngine.Structures.Runtime`.
- The shared VoxelEngine cave algorithm remains single-owner in `VoxelEngine.Structures.Runtime`.
- The minimum cave authoring contract/result needed by Game.Structures lives in `VoxelEngine.Structures.Api` and is injected from composition.
- The architecture suite rejects cross-module production dependencies on another module's private implementation assembly while preserving same-module, API, Composition/bootstrap, Tests/Editor, and explicit Foundation exceptions.
- Focused Structures/architecture tests and automatically derived validation are green on the exact feature SHA.

## Ownership / selected approach

`Game.Structures` owns castle-specific cave configuration, seed/anchor selection, and game-material mapping. `VoxelEngine.Structures.Api` owns the public cave-authoring capability contract. `VoxelEngine.Structures.Runtime` owns deterministic generic cave generation. Composition constructs the concrete runtime capability and injects it into the castle authoring path.

Introduce API-owned `CaveAuthoringResult` plus narrow `ICaveAuthoring` matching the current operation. Preserve the existing static runtime entry point for existing same-module/integration callers while making the concrete runtime type implement `ICaveAuthoring`; this avoids duplicating validation or `CaveNetworkAuthoringCore` delegation. Thread the capability through `CastleAuthoringBuild -> CastleDungeonAuthoring -> CastleCaveAuthoring`, remove the Runtime assembly reference, and update composition/tests.

## Hypotheses / discriminator

1. The direct runtime dependency is limited to cave authoring and can be replaced by one narrow API capability. Evidence identifies only the concrete cave aliases in `CastleCaveAuthoring`; compile after removing the asmdef reference will discriminate any additional hidden source dependencies.
2. Other repository modules may already violate the same API-only rule. The expanded repository-wide asmdef validator will enumerate them. Do not blanket-whitelist; classify each by same-module/API/Composition/Test/Editor/Foundation versus true production violation.

## Blast radius / remaining gates

Expected blast radius is Structures API/runtime/composition constructors, castle authoring call sites/tests, and CI architecture tests. No cave algorithm rewrite, visual redesign, or campaign-policy change is intended. Remaining gates: finish external constructor/composition inventory, compile after boundary removal, focused cave/castle behavior regressions, architecture rule regressions and full scan, exact-SHA targeted/module validation, final diff review.
