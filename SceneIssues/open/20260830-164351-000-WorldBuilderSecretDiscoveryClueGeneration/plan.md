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
- **Production clue evidence can be layered onto verified cave topology without weakening it.** Supported: deterministic normal voxel coating retains solid false-wall occupancy.
- **The Gallery breakable audit camera can use the terminal-segment edge as a safe eye point.** Falsified by exact run `33508045854`: all automated gates passed, but the full-resolution breakable image still showed world underside/sky void. Experiment 011 refined the cause to the camera sitting at the far edge of the 18-voxel final segment rather than a reliably carved interior point.

## Selected direction

Use the dedicated `Assets/Game/WorldBuilder/Validation/SecretDiscovery/` scene as the development and production-path proof surface. It consumes production voxel authoring, cave generation, cave-secret composition, material/coating IDs, the normal voxel renderer, and production vegetation/tree presentation. No primitive or parallel visual stack is acceptable.

The final Gallery consumer stays thin. For the breakable audit, change only SceneIssue camera orchestration: move the helper edge position toward the authored barrier so the eye remains inside the final tunnel segment. Do not change cave topology, clue semantics, renderer APIs, or bypass policy to fix framing.

## Current CI discriminator

Feature head `596c94ffec14585d5768a762d3960591a440ea4f` contains the refined acceptance-camera correction and durable experiment evidence. Build one targeted request from this exact SHA on `ci-test/fixes/agent-5`, then leave that request alone while queued/running. The next discriminating evidence is the full-resolution `02-authored-breakable-boundary.png`; it must visibly show the retained clue-bearing false wall from a valid tunnel viewpoint.

## Remaining gates

1. Run the exact current feature head through `ci-test/fixes/agent-5`; do not replace it while queued/running.
2. Inspect dedicated module and Gallery full-resolution screenshots; reject obscured, misframed, non-production, or visually ambiguous evidence.
3. Validate representative natural-route and mechanism-backed behavior at gameplay scale, or document the permitted unsupported architectural limitation and use another supported generated feature.
4. Confirm the thin final `WorldbuildingGalleryShowcase` acceptance consumer proves readable pre-solve evidence without universal markers.
5. Re-check blast radius/cost and every acceptance checkbox.
6. Only after all exact-SHA gates are green: close the SceneIssue, merge current master, revalidate if the SHA changes, and promote the exact feature head non-force.
