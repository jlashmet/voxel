# Tasks — Kentridge vegetation meadow density

## Investigation / implementation
- [x] Fetch/resume `fixes/agent-5`; read `AGENTS.md` and canonical `SceneIssues/README.md` (`SceneIssues/feature-readme.md` is absent).
- [x] Create and maintain separate `plan.md` and `tasks.md` before implementation.
- [x] Inspect Kentridge WorldBuilder definition, runtime ecology sampling, vegetation placement, packed grass renderer/shader, captures, CI logs, and built-player artifacts.
- [x] Implement reusable per-area ecology policy: vegetation allowlist, density/coverage, deterministic seed/variation, region parameters, exclusion classes, and ambient-animal allowlist.
- [x] Configure Kentridge countryside/meadow ecology to allow only semantic `VegetationKind.Grass`; tree and ambient-animal allowlists remain empty.
- [x] Route Kentridge placement through production terrain sampling and reusable exclusions for routes/paths, structures/interiors, water/wet, cultivated, steep/cliff, and other invalid surfaces.
- [x] Keep deterministic connected meadow placement authored by regional policy; no scene-local scatter coordinates or grass GameObjects.
- [x] Share deterministic 5–15 blades-per-semantic-instance expansion between packed renderer, diagnostics, and regressions.
- [x] Report renderer-equivalent total/primary-meadow blade counts and exclusion leakage/rejection diagnostics.
- [x] Fix playable WorldBuilder compatibility facade so authored `CountrysideEcology` reaches runtime.
- [x] Correct only this issue's replay camera metadata to current root `Kentridge Player Camera`; no scene serialization/shared harness changes.
- [x] Experiment 001: custom per-draw `_GrassTime` MPB advanced in test but built grass remained byte-identical; falsified by run `33244533044`.
- [x] Experiment 002: shared material `_GrassTime` changed to unscaled time and focused test passed, but built grass still remained byte-identical; falsified by run `33246401704`.
- [x] Discriminate clock boundary: built frames show moving clouds from engine-managed `_Time.y`, proving GPU time advances.
- [x] Experiment 003: switch existing grass wind formula to engine-managed `_Time.y`; remove dead custom `_GrassTime` shader/material/MPB CPU state.
- [x] Clean focused wind regression to exercise production packed draw across frames, require no custom `_GrassTime`, and prove topology is unchanged/no CPU rebuild.
- [x] Discriminate geometry visibility before final CI: `ShowcaseWorld.MaterialAt(y, surface)` proves `surface` is the topmost occupied voxel; above-ground construction uses `ground + 1`, while Kentridge vegetation currently roots at `surface * VoxelSize`.
- [ ] Experiment 004: correct Kentridge ecology grounding to the exposed voxel face `(surface + 1) * VoxelSize` without changing X/Z, density, exclusions, normal sampling, or renderer.
- [ ] Add production-scene regression proving generated semantic grass roots equal the world's exposed top face and are not buried.

## Regression / architecture checks
- [x] Prove Kentridge definition exposes grass-only dense regional policy and empty tree/ambient-animal allowlists.
- [x] Prove each required exclusion class can be authored independently.
- [x] Prove production-path deterministic meadow placement reaches `>=3000` renderer-equivalent blades in one connected field and only eligible samples produce grass.
- [x] Prove allowed-kind filtering, density behavior, and placement are deterministic.
- [x] Prove shared blade expansion is deterministic/bounded at 5–15 and is the renderer's exact contract.
- [x] Preserve packed chunk renderer; no legacy sprite, renderer-side global density magic, Kentridge-only scatter loop, per-blade GameObjects, or shader fork.
- [ ] Green exact-SHA focused production-scene regression for exposed-face grass roots plus packed GPU-only wind topology.
- [ ] Green exact-SHA Kentridge built-application replay proving visible blade motion.

