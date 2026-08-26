# Experiment 08 — red flagship fine-band presentation regression

## Hypothesis

The saved-view low-resolution patches are exposed because `VoxelShowcase` contracts the scheduler's configured 96 m finest terrain band with `m_DetailBandScale = 0.6`, moving the first coarse handoff inward to 57.6 m.

## What was performed

Added `VoxelEngine.Tests.EditMode.ShowcaseLodPresentationTests.FlagshipShowcaseKeepsFullResolutionFineBand` on source commit `83f08f37a8587521c56090b62c996044991e8194`. The test reads the actual serialized `Assets/Scenes/VoxelShowcase.unity` setting and computes the live outer edge of the configured 96 m fine band.

Requested that exact test through `ci-test/fixes/agent-4` at request commit `2ecb18ffd236dde643e71cbcbde6df5db2c20cb7` (`request_id=agent4-032832-fine-band-red`). GitHub Actions run: `32932899389`.

## Result

Confirmed red. Unity executed exactly one test case and it failed on the intended assertion:

`The flagship showcase shrinks its finest terrain band to 57.6 m (scale 0.60).`

Expected at least `95.999 m`; actual `57.6000023 m`. The run reached NUnit normally and exited with Unity test failure code 2, so this is behavioral regression evidence rather than an import/compiler/infrastructure failure.

## What was learned

The exact scene configuration intentionally admits step-2 terrain 38.4 m earlier than the scheduler's full 96 m fine-band layout. That matches the first LOD handoff reported in the failed standalone replay and places coarse terrain in the marked mid-ground even after hierarchy double-submission is suppressed.

## Next

Restore the flagship showcase detail scale to `1.0` (96 m fine band), keep that as the component default so new/recreated showcase instances do not regress to `0.6`, make the saved-view PlayMode fixture batchmode-safe, then rerun this focused regression green before the authoritative standalone saved-camera replay.
