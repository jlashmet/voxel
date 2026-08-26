# Experiment 006 — exact saved-camera framed-glazing replay

## Hypothesis

The attempt-2 framed/subdivided glazing should change the reported VoxelShowcase facade from a broad flat amber slab into distinct architectural panes with visible masonry ownership at the perimeter and between panes at the exact saved camera.

## What was performed

- Feature source: `ecb8fc1de735d4bd2eabbc7eec267fc2d9517578`.
- Targeted-CI request: `8286526f9baed716ba983d8fcd1c4a43c5bbda4f` on `ci-test/fixes/agent-3`.
- GitHub Actions run: `33004782593`, successful retry job `98299454658`.
- PlayMode smoke test: `VoxelEngine.Tests.PlayMode.KentridgeStructureComparisonSceneTests.SceneBuildsOriginalAndModifiedThroughProductionRenderer`.
- SceneIssue replay: `SceneIssues/open/20260826-132429-861-VoxelShowcase/issue.json`, 45 seconds in the standalone player.
- Successful artifact: `single-test-33004782593`, artifact id `9620696687`.

## Result

The retry completed with workflow conclusion `success` and `ci/single-test=success`. The PlayMode test passed, the standalone replay exited successfully, three exact-camera screenshots were produced, and the required artifact upload succeeded.

The final frame is preserved beside this experiment as `verification-final.jpg`. At the reported camera, the former broad amber treatment is now split into two distinct inset amber panes with masonry retained between and around them; the smaller opening at right is likewise visibly subdivided.

## What was learned

The structural visual hypothesis is confirmed: the captured facade no longer presents the original uninterrupted amber slab. Because the report is subjective ("they look aweful"), repository workflow still requires explicit human approval of the final frame before the capture can be marked fixed.

## Next

Present `verification-final.jpg` for human approval. If approved, recheck current master and create the separate terminal bookkeeping commit; if rejected, keep the capture open and use the feedback for the next product experiment.
