# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, readability policy, explicit voxel-bypass policy, reusable route/discovery integration, and representative built-player proof. This SceneIssue has no captures or marked regions, so standalone-player captures are the visual evidence source. The module-local player scene is the dedicated feature proof; `WorldbuildingGalleryShowcase` remains the required integration scene from the issue acceptance and is not a substitute for the dedicated proof.

## Hypotheses and discriminators

- **A: hidden-secret selection is missing/non-deterministic.** Falsified: production `SecretPlanner` resolves authoritative candidates deterministically and fails closed.
- **B: clue generation needs a second hidden-location solver.** Rejected: clue/route planning consumes canonical `ResolvedSecretPlan` identity.
- **C: deterministic route/readability/clue planning was missing.** Supported and implemented with stable IDs, semantic anchors, readability/diversity policy, bypass semantics, diagnostics, and focused regressions.
- **D: canonical discovery integration was unavailable.** Falsified on current master `2edf4c2e151492f67c4a1c1b846a9b7948284aba`: `SecretDiscoveryState` is the canonical candidate-keyed authority. `SecretDiscoveryLedger` now composes clue observation with that authority and regressions prove one first-discovery event across revisit/restore.
- **E: broad gallery captures can prove feature readability.** Falsified by exact built-player evidence: foliage/framing obscured the staged cues, and generic gallery composition did not make the clue chain self-evident. The module-local validation scene is therefore being corrected into a dedicated clue-focused environment rather than using colored debug markers or relying on gallery framing.

## Selected fix

Keep reusable planning/discovery semantics unchanged. Use the existing module-owned `WorldBuilderSecretDiscoveryValidation` scene as the feature-specific built-player proof and replace its marker tableau with a grounded three-stage environmental chain: displaced stones -> repeated weathered threshold/notches -> human-scale masonry seam at the canonical false-wall entrance. Validate canonical discovery directly in the scene without creating local discovery state. Keep the gallery composition only as the required integration consumer and later validate its exact built scene separately.

## Remaining gates

1. Run focused exact-SHA regression plus repository-derived module/Kentridge gates; inspect the dedicated scene full-resolution captures and classify visual quality.
2. If dedicated evidence is still below production quality, record the failed visual relationship before a second materially different visual fix.
3. Validate the exact built `WorldbuildingGalleryShowcase` integration scene without treating its screenshots as the dedicated feature scene.
4. Validate remaining interactable-route and representative-pattern acceptance against the now-landed reusable runtime; do not duplicate its behavior.
5. Check cost/blast radius. Do not close until every required acceptance item is proven; then close `open -> closed`, set `status=fixed`/`resolvedUtc`, merge current master, revalidate if the exact feature SHA changes, and push that exact head to master non-force.
