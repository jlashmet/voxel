# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Inspect assignment; `captures: []`, so `issue.json` is the acceptance contract.
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and real CharacterMotor traversal.
- [x] Production/storage regressions cover determinism, reachability, roads, settlements, blocking geography, explicit route solutions, bounded slopes, all 16 generic building shells, water realization, and two-stage presentation readiness.
- [x] Prove shared planner reuse with independent non-Kentridge `a`/`b` blocked-water fixture; no Kentridge policy in shared planner/catalogue APIs.
- [x] Feature-aware vertical residency/readiness publish authored upper Y-layers without widening horizontal interest or changing device/scheduler policy (`33346099006`, `33354287850`).
- [x] Experiments 019/020 prove Rossdam structures are authoritative/grounded and each authored bound has ready visible production geometry after settled readiness (`33359142139`, `33370392969`).
- [x] After two materially different failed camera corrections, isolate framing root cause; exact projected 3D envelope containment replaces the insufficient scalar/frustum-intersection models.
- [x] Replace demonstrated-cost solid fallback body with four bounded wall boxes; independent fixture proves hollow centre and <25% former body volume. Exact `33405010658` is regression/player green and reduces Rossdam publication to ~100k–120k indices per blockout.
- [x] Inspect `33405010658` full-resolution evidence: visual closure remains red at Moordell/Rossdam and Rossdam lake reads as a thin distant strip.
- [x] Ensure validation-only 90° lens executes after evidence survey pose; add end-frame Experiment 025 read-only camera/centre diagnostic.
- [x] Classify `33409727597` as product compile failure from missing `MountingForce.WorldGen.Voxel`; fix only that cause.
- [x] Run corrected Experiment 025 on exact feature `e1f734cbd9a6436fff5c7620d9557773b957d9b7`: request `1477dce4a1ecf01ce2e22b9953e650e4255fecbb`, run `33410426650` is green. Moordell/Rossdam capture at 90° with all four authored centres contained.
- [x] Inspect `33410426650` full-resolution Moordell/Rossdam frames: still closure-red. Pale regions follow terrain contours and multiple contained authored centres do not read as roofs; framing is rejected.

## Current exact gate
- [ ] Experiment 026: at each authored settlement building centre, pair existing production visible-surface coverage/source-step diagnostics with bounded authoritative `ShowcaseWorld.SurfaceQuery.TryFindTopSolid` top Y/material/delta-from-terrain observation. Validation-only; no camera/world/render/streaming mutation. Use the result to distinguish publication/readiness from renderer/LOD/presentation before any geometry/material change.
- [ ] Fix only the owner selected by Experiment 026 and add/retain a focused behavioral regression when the selected invariant is testable independently.
- [ ] Verify full-resolution Moordell/Rossdam/Fairy/Orc surveys show all four readable grounded authored blockouts with streets/open space and road arrival/exit.
- [ ] Re-check Rossdam lake framing: substantial authored water plus constrained route, not a thin distant strip.
- [ ] Capture Southern Ridge/pass and final `macro-network-overview` inside the unchanged 60 s replay.
- [ ] Re-run final exact-SHA targeted CI and prove focused regression + supported 60 s real-player smoke are closure-quality green for the same final feature SHA.

## Runtime / cost
- [x] Player-height road evidence and representative real CharacterMotor traversal are supported by prior exact artifacts; final exact run must retain both.
- [x] No unrelated SceneIssue implementation, feature-branch `.github/test-request.json`, custom CI transport/workflow, CharacterMotor/load-radius/device-budget change.
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries; shared feature scheduler remains unchanged.
- [x] Run `33405010658`: targeted test `0.007716s`, elapsed `69s`, final RSS `549MB`, peak RSS `5251MB`, no positive swap growth; Moordell ~t40, Rossdam ~t52, lake ~t58.
- [ ] Quantify actual additional vertical resident/generated region count against baseline; numerically prove no horizontal interest-radius/device-budget change.
- [ ] Measure final lake dimensions/depth/cells, route tile/solve/constrained counts, feature work/time, CPU/FPS, memory, streaming convergence, render/far-field telemetry against budgets.
- [ ] Quantify fixed-60-second target timing after visual closure.
- [ ] Re-fetch current master and re-check exact feature diff immediately before final targeted CI.

## Acceptance / closure
- [ ] (1) Source-backed macro graph remains authoritative through shared WorldBuilder APIs.
- [ ] (2) Every settlement has readable physical presence, including >=4 grounded generic blockouts where no richer generator owns it.
- [ ] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] (4) Roads are terrain-aware; blocked geography requires explicit semantic solutions and no silent lake/cliff/building crossing.
- [ ] (5) Reusable geographic authoring/query covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, and route/placement constraints.
- [ ] (6) Built world visibly contains a substantial lake + ridge and at least one geography-altered hard route.
- [ ] (7) Regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [ ] (8) No second scene-local graph/direct voxel-writing/static destination hierarchy.
- [ ] (9) Focused behavioral regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, bounded water cost, evidence sequencing, feature-aware vertical residency, two-stage readiness, settlement-survey containment, and generic blockout-shell cost.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal without runtime exceptions.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets.
- [ ] Every checkbox above is complete before closure.
- [ ] After green exact gates, move this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, merge current master, and non-force push that exact feature head to master; retry if master advances.
