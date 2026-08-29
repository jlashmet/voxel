# Tasks — Kentridge vegetation meadow density

## Investigation
- [x] Fetch current master and resume `fixes/agent-5` from it.
- [x] Read `AGENTS.md`, canonical SceneIssue workflow, and assigned issue; `captures: []` so there are zero marked poses.
- [x] Reuse existing `plan.md`; maintain this separate `tasks.md`.
- [ ] Inspect Kentridge WorldBuilder vegetation authoring, procedural grass renderer/shader wind path, tests, and budgets.
- [ ] Record competing hypotheses/discriminator/results in `plan.md`; add discovered work here.

## Implementation
- [ ] Add/generalize reusable per-area ecology policy: vegetation allowlist, density/coverage, deterministic variation, exclusions/clearance, ambient-animal policy hook.
- [ ] Configure Kentridge meadow/countryside to allow only canonical procedural grass and no ambient animals.
- [ ] Produce one contiguous meadow with >=3,000 procedural blades through WorldBuilder, not scene-local scatter/GameObjects.
- [ ] Respect roads, structures/interiors, water, cliffs/steep-invalid terrain and other exclusions.
- [ ] Fix shared grass wind only if production evidence proves it is broken; no Kentridge shader fork.

## Regression / cost
- [ ] Add focused production-path regression for policy filtering, density, determinism, exclusions, empty animals, and meadow blade count.
- [ ] Measure CPU/GPU/memory/world-build/build-time blast radius against existing vegetation/device budgets; do not weaken budgets.
- [ ] Review final diff for scope; `.github/test-request.json` must not be on the feature branch.

## Built-player visual gate
- [ ] Run one final targeted CI request on exact feature SHA using only `ci-test/fixes/agent-5`.
- [ ] Built Kentridge reaches a usable rendered scene without startup/runtime exceptions.
- [ ] Capture dense gameplay approach view and close player-height procedural-blade view.
- [ ] Record diagnostic proving >=3,000 blades belong to one meadow.
- [ ] Capture >=2 time-separated frames from the same stationary view proving visible wind motion.
- [ ] Store concise durable verification evidence beside the feature.

## Acceptance
- [ ] (1) Reusable per-area vegetation allowlist/density plus ambient-animal hook.
- [ ] (2) Kentridge uses top-level policy; only procedural grass; animals empty.
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
