# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Inspect assignment; `captures: []`, so `issue.json` is the acceptance contract.
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and real CharacterMotor traversal.
- [x] Production/storage regressions cover determinism, reachability, roads, settlements, blocking geography, explicit route solutions, bounded slopes, all 16 generic building shells, water realization, and two-stage presentation readiness.
- [x] Prove shared planner reuse with independent non-Kentridge `a`/`b` blocked-water fixture; no Kentridge policy in shared planner/catalogue APIs.

## Proven remediation / root cause
- [x] Reject broad residency, serial remote prestreaming, global scheduler preemption, skipped targets, widened load radius, and alternate evidence transports.
- [x] Keep validation streaming demand at semantic focus while survey camera moves independently; retain macro-driver automation ownership over later generic smoke-harness writes.
- [x] Add feature-aware vertical residency only for semantic feature Y-layers in already-demanded X/Z columns; exact run `33346099006` proves ordinary streaming publishes an upper authored layer without traversal forcing.
- [x] Correct two-stage presentation readiness to include the same semantic feature Y-layers; exact run `33354287850` proves the lower-ready/upper-missing race remains false until the upper shell publishes.
- [x] Experiment 019 exact run `33359142139`: all four Rossdam structures contain ~1.03–1.20M authoritative structure voxels, begin at grounded production placement Y, and retain authored wall-height span; generation/storage/grounding rejected.
- [x] Experiment 020 readiness-aligned exact run `33370392969`: all four Rossdam bounds intersect the real survey frustum and each intersects four ready visible production solid chunks at source step 2 after all target columns settle; generation/readiness/frustum intersection/coarse publication rejected.
- [x] Exact run `33372188117`: workflow green but vertical-only 45 m settlement-camera correction is closure-red because inherited 70 m rotation crops/mis-aims the survey.
- [x] Exact run `33373258471`: focused focus-ray-dolly regression product-red (`0.027976°` vs `<0.01°`) while built player succeeds; same one-readable-building/cropping symptom remains after a second materially different camera fix.
- [x] Experiment 021 minimal repro isolates framing containment before a third correction: production generic settlement footprint is 51.6 m x 48.4 m, ~35.36 m diagonal half-span; the exact scene 58° lens covers only ~34.37 m per side at the original ~62 m camera-to-focus vertical separation. Experiment 020's AABB test proved intersection, not full containment.
- [x] Exact run `33376804313` is workflow-green but visual-closure-red: the 72° flat-footprint regression passes while Moordell still clips the fourth blockout and Rossdam shows only one complete building plus a clipped structure.
- [x] Experiment 022 replaces the falsified scalar footprint model with exact 16:9 projection of the production-plan 3D building envelopes (foundation inset, terrain relief, wall and roof height) and widens only validation settlement surveys to 90° while preserving the established semantic camera/focus pose and production streaming/LOD policy.
- [x] Exact run `33383144783` proves the projected 90° containment regression and built-player startup, but is evidence-incomplete: Moordell still has one content-pending required building column at t59, so no macro settlement frame is emitted inside 60 s.
- [x] Experiment 023 isolates a demonstrated generic blockout publication cost: each fallback body was a fully solid brick volume despite acceptance requiring only a readable blockout. Replace it with four shared exterior wall boxes while preserving footprint, height, foundation, roof, placement bounds, materials, and collision silhouette; add an independent synthetic-dimension regression proving a hollow centre and <25% former body volume.
- [x] Experiment 024 isolates settlement-lens execution order and ensures the validation-only 90° lens executes after the evidence driver's survey pose.
- [x] Exact run `33405010658` on feature source `6037a817eea51eaa9f3c4ac45b7f1774c5310b7a` passes the independent generic blockout-shell regression and the unchanged real-player step. Rossdam publication reports `100113/119958/100074/118170` ready indices across its four blockouts versus experiment 019's ~1.03–1.20M solid bodies.
- [x] Inspect run `33405010658` full-resolution Moordell/Rossdam/lake frames. Visual closure is rejected: Moordell does not read as four clean complete blockouts; Rossdam shows only a subset clearly with the lower structure clipped; the lake-detour view remains a thin distant strip.
- [x] Add experiment 025 validation-only end-of-frame survey discriminator after the 90° lens; it records actual FOV/pose and all four authored building-centre viewport positions without mutating camera/world state.

