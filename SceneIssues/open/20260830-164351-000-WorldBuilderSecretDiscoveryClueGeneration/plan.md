# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and representative built-player proof. This issue has no captures/marked regions. `WorldbuildingGalleryShowcase` is only the final consumer/evidence scene; feature logic belongs in reusable WorldBuilder/generated-feature systems.

## Hypotheses and results

- **Hidden-secret selection is missing/non-deterministic.** Falsified: production `SecretPlanner` already resolves canonical hidden candidates deterministically.
- **Clue generation needs a second hidden-location solver.** Rejected: route/clue planning consumes canonical `ResolvedSecretPlan` identity.
- **Route/readability/clue planning was missing.** Supported; implemented with stable IDs, semantic anchors, readability/diversity policy, diagnostics, and explicit bypass semantics.
- **Reusable interaction/discovery APIs are unavailable.** Falsified: `WorldObjectSceneRuntime` and canonical `SecretDiscoveryState` are available and integration regression run `33419056074` is green.
- **No production generated secret geometry exists.** Narrowly falsified: `CaveSecretPocketAuthoring` creates verified generated hidden-space/barrier topology and `CaveSecretPocketSecretCandidateProvider` projects that exact geometry into canonical WorldBuilder secret identity. Generated-cave bypass regression run `33420376990` is green.
- **Primitive validation/showcase composition can prove visual acceptance.** Falsified twice. Exact captures remained blockout/obstructed, and merged `AGENTS.md` now explicitly forbids parallel primitive renderers for player-visible validation.

## Selected direction

Keep reusable planning/runtime and production cave integration. Remove the showcase-local primitive clue composition, its presentation test, and the dedicated primitive WorldBuilder validation scene/module declaration. This removes the parallel art/runtime stack rather than polishing it again.

Representative proof must now use an existing production generated-world/presentation path. The cave path is the supported production generated-secret topology; do not invent a separate architecture secret-geometry system merely to satisfy a screenshot. If an architectural pattern still lacks a supported generated realization, document that limitation and use another supported generated feature as allowed by the issue.

## Remaining gates

1. Run exact-head targeted CI after scope cleanup, using `SecretGeneratedCaveBypassIntegrationTests.VerifiedGeneratedCaveBarrierFeedsAuthoredBreakableBypassPolicy` as the smallest production-geometry invariant.
2. Produce representative generated-world clue/route proof through a production consumer, then run exact `WorldbuildingGalleryShowcase` built-player replay and inspect full-resolution evidence.
3. Re-check blast radius/cost and every acceptance checkbox.
4. Only after all exact-SHA gates are green: close the SceneIssue, merge current master, and promote the exact feature head non-force.
