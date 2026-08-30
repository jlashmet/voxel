# Tasks

- [x] Inventory current targeted CI, standalone-player capture, special-case scene/test mappings, and validation docs.
- [x] Define deterministic module ownership metadata: production paths, focused tests, optional validation scene, and separate scenario.
- [x] Implement diff-driven validation planning with conservative shared/core expansion and hard failure for unresolved required production ownership.
- [x] Make targeted CI automatically execute discovered focused tests, player-visible module validation targets, and `KentridgePlayableSlice` for production diffs.
- [x] Ensure required zero-match/skipped focused tests, module scenes, captures, or Kentridge integration fail validation.
- [x] Refactor the standalone-player harness so module validation, Kentridge, and SceneIssue replay share one generic mechanism without arbitrary test-name/scene-name feature inference.
- [x] Migrate Water to a module-local deterministic validation scene/scenario using production Water implementation.
- [x] Add an independent non-Water metadata/planner fixture to prove the discovery contract is reusable.
- [x] Keep existing useful focused regressions while removing PlayMode-only visual-acceptance semantics.
- [x] Update `AGENTS.md`, `SceneIssues/README.md`, `SceneIssues/feature-readme.md`, CI semantics, and validation documentation to describe the module-author workflow and hide harness internals.
- [x] Add focused automated regression coverage for module discovery, shared/core fallback, scene/scenario separation, and mandatory/fail-closed gate execution.
- [ ] Validate an ordinary Water production diff automatically selects focused Water tests -> built-player Water scene -> built-player `KentridgePlayableSlice`; record runtime/cost.
- [ ] Review final diff against all 18 acceptance criteria and complete required SceneIssue metadata/closure only after green exact-SHA CI.
