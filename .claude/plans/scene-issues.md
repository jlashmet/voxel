# Scene Issues Queue

Goal: work through the captured `SceneIssues/` queue oldest-first on the shared `fixes` branch, resolving exactly one capture at a time with a focused regression, the smallest production fix, targeted CI validation, replay/fixture verification where possible, and issue bookkeeping.

## Constraints

- Follow `SceneIssues/README.md` ordering and bookkeeping rules.
- Keep all fixes on the single feature branch `fixes`; use only `ci-test/fixes` for targeted CI requests.
- Do not weaken rendering/performance budgets or unrelated assertions.
- Prefer direct geometry/material/streaming invariants over fragile pixel goldens.
- Do not treat broken screenshots as expected baselines.
- Do not run Unity directly; connector-only validation uses the targeted CI branch.
- Original capture screenshots/circles stay unchanged.

## Queue

- [ ] `20260823-013834-177-VoxelShowcase` — side of the wall isn't rendering.
- [ ] `20260823-013924-433-VoxelShowcase` — terrain shape / grass-to-dirt transition regressed.
- [ ] `20260823-014011-920-VoxelShowcase` — overhead near terrain looks blue/spiral.
- [ ] `20260823-014108-038-VoxelShowcase` — waterfall missing; terrain renders where it should not.
- [ ] `20260823-014327-420-VoxelShowcase` — terrain mound appears in front of town then disappears a few steps forward (two captures).
- [ ] `20260823-014636-322-VoxelShowcase` — transient brown terrain artifact.

## Per-issue loop

- [x] Read note, all capture poses/circles, and inspect every screenshot available through the working environment.
- [x] Reproduce/locate the marked failure and identify the smallest responsible subsystem.
- [x] Add or extend a focused regression using the saved scene/pose fixture or a more direct invariant.
- [ ] Implement the smallest production fix.
- [ ] Commit production + regression with the capture id in the message.
- [ ] Validate the narrowest relevant Unity test through `ci-test/fixes`; iterate on failure.
- [ ] Re-check the saved viewpoints/fixture and relevant marked regions.
- [ ] Update `issue.json` with `status`, `resolvedUtc`, `resolutionSummary`, `regressionTest`, and the production/test `fixCommit` SHA.
- [ ] Commit bookkeeping separately, then advance to the next issue.

## Current investigation

Starting with `20260823-013834-177-VoxelShowcase`. The capture is at camera position `(99.2358, 26.4501, -4.8227)` in `VoxelShowcase`, with marked regions centered near normalized screen `(0.618, 0.705)` and `(0.407, 0.448)`. The note says the side of the wall is not rendering.

The GitHub connector exposes the screenshot files but does not expose binary PNG bytes to the current runtime, so screenshot inspection must be supplemented by the recorded pose/circles and deterministic scene/geometry tests until a replay-capable environment is available. Do not mark an issue fixed solely from speculation; validation must prove the underlying invariant.

The first saved-pose regression moved only `Camera.main`, which is not sufficient for this scene: VoxelShowcase streaming follows the showcase/player transform. A corrected CI-side replay calls `showcase.TeleportTo(capturePosition)` before restoring the recorded camera rotation. Promote that correction onto `fixes`, then rerun the exact capture regression on the current master-merged head and use its renderer metrics/logs to choose the smallest production fix.
