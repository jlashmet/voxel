# Experiment 002 — startup framing

## Hypothesis
The authored terrain is adequate, but the startup camera is too high/telephoto and flattens depth.

## Action
Try a lower startup presentation `(0, 9.5, -20)`, look at `(0, 2.5, 30)`, FOV 36, with a depth-band regression.

## Result
The focused regression passed, but real-player run `33132706675` stayed byte-stable. Its log shows SceneIssue replay restoring and pinning the captured camera at `(-0.70, 18.80, -18.50)`, FOV 29 after startup.

## Verdict
Falsified. Startup framing cannot change the acceptance replay.
