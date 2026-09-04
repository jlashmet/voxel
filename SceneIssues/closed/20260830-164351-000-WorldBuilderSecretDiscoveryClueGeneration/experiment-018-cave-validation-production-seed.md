# Experiment 018 — Cave validation production seed

## Symptom

Exact-SHA run `33832061126` (source `f95df847033e4d8dd1bf7e7917b22d7391015ab7`) cleared the previous test-assembly compile failure. All selected EditMode regressions passed (`Game.Composition.CaveWorldBuilder.Tests.EditMode` 3/3, `Game.Composition.Showcase.Tests.EditMode` 6/6, `VoxelEngine.Tests.EditMode` 5/5), and the requested SceneIssue built player reached the secret-discovery acceptance path with `boundaryClueVoxels=31` and `naturalClueVoxels=30`.

Automatic module validation then failed in the CaveWorldBuilder-owned player scene before cave secret authoring began. `CaveWorldBuilderSecretPocketValidation.OnEnable` constructed `ShowcaseWorld` with validation-only seed `0x43415645`; eager `ShowcaseCatalogue.Build` reached `KentridgeTownPlanner.PlaceNamedSites` and threw because the bounded candidate set for `AwonHouse` was exhausted.

## Competing hypotheses

### H1 — The validation-only world seed is invalid for the production Showcase catalogue

Evidence supporting H1:

- The exception stack is entirely inside `ShowcaseWorld` construction / Showcase catalogue / Kentridge town planning, before `CaveAuthoring` or `CaveSecretPocketComposition` executes.
- The CaveWorldBuilder EditMode behavioral regressions pass 3/3 in the same exact run.
- The requested Gallery SceneIssue built-player path reaches both secret clue routes in the same exact run.
- The production `VoxelShowcase` driver uses deterministic seed `0x5EED1234`; the failing Cave validation introduced its own mnemonic `0x43415645` seed instead of reusing that established production configuration.

### H2 — Cave secret authoring or clue presentation is crashing in built player

Evidence against H2:

- No Cave authoring or clue-presentation frame appears in the failing stack; construction fails first.
- The CaveWorldBuilder behavioral test assembly is green.
- Earlier dedicated SecretDiscovery built-player evidence already exercised the production cave authoring, clue presentation and destruction path successfully.

### H3 — The Cave validation should replace ShowcaseWorld with a lightweight storage fixture

Rejected for this fix. That would weaken the module-owned player proof by bypassing the same production storage/material/rendering/destruction composition used by the scene. The smallest acceptance-preserving correction is to keep `ShowcaseWorld` and use its established production deterministic seed.

## Change

Change only `CaveWorldBuilderSecretPocketValidation`'s default seed from `0x43415645` to the production Showcase seed `0x5EED1234`. The cave terrain query, cave generation request, clue randomization, preloaded production world terrain, and renderer all continue to share one deterministic seed. No shipping production behavior is changed.

## Expected discriminator

A fresh exact-SHA run should get past `ShowcaseWorld` construction in the CaveWorldBuilder player validation, emit `CaveWorldBuilder secret validation ready:`, later emit `CaveWorldBuilder secret validation wall destroyed:`, and continue to the Showcase, WorldBuilder and Kentridge player validations. Any subsequent failure should therefore be attributable to the actual module acceptance path rather than the unrelated Kentridge catalogue bootstrap.
