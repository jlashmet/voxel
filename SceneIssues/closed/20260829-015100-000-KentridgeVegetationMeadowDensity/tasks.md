# Tasks — Kentridge vegetation meadow density

## Investigation / implementation
- [x] Resume `fixes/agent-5`; read `AGENTS.md` and canonical `SceneIssues/README.md` (`SceneIssues/feature-readme.md` is absent).
- [x] Maintain separate `plan.md` and `tasks.md` before/during implementation.
- [x] Inspect the assigned capture, exact camera, WorldBuilder definition, runtime ecology sampling, placement, packed renderer/shader, tests, CI logs, and built-player artifacts.
- [x] Implement reusable per-area ecology policy for allowed vegetation/tree/animal kinds, density/coverage, deterministic variation, and route/built/water/cultivated/steep/invalid exclusions.
- [x] Configure Kentridge countryside through that top-level path: semantic Grass only; no trees or ambient animals; no scene-local coordinate scatter or per-blade GameObjects.
- [x] Preserve deterministic packed 5–15 blades-per-instance expansion and report renderer-equivalent total/connected-meadow diagnostics.
- [x] Use the shared grass shader/presentation; engine-managed time drives wind; no Kentridge shader fork or legacy grass sprite.
- [x] After three failed clock attempts, isolate the actual packed mesh + production shader and prove visible framebuffer deformation.
- [x] Confirm exposed-top-face grounding contract; later built evidence proved grounding was not the camera-visibility cause.
- [x] Experiment 005 identified the causal defect: the 12k sample cap at 0.4 m spacing exhausted behind the required player camera.
- [x] Apply the smallest causal correction: Kentridge sample spacing 0.4→0.8 m while keeping density 0.96 and `MaxUndergrowth=12000` unchanged.
- [x] Add production-camera regression using the real `Kentridge Player Camera` and production grass roots/frustum.

## Regression / acceptance
- [x] WorldBuilder exposes reusable area vegetation kinds/density plus ambient-animal policy hook.
- [x] Kentridge allows only procedural Grass and enables no ambient animal/tree kinds.
- [x] Allowed-kind filtering, density, deterministic placement/seed behavior, and all required exclusion classes are behaviorally covered.
- [x] Connected-meadow accounting and shared blade expansion are deterministic and renderer-equivalent.
- [x] Exact production scene reports >=3,000 blades in one connected meadow: final primary meadow = 57,752 blades.
- [x] Final scene reports zero excluded-surface grass and actively rejects route candidates (1,694); reusable tests cover built/water/cultivated/steep/invalid exclusion authoring/behavior.
- [x] Production-camera regression proves grass reaches the required view: 11,322 roots in front, 3,664 root clusters in frustum, 116.02 m forward coverage.
- [x] Exact built player launches `KentridgePlayableSlice`, remains usable, and has no startup/runtime exceptions.
- [x] Human visual inspection: player-height 39.8/49.8/59.8 s frames plainly read as a dense field made from individual procedural blades, not sparse/tiled/floating/buried accents.
- [x] Human visual inspection: stationary blade silhouettes visibly change over time; grass-band pixel deltas are 42.89% and 44.08% between successive late frames with sky/dialogue excluded.
- [x] No legacy grass sprite, scene-local scatter, thousands of grass GameObjects, Kentridge-only renderer, or shader fork introduced.
- [x] Durable final verification is stored in `verification-final.txt`.

## Blast radius / cost
- [x] Production coverage change is Kentridge authoring only; shared 12k semantic-instance cap, density, blade expansion, exclusion API, and packed topology remain bounded.
- [x] Final realization: 11,322 semantic instances / 113,490 blades / 16 packed chunks; zero excluded leakage.
- [x] Built player: 157 MB; build report 36.270 s; wrapper RSS 573 MB / peak 6,136 MB; ordinary captured play after warmup ~60–73 FPS before the held stationary phase.
- [x] Shared wind remains GPU-side and engine-managed time removes the prior custom per-frame grass-time state/write.
- [x] Harness does not expose separate CPU-ms/GPU-ms; no missing metric is invented.
- [x] Feature-only diff against current master contains only assigned Kentridge ecology/render/tests/SceneIssue files; no feature `.github/test-request.json`, unrelated capture, or workflow change.

## Final exact-SHA / promotion gate
- [x] Final validated source: `ec92c3002a6b75ca86de7819f4175c5390a1ca2b`.
- [x] Exactly one fresh final request after the source correction: `d71730e46c2e12bc81e8c6e58cb87c07525904e3` on `ci-test/fixes/agent-5`.
- [x] Workflow `33249542767` completed successfully; `ci/single-test=success`; focused PlayMode regression, player build/replay, previews, and artifact upload all passed.
- [x] Final artifact inspected directly; mandatory meadow-density and animation visual gates pass.
- [x] Pending metadata set (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`).
- [x] Only this assignment moved `open -> pending` in a separate bookkeeping commit.
- [x] Every acceptance criterion and checkbox required for closure is complete and validated.

## Remaining workflow operations after closure
- Set `status=fixed` and `resolvedUtc`, then move only this assignment `pending -> closed`.
- Fetch current `origin/master`, merge it into `fixes/agent-5`, stopping for any conflict outside assigned work.
- Non-force push that exact feature head to `origin/master`; if master advanced, fetch/merge/retry; verify `master == fixes/agent-5`.
