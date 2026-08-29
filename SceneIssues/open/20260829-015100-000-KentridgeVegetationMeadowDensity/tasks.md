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
- [x] Experiment 003: engine-managed `_Time.y` removes custom clock state and its focused test/build passed, but run `33246992214` still showed zero grass-region pixel change; third clock hypothesis falsified.
- [x] After the third failed attempt, isolate the visible grass draw in the focused production-scene regression: render the actual packed grass batch + production shader well above terrain and require visible framebuffer pixels to deform over real time.
- [x] Experiment 004: test exposed-face grounding `(surface + 1) * VoxelSize` and add matching generated-root assertion.
- [x] Classify run `33247434464` as product compile failure before tests/capture; restore the immediately preceding green interface-based `KentridgeRegionLife.Populate(...)` implementation.
- [x] Run corrected request `68a043abf55d453a3aa2d435aee191a6e7e6de6b` / workflow `33247764440`: focused test and player harness are workflow-green at exact source `6163f48f232025efe5cf19c10c10b3762ac7fbd2`.
- [x] Inspect `33247764440` real-player artifact directly: late stationary foreground is byte-identical while sky moves, so workflow success does not satisfy meadow/wind visual acceptance.
- [x] Compare pre-grounding player run `33246992214` with exposed-grounding run `33247764440`: corresponding foreground pixels are identical, falsifying exposed-root grounding as the cause of the visible failure.
- [ ] Experiment 005: prove whether generated grass roots/bounds intersect the exact Kentridge replay camera frustum and whether explicit production-batch submission to that camera produces visible pixels.
- [ ] Implement only the correction supported by experiment 005; remove/revert experiment-004 grounding behavior if it is not part of the proven surface contract.
- [ ] Add/adjust behavioral regression so it fails for the real production-camera visibility defect, not only global blade counts or an isolated synthetic camera.

## Regression / architecture checks
- [x] Prove Kentridge definition exposes grass-only dense regional policy and empty tree/ambient-animal allowlists.
- [x] Prove each required exclusion class can be authored independently.
- [x] Prove production-path deterministic meadow placement reaches `>=3000` renderer-equivalent blades in one connected field and only eligible samples produce grass.
- [x] Prove allowed-kind filtering, density behavior, and placement are deterministic.
- [x] Prove shared blade expansion is deterministic/bounded at 5–15 and is the renderer's exact contract.
- [x] Preserve packed chunk renderer; no legacy sprite, renderer-side global density magic, Kentridge-only scatter loop, per-blade GameObjects, or shader fork.
- [x] Green exact-SHA focused production-scene regression at `6163f48...` / run `33247764440`, including isolated shader deformation; this is necessary but not sufficient because the player visual gate fails.
- [ ] Production-camera regression proves the actual replay view contains submitted, visible packed grass.
- [ ] Green final exact-SHA Kentridge built-application replay proving visible blade motion.

## Blast radius / cost
- [x] WorldBuilder API remains additive; non-Kentridge callers retain defaults unless opting into ecology policy.
- [x] Density realization changes remain Kentridge composition + reusable policy/vegetation APIs.
- [x] Wind changes affect shared packed Grass/Nettle presentation only.
- [x] Final green-run density diagnostic so far: 11,469 semantic grass instances / 115,119 blades total; primary connected meadow 5,760 / 57,724 blades; 8 chunks; zero excluded-surface leakage.
- [x] Run `33247764440` player remained usable and reported approximately 58–83 FPS during normal captured play after startup before held-scene phases accelerated; artifact RSS/peak/build measurements are retained in CI evidence. Exact CPU-ms/GPU-ms are unavailable and are not inferred.
- [x] Engine-clock correction lowers CPU state cost: removes per-frame grass time material write, MPB clear/float write, persistent MPB, delegate, and time-source read; no new topology, draw, allocation, material, or CPU blade animation.
- [x] Experiment-004 exposed-face grounding cost is one integer increment per Kentridge ground sample, but the built-player comparison shows no visual benefit; do not count it as an accepted fix.
- [ ] Re-evaluate final runtime/density/leakage/visual-contact evidence after causally supported production-camera correction.