## Blast radius / cost
- [x] WorldBuilder API remains additive; non-Kentridge callers retain defaults unless opting into ecology policy.
- [x] Density realization changes remain Kentridge composition + reusable policy/vegetation APIs.
- [x] Wind changes affect shared packed Grass/Nettle presentation only.
- [x] Prior player density baseline: 11,478 semantic grass instances / 114,580 blades total; primary connected meadow 5,777 / 57,589 blades; 8 chunks; zero excluded-surface leakage.
- [x] Run `33246401704` retained identical density/leakage totals and reached ~68–74 FPS after startup before later held-scene phases accelerated into hundreds of FPS. Exact CPU-ms/GPU-ms/memory/build-time dimensions are not emitted by this harness and remain unavailable rather than inferred.
- [x] Engine-clock correction lowers CPU state cost: removes per-frame grass time material write, MPB clear/float write, persistent MPB, delegate, and time-source read; no new topology, draw, allocation, material, or CPU blade animation.
- [x] Exposed-face grounding cost is one integer increment per Kentridge ground sample; no added allocation, draw, mesh, object, or per-frame blade work.
- [ ] Re-evaluate final runtime/density/leakage/visual-contact evidence after exact-SHA built validation.

## Final exact-SHA validation / visual gate
- [x] Request `b598b19c88503ce9d59011f196dc404934bbef36` / run `33246401704` completed green but failed mandatory animation inspection; product-failure evidence only.
- [x] Human inspection + pixel comparison: grass/ground crop has exactly zero changed pixels at 39.3→49.3 and 49.3→59.3 seconds while sky changes.
- [x] Confirm the failed capture lacks readable moving blade silhouettes despite packed blade counts; grounding contract explains invisible/buried geometry.
- [ ] Refresh current `origin/master` before final exposed-face CI and confirm feature-only diff has no unrelated capture/workflow or feature `.github/test-request.json` change.
- [ ] Submit final exact-SHA request on the same assigned `ci-test/fixes/agent-5` mailbox only after confirming no queued/running request; do not create another transport.
- [ ] Leave queued/running CI untouched; inspect logs/artifact after completion. Retry only once for infrastructure failure.
- [ ] Built player reaches usable `KentridgePlayableSlice` without startup/runtime exceptions.
- [ ] Gameplay/player-height replay plainly reads as dense procedural meadow made of visible blades, not sparse/tiled/floating/buried grass.
- [ ] Diagnostic proves one connected meadow has `>=3000` rendered blades and zero excluded-surface placements.
- [ ] At least two late stationary frames visibly change blade silhouettes/poses due to wind.
- [ ] Store concise durable final verification evidence beside the issue.

## Acceptance (issue.json)
- [x] (1) WorldBuilder exposes reusable per-area vegetation kinds/density controls plus ambient-animal policy hook.
- [x] (2) Kentridge uses that path, allows only procedural Grass, and enables no ambient-animal kinds.
- [ ] (3) Final built app proves a connected Kentridge meadow has `>=3000` visible blades and visually reads as full meadow.
- [ ] (4) Final built gameplay visibly animates grass while player/camera are stationary.
- [ ] (5) Final evidence confirms roads, structures/interiors, water, steep/invalid terrain, cultivated surfaces when semantically identified, and other exclusions are not carpeted.
- [x] (6) No legacy grass sprite, scene-local scatter, thousands of grass GameObjects, or Kentridge-specific shader fork introduced.
- [x] (7) Behavioral regressions cover WorldBuilder policy, allowlist, density, determinism, exclusions, empty animal allowlist, blade expansion, and packed GPU-only wind topology; exposed-face grounding regression remains pending.
- [ ] (8) Exact built-application Kentridge harness is green and usable for final source.
- [ ] (9) Durable visual evidence satisfies mandatory meadow-density and animation checks.
- [ ] (10) Final blast-radius/cost evidence is measured/documented and acceptable.

## Metadata / promotion / publish
- [x] Commit ecology/density implementation and regressions on `fixes/agent-5`.
- [x] Commit engine-managed wind implementation and focused regression cleanup on `fixes/agent-5`.
- [ ] Commit exposed-face grounding fix and regression on `fixes/agent-5`.
- [ ] After green exact-SHA focused CI + built replay + human visual gate, set pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) and move only this capture `open -> pending` in a bookkeeping commit.
- [ ] Complete every remaining checkbox/acceptance item and record passing verification evidence.
- [ ] Move only this capture `pending -> closed`, set `status=fixed` and `resolvedUtc`, and commit final bookkeeping.
- [ ] Fetch current `origin/master`, merge into `fixes/agent-5`; if master advances, fetch/merge/retry.
- [ ] Push exact feature head to `origin/master` non-force and verify `master == fixes/agent-5`.
