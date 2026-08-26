# Experiment 10 — exact saved-view standalone verification

## Hypothesis

With the flagship showcase's detail-band scale restored to `1.0`, the original saved camera should remain inside the intended 96 m finest voxel band throughout all three marked regions, eliminating the coarse terrain patches seen in the failing replay.

## What was performed

Requested `VoxelEngine.Tests.PlayMode.ShowcaseSceneIssue032832ReplayTests.SavedFixtureIsConfiguredForExactReplay` through `ci-test/fixes/agent-4` at request commit `c2ecb585ff5e081a52e363ad48358e3cef3a6007`. That CI request is one bookkeeping commit above exact feature source `e47afdab13278d3cfdce79f43805b7feb4f89cac`, whose production ancestor is fix commit `ca89c74b653f21f936218c60464079641f12459f`.

GitHub Actions run: `32933454625`. Result artifact: `single-test-32933454625` / artifact id `9594146911`.

The shared profile executed the requested PlayMode fixture and then ran the real standalone `VoxelShowcase` player at the captured 1364x836 saved camera framing for the full 60-second capture window.

## Result

Confirmed.

- Requested PlayMode fixture: 1/1 passed.
- Standalone player capture: completed 60 seconds with zero harness assertion failures and `missingVisible=0` after convergence.
- Settled voxel bands: `0-96`, `96-192`, `192-288`, `288-409.6` metres, replacing the failing replay's `0-57.6` first handoff.
- Visual adjudication of the final presented frame: the former low-resolution/coarse boundary geometry through the second and third circles is gone, and all three original marked regions show the intended detailed terrain representation.

The Actions job-level conclusion was `cancelled` at the workflow time ceiling after the requested test, real-player capture, evidence generation, artifact upload, and final-status steps had completed successfully. The focused production regression was independently formally green in run `32933287067`; this run supplies the required exact saved-view real-player verification.

## Decision

The capture meets its acceptance criteria. Record the production fix as `ca89c74b653f21f936218c60464079641f12459f`, set terminal issue bookkeeping, and move the complete capture directory to `SceneIssues/closed/` without starting another capture.
