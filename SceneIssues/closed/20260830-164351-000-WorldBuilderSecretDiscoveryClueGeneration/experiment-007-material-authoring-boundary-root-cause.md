# Experiment 007 — material authoring boundary root cause

## Trigger

Exact-head targeted CI run `33452883114` failed during Unity script compilation after earlier, materially different compile fixes. Per the assignment rule, no further speculative fix was attempted until the repeated compile symptom was reduced to a minimal root cause.

## Minimal reproduction

The dedicated validation scene called:

```csharp
StructuresComposition.CreateAuthoringSession(
    _world.ReadStorage,
    _world.MutationStorage,
    _world.Palette,
    writeBudget: 4_000_000);
```

`StructuresComposition.CreateAuthoringSession` requires its third argument to implement `IMaterialAuthoringCatalogue`. `ShowcaseWorld.Palette` is intentionally a `MaterialPaletteView`, so the call cannot compile.

## Source evidence

`MaterialPalette` is the mutable runtime catalogue and implements `IMaterialAuthoringCatalogue`. `ShowcaseWorld` owns that mutable palette privately through its storage lifetime and deliberately exposes only `MaterialPaletteView Palette` to ordinary consumers. This means the validation scene was crossing the material-authoring boundary rather than merely missing a namespace or cast.

## Resolution

Keep the mutable catalogue private. `ShowcaseWorld` now exposes a composition-owned `CreateStructureAuthoringSession(int writeBudget)` capability that wires its owned read store, mutation store, and private mutable material catalogue into the existing `StructuresComposition.CreateAuthoringSession` factory. The validation scene consumes only the returned `IStructureAuthoringSession`.

This preserves the existing read-only palette API and keeps runtime material-authoring ownership inside the game composition root instead of weakening the storage boundary for a validation scene.

## Next discriminator

Run the focused clue-presentation regression and built SceneIssue replay from the exact new feature SHA using only `ci-test/fixes/agent-5`. If compilation succeeds, inspect the dedicated built-player screenshots full-resolution before adding the final gallery consumer.
