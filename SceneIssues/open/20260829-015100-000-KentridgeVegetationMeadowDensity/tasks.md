# Tasks — Kentridge vegetation meadow density

## Investigation / implementation
- [x] Fetch/resume `fixes/agent-5`; read `AGENTS.md` and canonical `SceneIssues/README.md` (`SceneIssues/feature-readme.md` is absent).
- [x] Create and maintain separate `plan.md` and `tasks.md` before implementation.
- [x] Inspect Kentridge WorldBuilder definition, runtime ecology sampling, vegetation placement, packed grass renderer/shader, captures, prior CI logs, and built-player artifacts.
- [x] Implement additive reusable per-area ecology policy: vegetation allowlist, density/coverage, deterministic seed/variation, meadow radius/sample spacing, route clearance/slope limits, explicit exclusion classes, and ambient-animal allowlist.
- [x] Configure Kentridge countryside/meadow ecology to allow only semantic `VegetationKind.Grass`; trees and ambient-animal kinds remain empty.
- [x] Route Kentridge placement through production terrain sampling and reusable exclusions for routes/paths, structures/interiors, water/wet, cultivated, steep/cliff, and other invalid surfaces.
- [x] Keep one deterministic connected primary meadow authored by regional policy; no scene-local scatter coordinates or grass GameObjects.
- [x] Share deterministic 5–15 blades-per-semantic-instance expansion between packed renderer, diagnostics, and regressions.
- [x] Report renderer-equivalent total/primary-meadow blade counts and concrete exclusion leakage/rejection diagnostics.
- [x] Fix playable WorldBuilder compatibility facade so authored `CountrysideEcology` reaches runtime.
- [x] Correct only this issue's replay camera metadata to current root `Kentridge Player Camera`; no scene serialization/shared harness changes.
- [x] Discriminate frozen-wind hypotheses with built evidence: shader is compiled/included; packed grass is resubmitted every frame; late fixed-camera grass pixels remain identical while sky pixels change.
- [x] First repair attempt: reuse one `MaterialPropertyBlock` in `ProceduralGrassBatch.Draw()`, snapshot unscaled presentation time into `_GrassTime`, and pass it directly to every packed draw; no per-frame allocation or mesh rebuild.
- [x] Add focused PlayMode regression through `ProceduralGrassBatch.Draw()` proving the submitted property-block clock advances while packed topology is unchanged.
- [x] Inspect final exact-SHA artifact `33244533044` and falsify the MPB-only repair: 39.9s→49.9s→59.9s grass/ground pixels are exactly identical while sky pixels change; shader wind frequencies do not alias at 10-second intervals.
- [x] Identify remaining production clock publication: `ProceduralVegetationMaterials.ApplyGrassState()` still writes `_GrassTime` from scaled `Time.time`, matching the fixed-camera freeze during paused dialogue/cutscene rendering.
- [ ] Repair the shared production material clock to use unscaled presentation time so ambient grass wind continues while gameplay time is paused.
- [ ] Add a focused regression that pauses `Time.timeScale`, advances real frames, republishes shared grass state, and proves the production material `_GrassTime` advances.

## Regression / architecture checks
- [x] Prove Kentridge definition exposes grass-only dense regional policy and empty tree/ambient-animal allowlists.
- [x] Prove each required exclusion class can be authored independently.
- [x] Prove production-path deterministic meadow placement reaches `>=3000` renderer-equivalent blades in one connected field and only eligible samples produce grass.
- [x] Prove allowed-kind filtering, density behavior, and placement are deterministic.
- [x] Prove shared blade expansion is deterministic/bounded at 5–15 and is the renderer's exact contract.
- [x] Preserve packed chunk renderer; no legacy sprite, renderer-side global density magic, Kentridge-only scatter loop, per-blade GameObjects, or shader fork.
- [x] Green exact-SHA focused PlayMode regression for the first MPB wind repair (`6c9219e90d68b939940adca1ca37be1f8961b31d`, run `33244533044`).
- [x] Green exact-SHA Kentridge built-application scene harness/replay for that SHA, but visual animation acceptance failed.
- [ ] Green exact-SHA focused regression for the corrected production material clock.
- [ ] Green exact-SHA Kentridge built-application replay proving the corrected clock visibly animates blades.

