# Experiment 007 — current-master pending replay

## Hypothesis

After integrating current `master`, the framed rectangular glazing still reads as intentional architectural windows at the exact reported camera: two inset panes separated and surrounded by masonry rather than one flat amber slab.

## What was performed

- Integrated feature source: `9de547d259760989b56a29916819f2c99cbd8d64`.
- Targeted-CI request: `bd8f93939a616b639275a7dd86a9793a70a561bc` on `ci-test/fixes/agent-3`.
- GitHub Actions run: `33014640709`.
- PlayMode smoke test: `VoxelEngine.Tests.PlayMode.KentridgeStructureComparisonSceneTests.SceneBuildsOriginalAndModifiedThroughProductionRenderer`.
- Exact replay fixture: `SceneIssues/open/20260826-132429-861-VoxelShowcase/issue.json`, `replay_seconds=45`.
- Successful artifact: `single-test-33014640709`, id `9624063061`, digest `sha256:7fe5d59cc96cf4f79eeca58d82353ecc0dab68cd3873b58f6beb053aad4336d2`.

## Result

Passed. The PlayMode test, exact standalone-player replay, screenshot previews, required artifact upload, and final status publication all succeeded. `ci/single-test` is `success` on the exact request SHA. The exact-pose replay still shows paired recessed amber panes with a masonry divider/perimeter; the former uninterrupted amber slab is absent. A downscaled copy of the exact final frame is committed as `verification-final.png`.

## What was learned

Hypothesis confirmed. The attempt-2 glazing composition survives current-master integration and the current shared replay workflow. The fix is ready for the repository's pending human-review queue.

## Next

Perform the separate open-to-pending bookkeeping commit with `status: pending`, terminal summary/regression/fix SHA filled, `resolvedUtc` left empty, verify the remote pending state, then stop and wait for the coordinator.
