# Experiment 039 — preserve distinct module player evidence

## Demonstrated defect
Artifact 9980400666 from exact run 34001756898 executes FarWorld before Water, but retains only Water logs and screenshots under `ModuleValidation/Results/Players/Assets_VoxelEngine_Rendering`. The same module-only output path also loses earlier Showcase and CharacterMotor player evidence. `showcase-player-capture.sh` removes Screenshots and rewrites fixed log filenames; successful earlier execution does not preserve inspectable evidence.

## Minimal correction
`run-module-validation.py` now gives each module/scene/scenario identity a distinct stable output subdirectory. A bounded scene-name prefix plus a digest of all three full identities avoids collisions from equal basenames, sanitized names, or plan-order changes. The summary records each output path. No validation targets, assertions, renderer modes, time limits or budgets change.

## Local behavioral proof
`python -m unittest discover -s /mnt/data/agent4-work/tests -v` exercised the actual runner with subprocess execution stubbed at the external Unity boundary. The stub reproduces the capture helper's deletion and fixed filenames. Against the exact baseline runner blob `2aeded24091da341ac8732a37e095adf50f25010`, three collision/preservation regressions fail; the ordering regression passes. With the correction, all four pass. These are Python orchestration tests, NOT Unity or visual acceptance.

## Integration state
The draw-owner diagnostic source is `affc45d54e08362ed6c7515a537bfb386eca4590`, request `019f5562d8b9d2575de0024d71ccbdb55dca028f`, run 34006671692. That queued exact request is untouched. This independent evidence correction is a later feature commit and must be included in the next exact validation after the diagnostic completes; it cannot retroactively repair that earlier artifact. Keep final evidence/visual checklist items unchecked until distinct built-player outputs are verified.
