# Replay verification evidence

- Capture: `20260825-040805-194-VoxelShowcase`
- Verified production/test source: `433bbe8ed24ce43627d4ff547d46e53930121f9e`
- Structural regression: `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary`
- Targeted CI: request commit `c5243ad758f2e6349fb64268dbd3dbc447893616`, Actions run `32882777952`, `ci/single-test = success`, exactly one test case executed.
- Exact replay: Actions run `32883739976`, head `a1836b7960f7f3890d9cef29734b206d69e25669`, success.
- Replay artifact: `sceneissue-040805-replay-32883739976`, artifact id `9576969385`, digest `sha256:dd5054c0af40b0c27260e4919e97cb9e29e81ec0e111c6795b6467d6b516f7c2`.
- Replay setup: fresh VoxelShowcase bake; saved `issue.json` SceneIssue replay; 1364×836; 70 seconds; original capture reference time 58.97969436645508 seconds.
- Frames inspected: all six emitted replay PNGs plus the bundled original `screenshot-001.png`.
- Visual result: frame 0 is still loading; frames at 24.4s, 34.4s, 44.4s, 54.4s, and 64.4s are stable and preserve the Kentridge road/stairs, retaining walls, lamps, adjacent buildings, and distant context. No duplicate-town or missing-structure regression is visible.
- Coverage result: from t=51s through t=70s, `SURFACE visible=544 min=544 max=544 swing=0 drops=0 missingMax=0`.
- Note: the standalone development replay draws the SceneIssue note overlay, so the replay is not pixel-identical to the original editor screenshot. This capture has no circled visual defect; its acceptance condition is the Kentridge authoring/ownership boundary.
