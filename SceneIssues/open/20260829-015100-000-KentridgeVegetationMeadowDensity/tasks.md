# Tasks — Kentridge vegetation meadow density

## Investigation
- [x] Fetch current master and resume `fixes/agent-5` from it.
- [x] Read `AGENTS.md`, canonical SceneIssue workflow, and assigned issue; `captures: []` so there are zero marked poses.
- [x] Reuse existing `plan.md`; maintain this separate `tasks.md`.
- [x] Inspect current Kentridge runtime vegetation authoring, procedural grass renderer, tests, and budgets; authored builder path moved to `Assets/Game/Composition/Kentridge/Playable/SceneRuntime/KentridgeRegionLife.cs`.
- [x] Record competing hypotheses/discriminator/results in `plan.md`; add discovered work here.
- [x] Confirm `ProceduralGrassBatch` already supports >3,000 instances by 1,023-instance draw batching; density failure is upstream placement/policy, not renderer capacity.
- [ ] Confirm the existing shared grass shader/material wind path and its built-player diagnostic/evidence mechanism before deciding whether production wind code needs modification.

## Implementation
- [ ] Add/generalize reusable per-area ecology policy: vegetation allowlist, density/coverage, deterministic variation, exclusions/clearance, ambient-animal policy hook.
- [ ] Add a backward-compatible vegetation-placement allowed-kind mask so a top-level area policy can constrain generated kinds without Kentridge-only scatter logic.
- [ ] Configure Kentridge meadow/countryside to allow only canonical procedural grass, no trees, and no ambient animals.
- [ ] Produce one contiguous meadow with >=3,000 procedural blades through WorldBuilder/runtime realization, not scene-local scatter/GameObjects.
- [ ] Compute the primary meadow as the largest 4-neighbor connected eligible sampling component and count generated grass within that component; do not treat an entire road side as contiguous.
- [ ] Respect road/bridge route clearance, built structures/interiors, riverbank/water, cliffs/steep-invalid terrain and other exclusions.
- [ ] Add runtime diagnostics that attribute grass count to the Kentridge meadow and make the acceptance count inspectable.
- [ ] Fix shared grass wind only if production evidence proves it is broken; no Kentridge shader fork.

## Regression / cost
- [ ] Add focused production-path regression for policy filtering, default-mask compatibility, density, determinism, exclusions, empty trees/animals, connected-component meadow attribution, and meadow blade count.
- [ ] Measure CPU/GPU/memory/world-build/build-time blast radius against existing vegetation/device budgets; do not weaken budgets.
- [ ] Review final diff for scope; `.github/test-request.json` must not be on the feature branch and no `.unity` serialization changes are allowed.

## Workflow validation / artifacts
- [ ] Run `python3 scripts/validate_module_scenes.py`.
- [ ] Run `python3 scripts/validate_no_prefab_lighting.py`.
- [ ] Refresh `validation-hashes.json` and `validation-report.md`; add `changed-files.txt`, `sample-transform-diffs.json`, and `validation-summary.md`.
- [ ] Run `python3 scripts/test_tree_mutation_model_load.py` and required structural review if applicable.
- [ ] Run snapshots for the targeted module list and inspect `layout-diff.json`.
- [ ] Run `python3 scripts/run_scene_semantic_baseline.py --baseline current`.
- [ ] Run editmode-behavior for the targeted module list.
- [ ] Complete `BlastRadiusReport.md`, mark planning blast-radius review, and re-check the approved `$2` scope.

## Built-player visual gate
- [ ] Run one final targeted CI request on exact feature SHA using only `ci-test/fixes/agent-5`.
- [ ] Built Kentridge reaches a usable rendered scene without startup/runtime exceptions.
- [ ] Capture dense gameplay approach view and close player-height procedural-blade view.
- [ ] Record diagnostic proving >=3,000 blades belong to one meadow.
- [ ] Capture >=2 time-separated frames from the same stationary view proving visible wind motion.
- [ ] Store concise durable verification evidence beside the feature.
- [ ] After green exact-SHA CI, run workflow-stability and strict lifecycle/hash gates and inspect cold/warm workflow artifacts.

## Acceptance
- [ ] (1) Reusable per-area vegetation allowlist/density plus ambient-animal hook.
- [ ] (2) Kentridge uses top-level policy; only procedural grass; trees and animals empty.
- [ ] (3) One Kentridge meadow has >=3,000 blades and visually reads full.
- [ ] (4) Grass visibly animates in normal built gameplay while stationary.
- [ ] (5) Invalid/excluded surfaces remain clear.
- [ ] (6) No legacy sprite, scene-local scatter, grass GameObject flood, or shader fork.
- [ ] (7) Focused production regressions cover required policy behavior.
- [ ] (8) Exact built Kentridge harness is usable and exception-free.
- [ ] (9) Durable human-inspectable density + animation evidence passes.
- [ ] (10) Blast radius/cost measured before closure.

## Promotion / publish
- [ ] Complete pending metadata and move only this feature open -> pending after all gates.
- [ ] Move pending -> closed, set fixed/resolvedUtc.
- [ ] Merge current master into feature, push feature, then push exact head to master non-force; retry if master advances.
- [ ] Verify every checkbox complete, `master == fixes/agent-5`, and assignment exists only under closed.
