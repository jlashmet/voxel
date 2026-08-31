# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, readability policy, explicit voxel-bypass policy, reusable route/discovery integration, and representative built-player proof. This SceneIssue has no captures or marked regions, so standalone-player captures are the visual evidence source. `WorldbuildingGalleryShowcase` is the required integration scene; a module-local scene may prove focused behavior but cannot substitute for representative generated-world acceptance.

## Hypotheses and discriminators

- **A: hidden-secret selection is missing/non-deterministic.** Falsified: production `SecretPlanner` resolves authoritative candidates deterministically and fails closed.
- **B: clue generation needs a second hidden-location solver.** Rejected: clue/route planning consumes canonical `ResolvedSecretPlan` identity.
- **C: deterministic route/readability/clue planning was missing.** Supported and implemented with stable IDs, semantic anchors, readability/diversity policy, bypass semantics, diagnostics, and focused regressions.
- **D: canonical discovery / reusable mechanism APIs are unavailable.** Falsified at the feature merge base `2edf4c2e151492f67c4a1c1b846a9b7948284aba`: `SecretDiscoveryState`, `WorldObjectDescriptor`, `WorldObjectSceneRegistry`, and `WorldObjectSceneRuntime` are present and accepted. `SecretDiscoveryLedger` composes with the canonical candidate-keyed authority rather than owning a second discovery store.
- **E: broad gallery captures can prove feature readability.** Falsified by run `33415154135`: the exact gallery player is usable, but foreground foliage and unrelated geometry dominate the replay views and the clue chain is not visually legible enough to count as representative proof.
- **F: incremental primitive-scene polish can reach the production visual bar.** Falsified twice. Run `33405791094` showed a sparse primitive tableau; after a materially different environmental/masonry pass, run `33415154135` still showed a primitive validation diorama. Experiment 005 isolates the root cause: both dedicated/gallery clue presentations use `GameObject.CreatePrimitive` rather than the production generated-world presentation boundary.
- **G: semantic bypass evidence is equivalent to generated secret-shell proof.** Rejected. `WorldBuilderVoxelCatalogue.Build` accepts only `AuthoredTownPlan`/Kentridge inputs and has no `ResolvedSecretPlan`/route realization or secret-specific voxel provenance, so actual protected-shell leakage cannot presently be measured without inventing a parallel realization path.

## Current implementation direction

Keep reusable planning semantics unchanged. Do not perform a third primitive-scene polish pass. Complete independent integration proof through the already-landed production runtime instead:

- `SecretRouteWorldObjectIntegrationTests` plans natural + breakable routes, executes the breakable mechanism through `WorldObjectSceneRuntime`, and composes both paths with the same canonical `SecretDiscoveryState` identity across unload/restore and repeated activation.
- Keep clue observation as presentation/memory only; canonical discovery remains the sole first-credit authority.
- Any subsequent visual correction must move representative proof onto an existing production generated-world/presentation consumer or reusable generated fixture using that same boundary. Adding more Unity primitives is not an acceptable discriminator after experiment 005.
- Do not fake generated-voxel bypass evidence with synthetic bounds/primitives. The acceptance remains blocked until the production secret-plan -> voxel realization boundary exists.

## Remaining gates

1. Let targeted run `33416403719` for exact feature SHA `481f63284edb549f1e4d8211738809cb82043f75` finish without replacement. If green, mark reusable mechanism/canonical discovery reload integration proven; if it fails, fix the completed failure cause before retrying on the same transport.
2. Representative generated-world clue/route proof in exact `WorldbuildingGalleryShowcase` remains open. Use only a production presentation boundary after the two-fix root-cause isolation; no further primitive-scene polish.
3. Generated voxel-shell bypass validation remains blocked because production `WorldBuilderVoxelCatalogue` does not consume resolved secret/route plans. Preserve this blocker and acceptance verbatim rather than substituting semantic-only evidence.
4. Keep cost/blast-radius evidence current. Do not close, move the issue, set `status=fixed`, merge master, or push to master until every required acceptance item is proven.
