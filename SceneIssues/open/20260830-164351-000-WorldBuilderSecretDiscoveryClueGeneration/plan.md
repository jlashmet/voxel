# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, readability policy, explicit voxel-bypass policy, reusable route/discovery integration, and representative built-player proof. This SceneIssue has no captures or marked regions, so its exact built-scene replay is the visual evidence source.

## Hypotheses and discriminators

- **A: hidden-secret selection is missing/non-deterministic.** Falsified: production `SecretPlanner` already resolves authoritative candidates deterministically and fails closed.
- **B: clue generation needs a second hidden-location solver.** Rejected: clue/route planning consumes canonical `ResolvedSecretPlan` identity.
- **C: deterministic route/readability/clue planning was missing.** Supported and implemented with stable IDs, semantic anchors, readability/diversity policy, bypass semantics, diagnostics, and focused regressions.
- **D: canonical interaction/discovery integration is unavailable.** Falsified after master advanced to `2edf4c2e151492f67c4a1c1b846a9b7948284aba`; the reusable discovery authority is now available and the regression proves one discovery event across revisit/reload.
- **E: the validated gallery lacked representative clue realization.** Supported: planner/module validation existed, but `WorldbuildingGalleryShowcase` did not compose clue presentation against its real generated content. A scene-local presentation-only cave chain now binds deterministic semantic clues to existing tour landmarks and one canonical cave candidate/entrance.

## Repeated-failure root cause isolation

Two materially different CI attempts failed before behavior execution. Artifact inspection isolated the causes before another fix: (1) the new scene composition called internal `WorldBlueprintBuilder.RequireSite`, producing four `CS1061` errors; public production composition must use `Region(...).Site(...)`; (2) the SceneIssue replay harness requires root `scenePath`, while this record previously only had nested `scene.path`. Both causes are now corrected without changing acceptance.

## Implemented / validated so far

Planning contracts, `SecretCluePlanner`, `SecretDiscoveryPlanner`, explicit bypass policies, canonical discovery ledger integration, focused behavioral regressions, and module-owned standalone validation are present. Prior exact-SHA runs proved deterministic planner behavior and the WorldBuilder module player scene. Gallery clue realization is presentation-only: no interaction/discovery authority or save state was added; clues target the canonical cave identity and observation alone cannot discover it.

## Remaining gates

1. Run focused exact-SHA regression plus repository-derived module/integration gates.
2. Replay this SceneIssue through the standalone player using exact `WorldbuildingGalleryShowcase`; require the gallery clue ready log and no runtime exceptions.
3. Inspect full-resolution built-player evidence for gameplay-scale readability, grounding/material separation, absence of glowing/sign-marker language, and obvious accidental bypass/route confusion; record cost/blast radius.
4. Do not close until every required acceptance item is proven. After green gates, close `open -> closed`, set `status=fixed`/`resolvedUtc`, merge current master, revalidate if the exact feature SHA changes, and push that exact head to master non-force.
