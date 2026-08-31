# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, readability policy, explicit voxel-bypass policy, reusable route/discovery integration, and representative built-player proof. This SceneIssue has no captures or marked regions, so standalone-player captures are the visual evidence source. The module-local player scene is the dedicated feature proof; `WorldbuildingGalleryShowcase` remains the required integration scene from the issue acceptance and is not a substitute for the dedicated proof.

## Hypotheses and discriminators

- **A: hidden-secret selection is missing/non-deterministic.** Falsified: production `SecretPlanner` resolves authoritative candidates deterministically and fails closed.
- **B: clue generation needs a second hidden-location solver.** Rejected: clue/route planning consumes canonical `ResolvedSecretPlan` identity.
- **C: deterministic route/readability/clue planning was missing.** Supported and implemented with stable IDs, semantic anchors, readability/diversity policy, bypass semantics, diagnostics, and focused regressions.
- **D: canonical discovery integration was unavailable.** Falsified on master `2edf4c2e151492f67c4a1c1b846a9b7948284aba`: `SecretDiscoveryState` is the canonical candidate-keyed authority. `SecretDiscoveryLedger` composes clue observation with that authority and regressions prove one first-discovery event across revisit/restore.
- **E: broad gallery captures can prove feature readability.** Falsified: foliage/framing obscured the staged cues, so the module-local validation scene became the dedicated clue-focused environment.
- **F: a sparse three-stage environmental tableau is sufficient production visual proof.** Falsified by exact-SHA run `33405791094` for `dc1bab0cad0170b448fef055e53842e30e6149a3`: the scene is readable but still prototype/blockout quality because the path, stones, and ruin remain isolated featureless primitives without believable surrounding construction or environmental integration.

## Selected fix

Keep reusable planning/discovery semantics unchanged. Improve only the module-owned `WorldBuilderSecretDiscoveryValidation` composition: a worn-earth approach with repeated displaced-stone evidence, layered masonry ruin construction, concealed panel courses and seam/weathering language, rubble/moss/ivy, and forest-edge framing. The clue language remains environmental and non-glowing. Canonical discovery is still validated directly through `SecretDiscoveryState`; no showcase-local persistence or second interaction authority is introduced.

## Remaining gates

1. Run focused exact-SHA regression plus repository-derived module/Kentridge gates for the new composition; inspect full-resolution dedicated captures and classify visual quality.
2. If the same dedicated readability/quality symptom fails again, stop visual iteration and isolate the minimum presentation root cause before another fix.
3. Validate the exact built `WorldbuildingGalleryShowcase` integration scene without treating its screenshots as the dedicated feature scene.
4. Validate remaining interactable-route and representative-pattern acceptance against the landed reusable runtime; do not duplicate its behavior.
5. Validate generated-voxel bypass facts only if production secret geometry realization now exists; otherwise keep that acceptance blocker explicit rather than substituting synthetic evidence.
6. Check cost/blast radius. Do not close until every required acceptance item is proven; then close `open -> closed`, set `status=fixed`/`resolvedUtc`, merge current master, revalidate if the exact feature SHA changes, and push that exact head to master non-force.
