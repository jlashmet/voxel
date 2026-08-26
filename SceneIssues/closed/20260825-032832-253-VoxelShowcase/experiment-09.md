# Experiment 09 — green flagship fine-band regression

## Hypothesis

Restoring the saved `VoxelShowcase` scene's detail-band scale to `1.0` returns the finest terrain band to the configured 96 m extent without changing the renderer's LOD architecture.

## What was performed

Production commit `ca89c74b653f21f936218c60464079641f12459f` changes only two behaviors:

- `Assets/Scenes/VoxelShowcase.unity`: `m_DetailBandScale` from `0.6` to `1`.
- `ShowcaseSceneIssue032832ReplayTests`: use a normal frame yield in batchmode instead of `WaitForEndOfFrame`, while retaining `WaitForEndOfFrame` for graphical editor execution.

Requested `VoxelEngine.Tests.EditMode.ShowcaseLodPresentationTests.FlagshipShowcaseKeepsFullResolutionFineBand` through `ci-test/fixes/agent-4` at request commit `be761040ea82cfd5680ccc27ae0b7c496ee40690` (`request_id=agent4-032832-fine-band-green`). GitHub Actions run: `32933287067`.

## Result

Confirmed. `ci/single-test` succeeded on the exact production source commit. The focused scene policy regression is now green: the 96 m configured fine band is no longer contracted to 57.6 m.

## Next

Run `VoxelEngine.Tests.PlayMode.ShowcaseSceneIssue032832ReplayTests.SavedFixtureIsConfiguredForExactReplay` from the exact current feature head. The shared CI profile must both pass the batchmode fixture and build/run the real `VoxelShowcase` player at the original 1364x836 saved camera framing. Do not close unless the standalone screenshots are visually clean in all three marked regions.
