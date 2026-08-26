# Experiment 005 — Exact VoxelShowcase replay verification

## Hypothesis

If the Kentridge ownership consolidation preserved scene behavior, then a fresh-baked standalone `VoxelShowcase` replay using the original SceneIssue metadata should converge to a stable Kentridge view with the expected street/architecture present and no visible-surface coverage loss. This is the required post-fix replay check; it does not require pixel identity because the capture's acceptance condition is architectural ownership rather than a circled rendering defect.

## What performed + source commit

- Verified production/test source commit: `433bbe8ed24ce43627d4ff547d46e53930121f9e`.
- Reconfirmed the focused ownership request commit `c5243ad758f2e6349fb64268dbd3dbc447893616` has `ci/single-test = success`; Actions run `32882777952` executed exactly one test case for `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary`.
- Added a capture-specific one-shot replay workflow only for verification, then tightened it before the final run so it:
  - fresh-bakes `Assets/Scenes/VoxelShowcase.unity` from the feature source,
  - passes the original `SceneIssues/open/20260825-040805-194-VoxelShowcase/issue.json` through the repository's `--scene-issue` standalone-player path,
  - matches the original `1364x836` capture dimensions,
  - runs the fixed view for 70 seconds, past the original capture time of `58.97969436645508` seconds,
  - bundles both the original `screenshot-001.png` and replay frames in the evidence artifact.
- Final replay source tip was `a1836b7960f7f3890d9cef29734b206d69e25669`; Actions run `32883739976` completed successfully.
- Downloaded and inspected artifact `sceneissue-040805-replay-32883739976` (artifact id `9576969385`, digest `sha256:dd5054c0af40b0c27260e4919e97cb9e29e81ec0e111c6795b6467d6b516f7c2`).
- Inspected every emitted replay frame: `showcase-000-t014.4s-stationary.png` through `showcase-005-t064.4s-stationary.png`, plus the bundled original capture.
- Reviewed the replay player telemetry around and after the original capture time. From t=51s through t=70s, `SURFACE` remains `visible=544`, `min=544`, `max=544`, `swing=0`, `drops=0`, and `missingMax=0`.

## Result

Passed.

The 14.4-second frame is still in scene loading, as expected. From 24.4 seconds onward, all five settled replay frames are visually stable and retain the expected Kentridge corridor: the road/stair run, retaining walls, lamps, adjacent building masses, and distant town/world context remain present without a structural disappearance or duplicate-town artifact. The frame nearest the original ~59-second capture time remains fully converged, and the surface telemetry reports no missing visible chunks or coverage drops through the end of the run.

The standalone replay includes the SceneIssue development overlay and is therefore not a pixel-for-pixel reproduction of the editor capture; the overlay also obscures part of the upper center. That does not invalidate this capture's acceptance because there are no circled visual regions and the issue note explicitly asks for one canonical Kentridge authoring/ownership path. The replay demonstrates that the consolidated authoring path still produces a coherent Kentridge scene after a fresh bake.

## What learned

The ownership cleanup is not only structurally green in EditMode; it survives a fresh production-style VoxelShowcase bake and standalone replay at the recorded scene view. The relocated physical backend can remain temporarily under `Assets/Game/WorldBuilder/Generation` without introducing a second town-authoring path.

The architecture review remains important: this successful replay does **not** make the temporary physical location the desired end state. `Game.WorldBuilder` should remain the semantic authoring layer, while generic layout, architecture, and voxel realization are still intended to move behind `VoxelEngine.WorldGen` in follow-on work.

## Next

Remove the temporary capture-specific replay workflow, update the durable plan with the completed verification, then perform the required separate terminal bookkeeping commit: set `issue.json` to `fixed`, record the regression/fix commit, move the entire capture from `SceneIssues/open/` to `SceneIssues/closed/`, push `fixes/agent-1`, and stop without selecting another capture.
