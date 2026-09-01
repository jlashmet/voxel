# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Inspect assignment; `captures: []`, so `issue.json` is the acceptance contract.
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and real CharacterMotor traversal.
- [x] Production/storage regressions cover determinism, reachability, roads, settlements, blocking geography, explicit route solutions, bounded slopes, all 16 generic building shells, water realization, and two-stage presentation readiness.
- [x] Prove physical-planner reuse with independent non-Kentridge `a`/`b` blocked-water fixture; no Kentridge policy in shared planner/catalogue APIs.
- [x] Feature-aware vertical residency/readiness publish authored upper Y-layers without widening horizontal interest or changing device/scheduler policy (`33346099006`, `33354287850`).
- [x] Experiments 019/020 prove Rossdam structures are planned/grounded and authored bounds have ready production geometry after settled readiness (`33359142139`, `33370392969`).
- [x] After two materially different failed camera corrections, isolate framing root cause; exact projected 3D envelope containment replaces insufficient scalar/frustum-intersection models.
- [x] Replace demonstrated-cost solid fallback body with four bounded wall boxes; independent fixture proves hollow centre and <25% former body volume. Exact `33405010658` is regression/player green and reduces Rossdam publication to ~100k–120k indices per blockout.
- [x] Ensure validation-only 90-degree lens executes after evidence survey pose; corrected Experiment 025 exact run `33410426650` proves all four Moordell/Rossdam authored centres are contained while visual closure remains red.
- [x] Experiment 026 exact run `33417092425`: all sampled Moordell/Rossdam building centres have stable production coverage but authoritative top solid equals procedural terrain (`delta=0`) at every centre.

## Spatial reservation integration
- [x] Integrate resolved `TopDownWorldPhysicalPlan` occupancy with Agent 7's shared `SpatialReservationSnapshot`: settlement envelopes, generic building footprint/clearance, resolved road segments, and settlement-arrival public-access handoffs. Geography/route solving remains owned by `TopDownWorldPhysicalPlanner` (`82482e000b2c7d5c441be4dfd16532b96de78fc2`).
- [x] Add independent non-Kentridge `alpha`/`beta` fixture proving shared reservations reject a resolved road through a generic building.
- [x] Validate exact source `7033ee755ae6bac7cc3c1cc3c83bb4ee2e7d5f5e` in run `33441865025`, job `99651749224`, artifact `9776935720`.

## Experiment 027 root-cause gate
- [x] Add generic low-volume `FEATUREGEN_TRACE` instrumentation in shared structure generation for candidate selection, evaluation accept/reject, primitive count, rasterization completion, and per-instance voxel-write delta.
- [x] Run Experiment 027 exact focused storage + 60-second player replay: run `33448157883` completed failure.
- [x] Isolate the repeated symptom before another fix: both focused test and player abort during hard-route physical planning because `BuildAround` cannot produce a dry detour around `southern-ridge`; feature generation is never reached, which also explains the sky/fog-only replay.
- [x] Add shared semantic relaxation policy with deterministic diagnostics; keep the generic route constraint contract strict by default.
- [x] Keep scene policy in composition: only the authored South Fighting Area -> Orc Village `SouthernRidge` GoAround constraint opts into `EndpointEscape`.
- [x] Add independent regression proving generic constraints default to `Strict`, explicit opt-in is possible, and Kentridge relaxes exactly one authored ridge-shoulder relationship.
- [ ] Re-run Experiment 027 exact-SHA focused production-storage acceptance + 60-second SceneIssue replay on the corrected feature head.
- [ ] Inspect focused result and standalone logs/screenshots; require planning to complete and no macro-realization exception before evaluating downstream settlement publication.

## Remaining acceptance
- [ ] Verify full-resolution Moordell/Rossdam/Fairy/Orc surveys show readable grounded authored blockouts with streets/open space and road arrival/exit.
- [ ] Re-check Rossdam lake framing: substantial authored water plus constrained route, not a thin distant strip.
- [ ] Capture Southern Ridge/pass and final `macro-network-overview` inside the supported replay.
- [ ] Re-run final exact-SHA targeted CI and prove focused regression + repository-derived module validation + supported real-player smoke are closure-quality green for the same final feature SHA.

## Runtime / cost
- [x] Player-height road evidence and representative real CharacterMotor traversal are supported by prior exact artifacts; final exact run must retain both.
- [x] No unrelated SceneIssue implementation, feature-branch `.github/test-request.json`, custom CI transport/workflow, CharacterMotor/load-radius/device-budget change.
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries; shared feature scheduler remains unchanged.
- [x] Run `33405010658`: targeted test `0.007716s`, elapsed `69s`, final RSS `549MB`, peak RSS `5251MB`, no positive swap growth; Moordell ~t40, Rossdam ~t52, lake ~t58.
- [ ] Quantify actual additional vertical resident/generated region count against baseline; numerically prove no horizontal interest-radius/device-budget change.
- [ ] Measure final lake dimensions/depth/cells, route tile/solve/constrained counts, feature work/time, CPU/FPS, memory, streaming convergence, render/far-field telemetry against budgets.
- [ ] Quantify fixed-replay target timing after visual closure.
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
- [ ] (9) Focused behavioral regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, shared spatial reservation integration, bounded water cost, evidence sequencing, feature-aware vertical residency, two-stage readiness, settlement-survey containment, generic blockout-shell cost, and the selected runtime publication defect.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal without runtime exceptions.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets.
- [ ] Every checkbox above is complete before closure.
- [ ] After green exact gates, move this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, merge current master, and non-force push that exact feature head to master; retry if master advances.
