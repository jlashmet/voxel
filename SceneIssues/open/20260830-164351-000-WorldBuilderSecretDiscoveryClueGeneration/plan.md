# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, readability policy, explicit voxel-bypass policy, reusable interactable realization, canonical idempotent discovery credit, and representative built-player proof. This SceneIssue has no captures/marked regions.

## Hypotheses and discriminators

- **A: hidden-secret selection is missing/non-deterministic.** Falsified: production `SecretPlanner` already resolves authoritative secret candidates deterministically and fails closed.
- **B: clue generation needs a second hidden-location solver.** Rejected: route/clue planning must consume `ResolvedSecretPlan` and existing stable site/secret identities.
- **C: deterministic route/readability/clue planning is missing.** Supported and implemented on this branch with stable route/clue IDs, semantic anchors, clue policy, bypass semantics, diagnostics, and focused regressions.
- **D: canonical interactable/discovery integration is now available on master.** Rechecked 2026-08-31 against master `2ea5f5c95f89fbf0403dbefb50b782829583d304`; falsified. `20260830-014314-000-ExplorationInteractablesSecretsShowcase` remains open. Do not duplicate its runtime authority.
- **E: production generated secret voxel geometry exists for bypass validation.** Falsified by source tracing: `WorldBuilderVoxelCatalogue` does not consume resolved secret/route plans, so no production secret-route voxel provenance exists to scan honestly.

## Implemented / validated

Planning contracts, `SecretCluePlanner`, `SecretDiscoveryPlanner`, explicit bypass policies, a narrow non-authoritative discovery ledger seam, focused behavioral regressions, and a module-owned standalone validation scene are complete. Exact feature SHA `6f68a84ecbb5c2e081c3ab666a19a7903161a347` passed targeted run `33361608731`, including focused tests, repository-derived module validation, Kentridge integration, dedicated WorldBuilder standalone-player execution, screenshots, artifacts, and final status. Full-resolution captures showed readable supported materials after the validation-scene shader fix. Planner work is one-shot/event-driven and bounded.

## Remaining blockers / closure gates

1. Bind interactable-backed routes and discovery metadata to the canonical reusable runtime once the owning SceneIssue lands on master; prove one stable discovery identity and revisit/reload/repeated-activation idempotence.
2. Add/consume the production secret-geometry realization path, then validate `ProtectedShell` / `AuthoredBreakablesOnly` against actual generated voxels.
3. Compose architectural, ruin/chamber, and natural traversal examples into `WorldbuildingGalleryShowcase`; run exact built-player validation and inspect gameplay-scale evidence for clue readability, route legibility, accidental bypass, placeholder/sign language, and runtime exceptions.
4. Keep this issue open until every acceptance item is proven. Then close with `status=fixed`/`resolvedUtc`, merge current master, revalidate the resulting exact SHA as required, and push that exact head to master non-force.
