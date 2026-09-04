# Experiment 017 — Module test assembly compatibility

## Hypothesis

Exact run `33831464573` failed before player validation because the newly module-owned EditMode assemblies omitted repository-native test compatibility dependencies, not because secret planning, cave composition, Gallery integration, or built-player behavior regressed.

## Evidence

Exact CI request `8ac42fbacc7a3915ce13b3c660b39a9338324535` targeted feature SHA `f6a6ae76b0eaf1d4bc617ccd10346769672f7a2f`. Automatic module-plan derivation succeeded and selected `WorldBuilder`, `Composition/CaveWorldBuilder`, and `Composition/Showcase`, including their module-local EditMode assemblies and validation scenes plus Kentridge integration.

Compilation then failed in the relocated regression assemblies. The tests still use namespace `VoxelEngine.Tests.EditMode` and call `Assert.Multiple`, but the new module asmdefs did not reference `VoxelEngine.Tests.Support`, which owns the repository's NUnit 3.5 compatibility shim. The Showcase regression assembly also crosses a production public type backed by `Unity.Collections` and did not reference that assembly explicitly.

The previous WorldBuilder-owned test assembly referenced both `VoxelEngine.Tests.Support` and `Unity.Collections`, so moving the unchanged regression files exposed assembly-boundary dependencies that were previously inherited from the owning test assembly.

## Result / discriminator

Confirmed test-assembly compatibility defect. No production behavior change is justified by this failure.

## Fix

- Add `VoxelEngine.Tests.Support` to both new module EditMode asmdefs.
- Add `Unity.Collections` to `Game.Composition.Showcase.Tests.EditMode`.
- Leave production code and regression test bodies unchanged.
- Re-run exact-SHA targeted CI through `ci-test/fixes/agent-5`; require automatic module tests, all required module-local built-player scenes, Kentridge, and standalone SceneIssue replay to pass before closure.
