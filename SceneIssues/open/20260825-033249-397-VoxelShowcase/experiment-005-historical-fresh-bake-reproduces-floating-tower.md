# Experiment 005 — Historical fresh bake reproduces floating tower

## Hypothesis

If the saved capture was caused by stale checked-in startup bake data rather than generation source, then freshly baking the Voxel Showcase from the capture-era source commit and replaying the exact saved pose should remove the floating tower.

## What I performed

- Source under test: `760dc909138088a46778f026501c17dd25f1b86d`.
- Trigger / harness commit: `e7e1c74ab60d5afe2c72af007e9e799a267628df`.
- GitHub Actions run: `32892084683`.
- Artifact: `sceneissue-033249-capture-source-32892084683` (`9580111331`, digest `sha256:d300f288edbd6032731341536310e55522cfa218d1815df4b55419a6cc5e8b39`).
- The workflow checked out the historical source, restored the assigned capture fixture plus only the current two-file replay harness, freshly baked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes`, and replayed the exact saved VoxelShowcase pose for 240 seconds at the original `1364x836` capture resolution.
- I inspected the untouched original screenshot and all 23 replay screenshots from `t=14.6s` through `t=235.3s`, including frames bracketing the original `222.427658s` capture time.

## Result

**Reproduced.** The same unsupported roofed/tower-like structure visible in the original capture is present in every historical fresh-bake replay frame after the world becomes visible, including the `t=215.3s` and `t=225.3s` frames surrounding the original capture time. The replay remained otherwise stable (`visible=204`, no missing/reappeared surfaces in the tail).

The bake itself completed successfully and reported `199 regions`, `10.6 MiB`, seed `0x5EED1234`, so this is not a failed or partial bake artifact.

## What I learned

The stale-startup-bake hypothesis is false. The floating tower was produced by capture-era generation/placement source itself. A later change already present on `fixes/agent-1` removes it in a fresh exact-pose replay, so the next task is to identify the smallest responsible source delta and make the support/placement condition explicit in a focused regression rather than relying on an incidental stacked fix.

## Next

Compare the capture-era Kentridge/showcase generation path against the current branch, isolate the smallest source change that removes this specific unsupported object, then add a focused regression that fails on the historical behavior and passes on the intended placement before making any capture-specific production adjustment.
