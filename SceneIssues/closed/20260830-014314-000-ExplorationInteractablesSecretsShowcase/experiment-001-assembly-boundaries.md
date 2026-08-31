# Experiment 001 — showcase assembly boundaries

## Trigger

The same acceptance gate (targeted PlayMode compilation) failed twice after materially different fixes, so another change requires an isolated minimal repro/root cause rather than another speculative edit.

## Failure 1 — parent could not consume composition

CI run `33341824451` compiled the exact feature tree based on `ff6476b487fde9364a04291014c89029c98350c2` and failed before tests executed. `Assets/Game/Composition/Showcase/ShowcaseWorld.WorldObjects.cs` could not resolve `ExplorationInteractablesSecretsShowcase`.

Minimal dependency repro:

- `ShowcaseWorld.WorldObjects.cs` belongs to the parent `Game.Composition.Showcase` assembly.
- `ExplorationInteractablesSecretsShowcase.cs` had been placed under `SceneRuntime/`, therefore in the child `VoxelEngine.Showcase` assembly.
- A parent assembly cannot consume a symbol defined only in its child without creating an invalid dependency direction.

Discriminator: move only the pure composition class to the parent assembly and compile again. Falsifier: the same unresolved composition symbol remains.

Result: after moving the pure composition class to `Assets/Game/Composition/Showcase/ExplorationInteractablesSecretsShowcase.cs`, the original unresolved-symbol errors disappeared in CI run `33341903081`. This confirms the first root cause.

## Failure 2 — child host lacks runtime reference

CI run `33341903081` compiled the exact feature tree based on `953c3d1329242ef01de770a0f1e22f4585156edb` and advanced past the first failure, then failed in `SceneRuntime/InteractablesShowcaseScene.cs`:

- `Game.Structures.Runtime` namespace not found.
- `WorldObjectSceneRegistry` type not found.

Minimal dependency repro:

- `InteractablesShowcaseScene.cs` is intentionally in child assembly `VoxelEngine.Showcase` because it is a Unity scene host.
- The host directly owns a `WorldObjectSceneRegistry`, whose type is in assembly `Game.Structures.Runtime`.
- `Assets/Game/Composition/Showcase/SceneRuntime/VoxelEngine.Showcase.asmdef` references `Game.Structures.Api`, `Game.Composition.Showcase`, and `Game.Composition.WorldObjects.Runtime`, but does **not** reference `Game.Structures.Runtime`.
- No gameplay assertion has executed yet; this is an assembly declaration failure, not evidence against the interaction composition.

Discriminator: add the one missing `Game.Structures.Runtime` asmdef reference, leaving code and runtime behavior unchanged, then compile the same regression again. Falsifier: unresolved `WorldObjectSceneRegistry` remains or a dependency cycle is reported.

## Required ownership boundary

`Game.Composition.Showcase` owns pure deterministic showcase composition data and may be consumed by the existing `ShowcaseWorld`. `VoxelEngine.Showcase` is a child scene/presentation assembly: it references the parent composition plus the runtime dependencies its MonoBehaviours actually instantiate. Shared `WorldObject` interaction semantics remain outside both compositions.
