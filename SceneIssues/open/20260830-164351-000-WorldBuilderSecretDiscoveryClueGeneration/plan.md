# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and representative built-player proof. This issue has no captures/marked regions. `WorldbuildingGalleryShowcase` is only the final consumer/evidence scene; feature logic and primary visual development belong in reusable WorldBuilder/generated-feature systems and a dedicated module-local validation scene.

## Hypotheses and results

- **Hidden-secret selection is missing/non-deterministic.** Falsified: production `SecretPlanner` already resolves canonical hidden candidates deterministically.
- **Clue generation needs a second hidden-location solver.** Rejected: route/clue planning consumes canonical `ResolvedSecretPlan` identity.
- **Route/readability/clue planning was missing.** Supported; implemented with stable IDs, semantic anchors, readability/diversity policy, diagnostics, and explicit bypass semantics.
- **Reusable interaction/discovery APIs are unavailable.** Falsified: `WorldObjectSceneRuntime` and canonical `SecretDiscoveryState` are available and integration regression run `33419056074` is green.
- **No production generated secret geometry exists.** Narrowly falsified: `CaveSecretPocketAuthoring` creates verified generated hidden-space/barrier topology and `CaveSecretPocketSecretCandidateProvider` projects that exact geometry into canonical WorldBuilder secret identity. Generated-cave bypass regression run `33420376990` is green.
- **Primitive validation/showcase composition can prove visual acceptance.** Falsified twice. Exact captures remained blockout/obstructed, and merged `AGENTS.md` explicitly forbids parallel primitive renderers for player-visible validation.
- **Production clue evidence can be layered onto verified cave topology without weakening it.** Supported: the dedicated validation path applies deterministic normal voxel coating to the retained false-wall volume and rechecks the barrier remains solid.

## Selected direction

Use the dedicated `Assets/Game/WorldBuilder/Validation/SecretDiscovery/` scene as the development and visual-proof surface. It must consume production voxel authoring, cave generation, cave-secret composition, material/coating IDs, the normal voxel renderer, and production vegetation/tree presentation. No `GameObject.CreatePrimitive`, helper mesh, one-off material/shader, fake vegetation, or gallery-local parallel renderer is acceptable.

The cave path is the supported production generated-secret topology; do not invent a separate architecture secret-geometry system merely to satisfy a screenshot. If an architectural pattern still lacks a supported generated realization, document that limitation and use another supported generated feature as allowed by the issue. Only after the dedicated scene is proven should the Worldbuilding Gallery receive a thin final acceptance consumer.

## Current CI discriminator

Run `33445911882` failed before any dedicated-scene screenshot because `CaveSecretPocketCluePresentation.cs` referenced nonexistent `Coatings.None`. This is a local compile defect in the new clue-presentation guard, not an acceptance symptom. The coating contract is a byte whose zero value means no coating, so the guard now rejects `boundaryCoating == 0` without adding a new module dependency.

## Remaining gates

1. Retry the exact current feature head through `ci-test/fixes/agent-5`; do not replace it while queued/running.
2. Inspect the dedicated built-player screenshots at full resolution and reject obscured, misframed, non-production, or visually ambiguous evidence.
3. Validate representative natural-route and mechanism-backed behavior at gameplay scale, or document the permitted unsupported architectural limitation and use another supported generated feature.
4. Add only the thin final `WorldbuildingGalleryShowcase` acceptance consumer, then replay that exact built scene.
5. Re-check blast radius/cost and every acceptance checkbox.
6. Only after all exact-SHA gates are green: close the SceneIssue, merge current master, revalidate if the SHA changes, and promote the exact feature head non-force.
