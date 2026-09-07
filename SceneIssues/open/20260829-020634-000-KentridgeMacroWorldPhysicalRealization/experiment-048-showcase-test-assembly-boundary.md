# Experiment 048 — Showcase test assembly boundary

## Exact evidence

Exact feature source `775ddecad735f2dbfad8306e510dda133de5d05f` was validated by run `33928292286` through transport `fb83918f6ca213f438446f1a00883921bd5ed570`.

The run passed exact-source admission and repository module-plan derivation. Both automatic module validation and the standalone SceneIssue build then stopped at Unity script compilation before any requested/module/player behavior executed.

Unity reported two assembly definitions in the same folder:

- `Assets/Game/Composition/Showcase/Tests/EditMode/Game.Composition.Showcase.Tests.asmdef`
- `Assets/Game/Composition/Showcase/Tests/EditMode/Game.Composition.Showcase.Tests.EditMode.asmdef`

The second definition was introduced by this feature for `ShowcaseFeatureResidencyTests`. That test only depends on `Game.Composition.Showcase`, `VoxelEngine.Storage.Api`, `VoxelEngine.Structures.Api`, `Unity.Collections`, and `Unity.Mathematics`, all already referenced by the established `Game.Composition.Showcase.Tests` assembly.

## Conclusion / correction

This is an agent-owned test-assembly composition defect, not product runtime evidence. Remove the redundant feature-added asmdef and its `.meta`; keep `ShowcaseFeatureResidencyTests` in the established `Game.Composition.Showcase.Tests` assembly. No production code, runtime policy, validation semantics, budgets, or renderer behavior changes.

The duplicate module-owner failure from the preceding exact run is no longer present after the current-master reconciliation; run `33928292286` successfully derived the module plan before reaching this compile boundary.
