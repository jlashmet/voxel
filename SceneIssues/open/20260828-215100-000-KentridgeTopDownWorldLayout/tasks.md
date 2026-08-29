# Tasks — Kentridge top-down world layout

## Required feature work
- [x] Inspect the assigned feature and confirm `captures: []` / zero marked regions.
- [x] Audit original/imported world evidence and separate verified transitions from inferred placement guidance.
- [x] Add reusable WorldBuilder macro node/route/layout contracts with reserved envelopes and provenance.
- [x] Add deterministic layout planning with contradiction, overlap, and unreachable-hard-route rejection.
- [x] Define the Mounting Force macro graph around Kentridge from source-backed evidence.
- [x] Realize Kentridge exits, routes, and neutral destination reservations through shared voxel WorldBuilder output.
- [x] Keep town art/style out of scope and preserve existing Kentridge/Hightown authored settlement generation.
- [x] Add a production-path regression covering deterministic placement, verified reachability, physical Kentridge exit, Hightown anchor alignment, and severed-hard-route failure.
- [x] Check static blast radius/cost: 21 nodes, 20 routes, 803 route tiles, 21 markers, 41 definitions; one-shot selection; no device-budget changes.

## Discovered runtime-evidence work
- [x] Initial exact-SHA regression and real-player build/run completed green on source `8b6ea69c` (run `33223023746`).
- [x] Inspect the actual run artifact rather than treating workflow green as sufficient.
- [x] Add a reusable capture-less Kentridge SceneIssue harness profile so validation continues beyond loading and records post-load evidence without changing recorded-pose replays.
- [ ] Capture a post-load real-player frame showing the production top-down macro-layout overlay.
- [ ] Capture normal eye-level scripted traversal using the real `CharacterMotor`, collision, streaming, and generated route/corridor output.
- [ ] Confirm post-load geography is usable/coherent and no startup/runtime exception occurs.
- [ ] Record durable verification evidence beside the assigned feature.

## Acceptance audit
- [x] (1) Kentridge selects a reusable Mounting Force macro-world definition through production WorldBuilder.
- [x] (2) Major destinations/intermediary regions and hard/soft provenance are represented.
- [x] (3) Verified traversal/reachability is enforced by planner and regression.
- [x] (4) Kentridge exits resolve into generated shared-system corridors rather than dead ends.
- [x] (5) Soft placement guidance is subordinate to verified topology.
- [x] (6) Town styling/material/architecture work remains out of scope.
- [x] (7) Fixed-seed layout/routing is deterministic and reusable; scene code selects intent.
- [x] (8) Production regression covers graph, placement, connectivity, physical realization, and hard-route failure.
- [ ] (9) Built-player top-down plus normal playable-traversal evidence is inspectable and compared to source topology.
- [ ] (10) Exact built `KentridgePlayableSlice` evidence shows a usable rendered post-load scene and inspectable surrounding routes.
- [x] (11) Blast radius/cost reviewed without weakening budgets.

## Closure
- [ ] Every checkbox and acceptance criterion above is complete.
- [ ] Final exact-SHA focused CI and built-player evidence are green for the final source state.
- [ ] Complete pending metadata and move only this feature `open -> pending`.
- [ ] Move only this feature `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Fetch current `origin/master`, merge it into `fixes/agent-5`, and stop on any unrelated conflict.
- [ ] Push exact feature head, then the same head to `origin/master` non-force; retry if master advances.
- [ ] Verify `master == fixes/agent-5` and the feature exists only under `SceneIssues/closed`.
