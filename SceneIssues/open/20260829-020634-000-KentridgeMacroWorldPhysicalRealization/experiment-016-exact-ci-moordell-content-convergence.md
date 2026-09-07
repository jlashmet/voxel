# Experiment 016 — Exact CI Moordell content convergence

## Hypothesis
The endpoint-safe Rossdam correction should restore production planning while allowing the existing evidence driver to progress through every required macro target inside the supported 60-second replay.

## Action / source
Exact feature source `cb4e1b0fa1464da214d799ba55750bea38159143` was requested through CI wrapper `e5bdf88a387c486b72be5d8950fb6d1458a5aad9`; the wrapper commit's parent is the exact feature source. Workflow run `33304172039`, job `99237645919`, executes focused PlayMode test `VoxelEngine.Tests.PlayMode.KentridgeRossdamRouteConstraintTests.RossdamLakeKeepsNorthernJunctionDryWhileStillForcingAuthoredDetours` and a 60-second `KentridgePlayableSlice` standalone replay.

## Result
Mixed; feature visual-red. The exact focused test passes `1/1`. Standalone exits cleanly with assertion failures `0`, no swap growth, peak RSS `5,571,344 KB`, and late-run FPS samples approximately `197`, `222`, `232`, `168`, and `137`. Visible streaming coverage converges (`sampled=100`, `missingVisible=0`, `coverage=True`).

Closure evidence does not converge. Full-resolution captures at approximately 39 s, 49 s and 59 s all remain on Moordell with `Moordell waiting for content coverage`; there are no subsequent evidence targets for Rossdam, Rossdam Lake, Fairy Village, Orc Village, Southern Ridge/pass, or network. Near 60 s the readiness telemetry remains `contentCols=5 readyContent=4 missingContent=1`, so one content column prevents target advancement despite complete visible coverage.

## Verdict / next discriminator
Do not promote from workflow success alone. Inspect the Moordell readiness content-column set against the validation-only semantic streaming demand and actual authored evidence footprint. Preserve real content-settled and renderer-coverage gates. Do not increase normal load radius, add broad residency/prestreaming, serialize building-centre prestreaming, skip readiness, or extend replay beyond 60 s. Fix only a proved required-demand mismatch or irrelevant-content-column mismatch, then rerun one exact-SHA closure request.