## Current exact gate
- [x] Projected-authored-bounds containment regression is exact-source green on run `33383144783`.
- [x] Run the independent generic blockout-shell regression on the exact current feature SHA through `ci-test/fixes/agent-6` and inspect its unchanged 60 s built-player replay for convergence/visual evidence (`33405010658`, green).
- [x] After focused green, inspect full-resolution Moordell/Rossdam frames; do not accept workflow green without visual closure. Result: closure-red.
- [ ] Run experiment 025 on exact current feature SHA through `ci-test/fixes/agent-6`; inspect `MACROEVIDENCE end-frame-survey` for actual 90° FOV/pose and four-centre containment, then inspect the corresponding full-resolution settlement frames before any geometry change.
- [ ] Verify full-resolution Moordell/Rossdam/Fairy/Orc surveys show all four readable authored settlement blockouts with streets/open space and arrival/exit.
- [ ] Re-check Rossdam lake framing; it must read as substantial authored water plus constrained route, not the thin distant strip still visible in run `33405010658`.
- [ ] Capture Southern Ridge/pass and final `macro-network-overview` inside the unchanged 60 s replay.
- [ ] Re-run final exact-SHA targeted CI and prove focused regression + supported 60 s real-player smoke are closure-quality green for the same final feature SHA.

## Exact runtime / visual gate
- [ ] Exact built `KentridgePlayableSlice` reaches usable gameplay without startup/runtime exceptions and captures every required macro target inside 60 s.
- [ ] Full-resolution evidence shows at least four readable grounded blockouts at Moordell, Rossdam, Fairy Village, and Orc Village plus streets/open space and road arrival/exit.
- [ ] Full-resolution evidence shows continuous roads/network, substantial Rossdam lake, Southern Ridge/pass, visibly geography-constrained route behavior, differentiated countryside, and representative real CharacterMotor traversal.
- [x] Player-height road evidence and representative real CharacterMotor traversal are supported by the driver and prior exact artifacts; final exact run must retain both.

## Blast radius / cost
- [x] No unrelated SceneIssue implementation, no feature-branch `.github/test-request.json`, no custom CI transport/workflow, no CharacterMotor/load-radius/device-budget change.
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries.
- [x] Shared `FeatureRegionBuild` scheduling remains unchanged.
- [x] Feature-aware vertical residency cannot expand empty horizontal columns: it only adds semantic Y-layers after normal X/Z demand admission and shares a catalogue-hash cache with readiness.
- [x] Validation diagnostics/camera corrections are profile-only and do not drive generation, streaming, renderer admission, or normal gameplay policy.
- [x] Baseline run `33304172039`: assertions `0`, no swap growth, peak RSS `5,571,344 KB`, late FPS ~`197/222/232/168/137`, final `missingVisible=0 coverage=True`.
- [x] Run `33318399738`: elapsed `73s`, final RSS `598MB`, peak RSS `5640MB`, swap growth `0MB`; settled-target FPS typically ~150–370.
- [x] Exact run `33346099006`: focused residency green; combined peak ~`5824MB`, no swap growth, late FPS generally >100 and often 200–360.
- [x] Exact run `33373258471`: after startup, late player FPS is generally ~127–332 with p95 frame time mostly ~3–12 ms while remote settlement feature publication proceeds.
- [x] Exact run `33376804313`: late Moordell/Rossdam publication remains generally ~133–346 FPS with p95 frame time mostly ~3–13 ms; Rossdam becomes content-ready around t56 and capture-ready before t57.
- [x] Exact run `33383144783`: focused projected-containment test passes; player stays runtime-clean, but Moordell convergence exceeds 33 s after macro start and remains one required column pending at t59, demonstrating substantial target-publication variance.
- [x] Run `33405010658`: targeted test passes in `0.007716s`; CI reports `elapsed=69s`, final RSS `549MB`, peak RSS `5251MB`, no positive swap growth. Moordell becomes capture-ready around t40; Rossdam around t52; late FPS during Rossdam publication is generally ~300–375 before the lake phase and ~130–250 while the new lake target streams.
- [x] Quantify shell body reduction at Rossdam: ready publication indices are ~100k–120k per blockout, roughly an order of magnitude below experiment 019's ~1.03–1.20M solid structures while retaining four ready visible chunks per building.
- [ ] Quantify actual additional vertical resident/generated region count from feature-aware residency against baseline; prove no horizontal interest-radius/device-budget change numerically.
- [ ] Measure final lake dimensions/depth/primitive cells, route tile/solve/constrained counts, feature work/time, CPU/FPS, memory, streaming convergence, and render/far-field telemetry against budgets.
- [ ] Quantify fixed-60-second target phase timing after visual framing closes; run `33405010658` reaches Moordell around t40, Rossdam around t52, lake capture around t58, and only Fairy readiness by the end of the replay.
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
- [ ] Corrected final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Every checkbox above is complete before closure.
- [ ] After green exact gates, move this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, merge current master, and non-force push that exact feature head to master; retry if master advances.