## Blast radius / cost
- [x] WorldBuilder API remains additive; non-Kentridge callers retain defaults unless opting into ecology policy.
- [x] Density realization changes remain Kentridge composition + reusable policy/vegetation APIs.
- [x] Wind change affects shared packed Grass/Nettle presentation only.
- [x] Source-level first-repair cost: one persistent property block per grass batch, one unscaled-time read, one block clear, and one float write per draw; no per-frame managed allocation, material creation, mesh rebuild, or CPU blade animation.
- [x] Prior player baseline: 11,478 semantic grass instances / 114,580 blades total; primary connected meadow 5,777 / 57,589 blades; 8 chunks; zero excluded-surface leakage; ~110 FPS available runtime baseline.
- [x] Final failed-visual run retained identical density/leakage totals: 11,478 semantic instances / 114,580 blades total; primary meadow 5,777 / 57,589 blades; 8 chunks; excluded-surface-grass=0. Runtime log stabilizes near 60–72 FPS before the paused-dialogue phase and ~550–700 FPS afterward; exact CPU-ms/GPU-ms/memory/build-time dimensions are not emitted by this harness and remain unavailable rather than inferred.
- [ ] Re-evaluate final corrected-clock runtime cost after exact-SHA built validation; expected delta is only replacing scaled with unscaled clock publication, with no new topology or allocations.

## Final exact-SHA validation / visual gate
- [x] Refresh current `origin/master` before final CI; master `521ba9c1fc5531f299f09595316dff03af01df57` was the feature merge base (`behind_by=0`) at request time.
- [x] Confirm feature-only diff has no unrelated capture/workflow and no `.github/test-request.json` change.
- [x] Create exactly one final request on `ci-test/fixes/agent-5`, based directly on exact feature SHA, targeting `ProceduralGrassWindTests.Draw_PublishesAdvancingWindClockWithoutRebuildingPackedGrass` plus this issue's 60-second replay.
- [x] Leave queued/running CI untouched; final run `33244533044` completed green and its artifact was inspected.
- [x] Built player reaches usable `KentridgePlayableSlice` without startup/runtime exceptions attributable to this feature.
- [x] Normal gameplay/player-height replay reads as dense procedural meadow rather than sparse/floating grass.
- [x] Durable diagnostic proves one connected meadow has `57,589` rendered blades (`>=3000`) and zero excluded-surface placements.
- [ ] Inspect at least two late, time-separated stationary frames and prove visible blade silhouettes/poses change due to wind. Final run instead proves no grass-region pixel change at 39.9/49.9/59.9s.
- [ ] Store final passing verification evidence beside the issue. The current failed visual experiment must remain diagnostic only.
- [ ] Obtain an authorized exact-SHA validation for the corrected production-clock source state. Current instruction forbids an extra CI transport, so do not update `ci-test/fixes/agent-5` again without changed coordinator instruction.

## Acceptance (issue.json)
- [x] (1) WorldBuilder exposes reusable per-area vegetation kinds/density controls plus ambient-animal policy hook.
- [x] (2) Kentridge uses that path, allows only procedural Grass, and enables no ambient-animal kinds.
- [x] (3) Built app proves a connected Kentridge meadow has `>=3000` blades and visually reads as full meadow.
- [ ] (4) Final built gameplay visibly animates grass while player/camera are stationary.
- [x] (5) Built diagnostics confirm roads, structures/interiors, water, steep/invalid terrain, cultivated surfaces when semantically identified, and other exclusions are not carpeted.
- [x] (6) No legacy grass sprite, scene-local scatter, thousands of grass GameObjects, or Kentridge-specific shader fork introduced.
- [x] (7) Behavioral regressions cover WorldBuilder policy, allowlist, density, determinism, exclusions, empty animal allowlist, blade expansion, and shared wind submission; corrected material-clock regression remains pending.
- [x] (8) Exact built-application Kentridge harness is green and usable for the tested SHA.
- [ ] (9) Durable visual evidence satisfies mandatory meadow-density and animation checks.
- [ ] (10) Final blast-radius/cost evidence for the corrected clock is measured/documented and acceptable.

## Metadata / promotion / publish
- [x] Commit ecology/density implementation and regressions on `fixes/agent-5`.
- [x] Commit first shared wind fix and focused renderer regression on `fixes/agent-5`.
- [ ] After green exact-SHA focused CI + built replay + human visual gate, set pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) and move only this capture `open -> pending` in a bookkeeping commit.
- [ ] Complete every remaining checkbox/acceptance item and record passing verification evidence.
- [ ] Move only this capture `pending -> closed`, set `status=fixed` and `resolvedUtc`, and commit final bookkeeping.
- [ ] Fetch current `origin/master`, merge into `fixes/agent-5`; if master advances, fetch/merge/retry.
- [ ] Push exact feature head to `origin/master` non-force and verify `master == fixes/agent-5`.
