# Plan — WorldBuilder Secret Discovery Clue Generation

## Evidence / hypothesis discrimination

- No captures or marked-region overlays are present in this SceneIssue directory, so there is no image-localized defect to inspect before code.
- Hypothesis A — the secret-location solver itself is missing or non-deterministic: **disproved by source inspection**. `Game.WorldBuilder.Runtime.SecretPlanner` already resolves required and policy secrets from authoritative `SecretCandidate`s, reserves physical candidates, validates entrance topology, uses stable seeded selection, and fails closed when required candidates cannot resolve.
- Hypothesis B — a second secret-location/planning mechanism is needed for clue generation: **rejected**. Clues should consume `SecretRef`, `SiteRef`, `ResolvedSecretPlan`, and existing site resolution rather than choosing hidden spaces independently.
- Hypothesis C — the missing foundation is a deterministic authored clue-plan layer tied to resolved secrets: **supported**. No clue-specific API/runtime/tests currently exist on master.
- Hypothesis D — built-player inspect/read/discover realization can immediately use the generic interactable system: **not yet established**. `20260830-014314-000-ExplorationInteractablesSecretsShowcase` remains open on current master and no stable `InteractableDescriptor` contract was found by repository search. Re-check direct source before treating this as a blocker.

## Implementation direction

1. Add semantic clue authoring contracts to `Game.WorldBuilder.Api`, reusing stable WorldBuilder identities rather than scene object references. The contract must express clue id, secret, stage/order, clue kind, semantic source site, optional target site, content key/text, required/optional semantics, and optional secret-scoped memory topic.
2. Extend campaign authoring/blueprint composition only as needed to carry clue specs; do not alter the existing secret topology solver.
3. Add `SecretCluePlanner` in `Game.WorldBuilder.Runtime`. It will resolve clue specs only after authoritative site and secret resolution, deterministically order/choose legal sources from world seed + secret id, fail closed for unresolved required clues, and emit inspectable resolution/debug data.
4. Keep progression/discovery state event-driven and serializable. If the generic interactable/discovery runtime prerequisite is not yet available, define only the narrow semantic/runtime state seam required for later binding and record the blocked built-player interaction acceptance rather than inventing scene-local interaction code.
5. Add behavioral regressions proving a required three-stage hidden-cache chain, a rumor/NPC variant, same-seed determinism, permitted seed variation when alternatives exist, hard failure for missing required sources, and initial undiscovered memory state followed by persistent discovery.
6. Use the repository's new module-local validation convention for any focused built-player harness. Do not use top-level showcase scenes as the focused acceptance scene.
7. Run exact targeted CI only through `ci-test/fixes/agent-5`; keep `.github/test-request.json` off this feature branch.
8. Inspect built-player output and screenshots once the interaction prerequisite and module validation path permit the actual clue -> inspect/read -> discovery sequence. Measure planning/state cost and confirm no per-frame world search.

## Closure gates

- All behavioral and deterministic regressions green at exact feature SHA.
- Required/optional source validation demonstrated.
- Built-player clue discovery flow exercised through production interaction/discovery boundaries, with screenshots inspected at full resolution.
- Blast radius/cost documented and bounded.
- Only then move this issue directly from `open` to `closed`, set `status=fixed` and `resolvedUtc`, merge current `origin/master`, and push that exact feature head to `origin/master` non-force.
