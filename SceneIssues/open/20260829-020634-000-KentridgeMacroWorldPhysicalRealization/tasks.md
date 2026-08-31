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
- [x] Root-cause-driven correction keeps the established 70 m semantic pose and production LOD/streaming unchanged, widens only validation settlement surveys to 72°, restores the normal lens outside settlement surveys, and adds focused containment regression.

## Current exact gate
- [ ] Exact run `33376804313` / wrapper `72639b4b36559aed26af056ea6b09706fe37cc01` for feature code source `5ca4d29efc8c538806f078e3393aa941526a5ede`: do not replace while queued/running. At last check job `99440020485` was queued with no runner and no steps started.
- [ ] After completion, inspect focused result plus full-resolution Moordell/Rossdam frames; do not accept workflow green without visual closure.
- [ ] Verify full-resolution Moordell/Rossdam/Fairy/Orc surveys show all four readable authored settlement blockouts with streets/open space and arrival/exit.
- [ ] Re-check Rossdam lake framing; it must read as substantial authored water plus constrained route, not the thin distant strip seen in earlier artifacts.
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
- [ ] Quantify actual additional vertical resident/generated region count from feature-aware residency against baseline; prove no horizontal interest-radius/device-budget change numerically.
- [ ] Measure final lake dimensions/depth/primitive cells, route tile/solve/constrained counts, feature work/time, CPU/FPS, memory, streaming convergence, and render/far-field telemetry against budgets.
- [ ] Quantify fixed-60-second target phase timing after visual framing closes; current completed run reaches Moordell content/capture around t42 and begins Rossdam around t44, so later evidence currently misses cutoff.
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
- [ ] (9) Focused behavioral regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, bounded water cost, evidence sequencing, feature-aware vertical residency, two-stage readiness, and settlement-survey containment.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal without runtime exceptions.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets.
- [ ] Corrected final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Every checkbox above is complete before closure.
- [ ] After green exact gates, move this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, merge current master, and non-force push that exact feature head to master; retry if master advances.