## Final exact-SHA validation / visual gate
- [x] Run `33246992214` is the third green workflow / failed visual experiment: engine-managed clock still yields exactly zero grass/ground pixel change at late stationary captures.
- [x] Refresh current `origin/master`: `521ba9c1fc5531f299f09595316dff03af01df57` was the feature merge base at last check (`behind_by=0`).
- [x] Review feature-only diff at source `6163f48...`: only assigned Kentridge ecology/render/tests/issue files changed; no unrelated capture/workflow and no feature `.github/test-request.json` change.
- [x] Request/run `4cb67bf942b5b4f9ad8834bb8c4ac92780ac84f2` / `33247434464` completed with a product compile failure and produced no validation evidence; it does not count as a gate.
- [x] Corrected request `68a043abf55d453a3aa2d435aee191a6e7e6de6b` / run `33247764440` completed; leave it intact as historical evidence rather than replacing it.
- [x] Built player reaches usable `KentridgePlayableSlice` without startup/runtime exceptions in run `33247764440`.
- [x] Diagnostic in run `33247764440` proves one connected meadow has `>=3000` rendered blades and zero excluded-surface placements.
- [ ] Gameplay/player-height replay plainly reads as dense procedural meadow made of visible blades, not sparse/tiled/floating/buried grass.
- [ ] At least two late stationary frames visibly change blade silhouettes/poses due to wind.
- [ ] After feature source changes, refresh master/diff and issue exactly one fresh final exact-SHA request on the assigned idle `ci-test/fixes/agent-5` mailbox; do not create another transport or replace queued work.
- [ ] Store concise durable final verification evidence beside the issue.

## Acceptance (issue.json)
- [x] (1) WorldBuilder exposes reusable per-area vegetation kinds/density controls plus ambient-animal policy hook.
- [x] (2) Kentridge uses that path, allows only procedural Grass, and enables no ambient-animal kinds.
- [ ] (3) Final built app proves a connected Kentridge meadow has `>=3000` visible blades and visually reads as full meadow.
- [ ] (4) Final built gameplay visibly animates grass while player/camera are stationary.
- [ ] (5) Final evidence confirms roads, structures/interiors, water, steep/invalid terrain, cultivated surfaces when semantically identified, and other exclusions are not carpeted.
- [x] (6) No legacy grass sprite, scene-local scatter, thousands of grass GameObjects, or Kentridge-specific shader fork introduced.
- [ ] (7) Behavioral regressions cover WorldBuilder policy, allowlist, density, determinism, exclusions, empty animal allowlist, blade expansion, packed GPU-only wind topology, isolated render deformation, and the real production-camera visibility defect.
- [ ] (8) Exact built-application Kentridge harness is green and usable for final source.
- [ ] (9) Durable visual evidence satisfies mandatory meadow-density and animation checks.
- [ ] (10) Final blast-radius/cost evidence is measured/documented and acceptable.

## Metadata / promotion / publish
- [x] Commit ecology/density implementation and regressions on `fixes/agent-5`.
- [x] Commit engine-managed wind implementation and focused regression cleanup on `fixes/agent-5`.
- [x] Commit experiment-004 grounding/minimal-render regression on `fixes/agent-5`; later evidence now requires re-evaluation rather than treating that behavior as resolved.
- [ ] After green final exact-SHA focused CI + built replay + human visual gate, set pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) and move only this capture `open -> pending` in a bookkeeping commit.
- [ ] Complete every remaining checkbox/acceptance item and record passing verification evidence.
- [ ] Move only this capture `pending -> closed`, set `status=fixed` and `resolvedUtc`, and commit final bookkeeping.
- [ ] Fetch current `origin/master`, merge into `fixes/agent-5`; if master advances, fetch/merge/retry.
- [ ] Push exact feature head to `origin/master` non-force and verify `master == fixes/agent-5`.
