# Plan — Structures module API/runtime boundary

## Observed behavior

`Game.Structures.Runtime` references `VoxelEngine.Structures.Runtime`, and `CastleCaveAuthoring` directly aliases the concrete engine `CaveAuthoring` and `CaveAuthoringResult` runtime types. The existing architecture test only prevents `VoxelEngine -> Game`; it does not enforce API-only dependencies between sibling/vertical modules.

## Acceptance

- `Game.Structures.Runtime` depends on `VoxelEngine.Structures.Api`, not `VoxelEngine.Structures.Runtime`.
- The shared VoxelEngine cave algorithm remains single-owner in `VoxelEngine.Structures.Runtime`.
- The minimum cave authoring contract/result needed by Game.Structures lives in `VoxelEngine.Structures.Api` and is injected from composition.
- The architecture suite rejects cross-module production dependencies on another module's private implementation assembly while preserving same-module, API, Composition/bootstrap, Tests/Editor, and explicit Foundation exceptions.
- Focused Structures/architecture tests and automatically derived validation are green on the exact feature SHA.

## Ownership / selected approach

`Game.Structures` owns castle-specific cave configuration, seed/anchor selection, and game-material mapping. `VoxelEngine.Structures.Api` owns the public cave-authoring capability contract. `VoxelEngine.Structures.Runtime` owns deterministic generic cave generation. Composition constructs the concrete runtime capability and injects it into the castle authoring path.

Introduce an API result plus narrow `ICaveAuthoring`-style interface matching the current authoring operation. Keep `CaveNetworkAuthoringCore` and algorithm implementation private to Runtime. Thread the API capability through `CastleAuthoringBuild -> CastleDungeonAuthoring -> CastleCaveAuthoring` (or the equivalent existing composition path), remove the Runtime assembly reference, and update callers/tests.

## Hypotheses / discriminator

1. The direct runtime dependency is limited to cave authoring and can be replaced by one narrow API capability. Evidence already identifies the concrete aliases in `CastleCaveAuthoring`; compile after removing the asmdef reference will reveal any additional runtime leaks.
2. Other repository modules may already violate the same API-only rule. The expanded repository-wide asmdef validator will enumerate them. Do not blanket-whitelist; classify each by same-module/API/Composition/Test/Editor/Foundation versus true production violation.

## Blast radius / remaining gates

Expected blast radius is Structures API/runtime/composition constructors, castle authoring call sites/tests, and CI architecture tests. No cave algorithm rewrite, visual redesign, or campaign-policy change is intended. Remaining gates: compile after boundary removal, focused cave/castle behavior regressions, architecture rule regressions and full scan, exact-SHA targeted/module validation, final diff review.
