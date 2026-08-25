# Experiment 002 — capture-source fresh-bake replay (runner interference)

## Hypothesis

If the floating tower was caused by stale checked-in startup data rather than deterministic generation source, then a fresh bake from the exact source snapshot that existed when the issue was captured (`760dc909138088a46778f026501c17dd25f1b86d`) should replay the saved camera without the unsupported tower.

## What I performed + source commit

- Trigger branch/source commit: `906b6424a1fe6ef1e2f52f50c257610448c65e78` on `fixes/agent-1`.
- Diagnostic checkout: exact historical source `760dc909138088a46778f026501c17dd25f1b86d`, the last `master` commit before the capture timestamp `2026-08-25T03:32:49.3972530Z`.
- Injected only the assigned capture fixture (`issue.json` and `screenshot-001.png`) from the trigger commit into that historical checkout.
- Ran `VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeShowcaseWorld` to replace the historical checked-in startup image with a fresh bake.
- Intended to replay the exact saved camera at 1364×836 through 240 seconds.
- GitHub Actions run: `32889075632`, job `97936428954`.
- Artifact: `sceneissue-033249-capture-source-32889075632`, artifact id `9578884069`, digest `sha256:643d370ffd7ab36c848c527b91c4619fa25e82b3898ec4af0bc50dca1603284a`.

## Result

Inconclusive for the visual hypothesis. The historical-source fresh bake completed successfully with status 0 after 198 seconds and peaked at 11,806 MB RSS. The workflow then failed its post-bake *global* Unity-process wait before player replay because it detected an unrelated interactive Unity editor and AssetImportWorkers running from `/Users/jlashmet/code/voxel`. Those processes were not the batch process in the runner workspace (`/Users/jlashmet/tmp/_work/voxel/voxel`). Consequently the saved-view replay step was skipped and no replay screenshots were produced.

This is infrastructure interference, not a failed fix attempt and not evidence for or against the floating-tower hypothesis.

## What I learned

- The exact capture-time source can still fresh-bake successfully under the current runner/Unity version.
- The diagnostic workflow's process guard is over-broad: it keys on any `/Unity.app/Contents/MacOS/Unity` process on the machine rather than the runner workspace/batch process.
- The visual A/B still needs to be completed before assigning root cause.

## Next

Repeat the exact same historical-source fresh-bake replay while removing only the over-broad post-bake global process wait. Preserve the exact source snapshot, fixture, resolution, camera pose, and 240-second duration so the next result differs only in runner orchestration.
