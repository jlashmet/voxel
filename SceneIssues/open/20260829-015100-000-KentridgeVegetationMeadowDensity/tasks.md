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
- [x] Root-cause shared wind delivery at queued `Graphics.DrawMesh`: mutable shared-material `_GrassTime` is not a safe per-submission snapshot; Unity requires `MaterialPropertyBlock` for queued per-draw state.
- [x] Fix shared `ProceduralGrassBatch.Draw()` to reuse one property block, snapshot unscaled presentation time into `_GrassTime`, and pass it directly to every packed draw. Preserve GPU deformation/batching; no Kentridge shader fork.
- [x] Add focused PlayMode regression through the actual `ProceduralGrassBatch.Draw()` path proving the submitted clock advances across frames while chunk/blade/vertex/triangle topology stays unchanged without rebuild.

## Regression / architecture checks
- [x] Prove Kentridge definition exposes grass-only dense regional policy and empty tree/ambient-animal allowlists.
- [x] Prove each required exclusion class can be authored independently.
- [x] Prove production-path deterministic meadow placement reaches `>=3000` renderer-equivalent blades in one connected field and only eligible samples produce grass.
- [x] Prove allowed-kind filtering, density behavior, and placement are deterministic.
- [x] Prove shared blade expansion is deterministic/bounded at 5–15 and is the renderer's exact contract.
- [x] Preserve packed chunk renderer; no legacy sprite, renderer-side global density magic, Kentridge-only scatter loop, per-blade GameObjects, or shader fork.
- [ ] Green exact-SHA focused PlayMode regression for advancing wind clock.
- [ ] Green exact-SHA Kentridge built-application scene harness/replay after wind repair.

## Blast radius / cost
- [x] WorldBuilder API remains additive; non-Kentridge callers retain defaults unless opting into ecology policy.
- [x] Density realization changes remain Kentridge composition + reusable policy/vegetation APIs.
- [x] Wind change affects shared packed Grass/Nettle presentation only.
- [x] Source-level wind cost: one persistent property block per grass batch, one unscaled-time read, one block clear, and one float write per draw; no per-frame managed allocation, material creation, mesh rebuild, or CPU blade animation.
- [x] Prior player baseline: 11,478 semantic grass instances / 114,580 blades total; primary connected meadow 5,777 / 57,589 blades; 8 chunks; zero excluded-surface leakage; ~110 FPS available runtime baseline.
- [ ] Record final post-fix runtime blade/chunk/leakage/FPS evidence and compare with baseline; explicitly document any unavailable CPU-ms/GPU-ms/memory/build-time dimensions rather than inventing values.

## Final exact-SHA validation / visual gate
- [x] Refresh current `origin/master` before final CI; current master `521ba9c1fc5531f299f09595316dff03af01df57` is the feature merge base (`behind_by=0`), so no merge commit is required.
- [x] Confirm feature-only diff has no unrelated capture/workflow and no `.github/test-request.json` change.
- [ ] Create exactly one fresh final request on `ci-test/fixes/agent-5`, based directly on exact feature SHA, targeting `ProceduralGrassWindTests.Draw_PublishesAdvancingWindClockWithoutRebuildingPackedGrass` plus this issue's 60-second replay.
- [ ] Leave queued/running CI untouched; inspect logs/artifact after completion. Retry only once if the failure is infrastructure, per workflow rules.
- [ ] Built player reaches usable `KentridgePlayableSlice` without startup/runtime exceptions.
- [ ] Normal gameplay/player-height replay plainly reads as dense procedural meadow, not sparse/tiled/floating grass.
- [ ] Durable diagnostic proves one connected meadow has `>=3000` rendered blades and zero excluded-surface placements.
- [ ] Inspect at least two late, time-separated stationary frames and prove visible blade silhouettes/poses change due to wind.
- [ ] Store concise durable final verification evidence beside the issue.

## Acceptance (issue.json)
- [x] (1) WorldBuilder exposes reusable per-area vegetation kinds/density controls plus ambient-animal policy hook.
- [x] (2) Kentridge uses that path, allows only procedural Grass, and enables no ambient-animal kinds.
- [ ] (3) Final built app proves a connected Kentridge meadow has `>=3000` blades and visually reads as full meadow.
- [ ] (4) Final built gameplay visibly animates grass while player/camera are stationary.
- [ ] (5) Final evidence confirms roads, structures/interiors, water, steep/invalid terrain, cultivated surfaces when semantically identified, and other exclusions are not carpeted.
- [x] (6) No legacy grass sprite, scene-local scatter, thousands of grass GameObjects, or Kentridge-specific shader fork introduced.
- [x] (7) Behavioral regressions cover WorldBuilder policy, allowlist, density, determinism, exclusions, empty animal allowlist, blade expansion, and shared wind submission.
- [ ] (8) Exact built-application Kentridge harness is green and usable.
- [ ] (9) Durable visual evidence satisfies mandatory meadow-density and animation checks.
- [ ] (10) Final blast-radius/cost evidence is measured/documented and acceptable.

## Metadata / promotion / publish
- [x] Commit ecology/density implementation and regressions on `fixes/agent-5`.
- [x] Commit shared wind fix and focused renderer regression on `fixes/agent-5`.
- [ ] After green exact-SHA focused CI + built replay + human visual gate, set pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) and move only this capture `open -> pending` in a bookkeeping commit.
- [ ] Complete every remaining checkbox/acceptance item and record verification evidence.
- [ ] Move only this capture `pending -> closed`, set `status=fixed` and `resolvedUtc`, and commit final bookkeeping.
- [ ] Fetch current `origin/master`, merge into `fixes/agent-5`; if master advances, fetch/merge/retry.
- [ ] Push exact feature head to `origin/master` non-force and verify `master == fixes/agent-5`.
