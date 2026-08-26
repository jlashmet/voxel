# Experiment 018 — final cleanup regression

## Hypothesis
After removing the temporary capture-specific replay test, the final production/test tree still preserves the tree gameplay contract: surviving semantic wood blocks a player-sized volume, removed wood stops blocking, and the showcase projectile/damage path removes semantic tree branches.

## What was performed
- Production/test fix commit: `fdc54b765714c5b6df5787ecc57640be2d356381`.
- CI request commit: `f3bbe8ab5b55e442fb9422496b79ebbeeb350f76` on `ci-test/fixes/agent-6`.
- GitHub Actions run: `33000528711`.
- Requested exactly `VoxelEngine.Tests.PlayMode.ShowcaseTreeInteractionRegressionTests` in PlayMode with request id `sceneissue-033053-final-fdc54b-20260826T1835Z`.

## Result
**Passed.** Unity executed exactly 3 test cases and returned status 0. The workflow published `ci/single-test: success` and uploaded artifact `single-test-33000528711` (artifact ID `9618496725`, SHA-256 `2942cb1f7cefd34a0783c5d74e1ff2b69ec6f9e4681cc8079908f14afb49cadd`).

After this green run, current `master` advanced only with scene-agent CI/process coordination files (`.github/test-request.json`, `.github/workflows/tests-single.yml`, `AGENTS.md`, `Docs/scene-agent-efficiency-plan.md`, `SceneIssues/README.md`, and `tools/showcase-player-capture.sh`). Those files were merged into `fixes/agent-6` as a two-parent merge without changing production code, tests, scene data, or replay evidence, so the tested behavior inputs remain byte-for-byte the green `fdc54b7...` tree.

## What was learned
**Hypothesis confirmed.** The permanent three-case regression is green on the final production/test fix after temporary replay wiring was removed. The subsequent master integration is process-only and does not invalidate that behavior result.

## Next
Write terminal `issue.json` fields using `fdc54b765714c5b6df5787ecc57640be2d356381` as `fixCommit`, move the whole capture from `SceneIssues/open/` to `SceneIssues/closed/` in a separate bookkeeping commit, verify the remote feature/CI state, then promote the integrated feature branch to `master` non-force and verify terminal master state.
