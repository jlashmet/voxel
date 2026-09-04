# Experiment 021 — Showcase publication test assembly dependency

## Observation

Exact targeted run `33837600536` validated source `9a7c7a5ce93b2f942c6cbbd3aec254417b59915f` through request commit `13183c5afa4da36d6f22c1f34df0b17aa6351896`. Automatic planning correctly selected CaveWorldBuilder, Showcase, and WorldBuilder plus their module-local players and Kentridge integration, but Unity aborted before executing tests because scripts did not compile. The standalone Gallery player build failed for the same compile error.

The exact compiler diagnostic repeated three times:

`Assets/Game/Composition/Showcase/Tests/EditMode/WorldbuildingGallerySecretDiscoveryPublicationTests.cs(28,13): error CS0012: The type 'WorldObjectRuntimeComposition' is defined in an assembly that is not referenced. You must add a reference to assembly 'Game.Composition.WorldObjects.Runtime'.`

## Competing hypotheses

1. The production post-bake publication change is invalid. **Not exercised**: compilation stopped before any test or player ran.
2. The publication regression itself uses an unsupported runtime API. **Rejected**: the API is already exposed by the production `ShowcaseWorld` construction boundary; the compiler specifically reports only a missing assembly reference.
3. The Showcase EditMode test asmdef is missing the transitive public construction dependency. **Supported**: `Game.Composition.Showcase.Tests.EditMode.asmdef` references `Game.Composition.Showcase` but not `Game.Composition.WorldObjects.Runtime`, while the new regression constructs `ShowcaseWorld` and therefore must resolve the exposed `WorldObjectRuntimeComposition` type.

## Fix

Add `Game.Composition.WorldObjects.Runtime` to `Game.Composition.Showcase.Tests.EditMode` references. This is a module-owned test-compilation dependency only; production behavior is unchanged.

## Expected discriminator

The next exact-SHA run must pass compilation, execute `WorldbuildingGallerySecretDiscoveryPublicationTests`, then continue into CaveWorldBuilder/Showcase/WorldBuilder module-local players, Kentridge integration, and the exact `WorldbuildingGalleryShowcase` SceneIssue replay. Any later failure is a new behavioral/visual result and must be diagnosed rather than treating this compile dependency as unresolved.
